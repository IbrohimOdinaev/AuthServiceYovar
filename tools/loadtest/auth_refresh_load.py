#!/usr/bin/env python3
import argparse
import base64
import hashlib
import html.parser
import http.client
import json
import os
import ssl
import statistics
import threading
import time
import urllib.parse
from concurrent.futures import ThreadPoolExecutor, as_completed


class LoginFormParser(html.parser.HTMLParser):
    def __init__(self):
        super().__init__()
        self.inputs = {}

    def handle_starttag(self, tag, attrs):
        if tag != "input":
            return

        values = dict(attrs)
        name = values.get("name")
        if name:
            self.inputs[name] = values.get("value", "")


def b64url(data):
    return base64.urlsafe_b64encode(data).decode().rstrip("=")


class BrowserSession:
    def __init__(self, base_url, forwarded_host, forwarded_for):
        self.base_url = base_url.rstrip("/")
        self.forwarded_host = forwarded_host
        self.forwarded_for = forwarded_for
        self.cookies = {}

    def request(self, method, url, body=None, content_type=None, follow=True):
        url = self._normalize_url(url)

        for _ in range(10):
            parsed = urllib.parse.urlparse(url)
            path = parsed.path or "/"
            if parsed.query:
                path += "?" + parsed.query

            headers = {
                "Host": self.forwarded_host,
                "X-Forwarded-Proto": "https",
                "X-Forwarded-Host": self.forwarded_host,
                "X-Forwarded-For": self.forwarded_for,
                "User-Agent": "authservice-load-test/1.0",
            }

            if content_type:
                headers["Content-Type"] = content_type

            if self.cookies:
                headers["Cookie"] = "; ".join(f"{key}={value}" for key, value in self.cookies.items())

            if parsed.scheme == "https":
                connection = http.client.HTTPSConnection(
                    parsed.hostname,
                    parsed.port,
                    timeout=30,
                    context=ssl._create_unverified_context())
            else:
                connection = http.client.HTTPConnection(parsed.hostname, parsed.port, timeout=30)
            connection.request(method, path, body=body, headers=headers)
            response = connection.getresponse()
            response_body = response.read()
            response_headers = response.getheaders()
            status = response.status
            connection.close()

            self._store_cookies(response_headers)

            header_map = {key.lower(): value for key, value in response_headers}
            location = header_map.get("location")
            if follow and status in (301, 302, 303, 307, 308) and location:
                url = self._normalize_url(location)
                if status in (301, 302, 303):
                    method = "GET"
                    body = None
                    content_type = None
                continue

            return status, response_headers, response_body, url

        raise RuntimeError("too many redirects")

    def _store_cookies(self, headers):
        for key, value in headers:
            if key.lower() != "set-cookie":
                continue

            first = value.split(";", 1)[0]
            if "=" not in first:
                continue

            name, cookie_value = first.split("=", 1)
            self.cookies[name] = cookie_value

    def _normalize_url(self, url):
        https_origin = f"https://{self.forwarded_host}"
        http_origin = f"http://{self.forwarded_host}"

        if url.startswith(https_origin):
            return self.base_url + url[len(https_origin):]

        if url.startswith(http_origin):
            return self.base_url + url[len(http_origin):]

        if url.startswith("/"):
            return self.base_url + url

        return url


def get_initial_refresh_token(args, vu_id):
    session = BrowserSession(
        args.base_url,
        args.forwarded_host,
        f"10.240.{vu_id // 250}.{vu_id % 250 + 1}")

    code_verifier = b64url(os.urandom(32))
    code_challenge = b64url(hashlib.sha256(code_verifier.encode()).digest())
    state = b64url(os.urandom(16))

    authorize_url = args.base_url.rstrip("/") + "/connect/authorize?" + urllib.parse.urlencode({
        "client_id": args.client_id,
        "response_type": "code",
        "scope": args.scope,
        "redirect_uri": args.redirect_uri,
        "code_challenge": code_challenge,
        "code_challenge_method": "S256",
        "state": state,
    })

    status, _, body, login_url = session.request("GET", authorize_url)
    if status != 200:
        raise RuntimeError(f"login page returned {status}: {body[:200]!r}")

    parser = LoginFormParser()
    parser.feed(body.decode(errors="replace"))

    return_url = parser.inputs.get("ReturnUrl")
    if not return_url:
        raise RuntimeError("ReturnUrl was not found")

    form = {
        "Email": args.email,
        "Password": args.password,
        "ReturnUrl": return_url,
    }

    csrf = parser.inputs.get("__RequestVerificationToken")
    if csrf:
        form["__RequestVerificationToken"] = csrf

    status, _, _, callback_url = session.request(
        "POST",
        login_url,
        urllib.parse.urlencode(form).encode(),
        "application/x-www-form-urlencoded")

    if status != 200:
        raise RuntimeError(f"login/callback returned {status}")

    callback_query = urllib.parse.parse_qs(urllib.parse.urlparse(callback_url).query)
    code = callback_query.get("code", [None])[0]
    if not code:
        raise RuntimeError("authorization code was not found")

    if callback_query.get("state", [None])[0] != state:
        raise RuntimeError("state mismatch")

    status, _, body, _ = session.request(
        "POST",
        args.base_url.rstrip("/") + "/connect/token",
        urllib.parse.urlencode({
            "grant_type": "authorization_code",
            "client_id": args.client_id,
            "redirect_uri": args.redirect_uri,
            "code": code,
            "code_verifier": code_verifier,
        }).encode(),
        "application/x-www-form-urlencoded",
        follow=False)

    if status != 200:
        raise RuntimeError(f"token exchange returned {status}: {body[:300].decode(errors='replace')}")

    token = json.loads(body.decode())
    refresh_token = token.get("refresh_token")
    if not refresh_token:
        raise RuntimeError("refresh_token was not returned")

    return refresh_token


def refresh_loop(args, vu_id, initial_refresh_token, stop_at):
    session = BrowserSession(
        args.base_url,
        args.forwarded_host,
        f"10.241.{vu_id // 250}.{vu_id % 250 + 1}")

    refresh_token = initial_refresh_token
    latencies_ms = []
    successes = 0
    failures = 0
    errors = {}

    while time.monotonic() < stop_at:
        started = time.perf_counter()

        try:
            status, _, body, _ = session.request(
                "POST",
                args.base_url.rstrip("/") + "/connect/token",
                urllib.parse.urlencode({
                    "grant_type": "refresh_token",
                    "client_id": args.client_id,
                    "refresh_token": refresh_token,
                }).encode(),
                "application/x-www-form-urlencoded",
                follow=False)

            elapsed_ms = (time.perf_counter() - started) * 1000
            latencies_ms.append(elapsed_ms)

            if status == 200:
                payload = json.loads(body.decode())
                refresh_token = payload.get("refresh_token") or refresh_token
                successes += 1
            else:
                failures += 1
                key = f"HTTP {status}: {body[:160].decode(errors='replace')}"
                errors[key] = errors.get(key, 0) + 1
        except Exception as exception:
            elapsed_ms = (time.perf_counter() - started) * 1000
            latencies_ms.append(elapsed_ms)
            failures += 1
            key = type(exception).__name__ + ": " + str(exception)
            errors[key] = errors.get(key, 0) + 1

    return successes, failures, latencies_ms, errors


def percentile(values, pct):
    if not values:
        return 0

    values = sorted(values)
    index = int(round((len(values) - 1) * pct / 100))
    return values[index]


def run_stage(args, vus, duration):
    print(f"\nPreparing {vus} virtual users...")
    with ThreadPoolExecutor(max_workers=min(vus, args.prepare_workers)) as executor:
        futures = [executor.submit(get_initial_refresh_token, args, vu_id) for vu_id in range(vus)]
        refresh_tokens = [future.result() for future in as_completed(futures)]

    print(f"Running refresh-token load: vus={vus}, duration={duration}s")
    stop_at = time.monotonic() + duration

    with ThreadPoolExecutor(max_workers=vus) as executor:
        futures = [
            executor.submit(refresh_loop, args, vu_id, refresh_tokens[vu_id], stop_at)
            for vu_id in range(vus)
        ]

        successes = 0
        failures = 0
        latencies = []
        errors = {}

        for future in as_completed(futures):
            worker_successes, worker_failures, worker_latencies, worker_errors = future.result()
            successes += worker_successes
            failures += worker_failures
            latencies.extend(worker_latencies)
            for key, value in worker_errors.items():
                errors[key] = errors.get(key, 0) + value

    total = successes + failures
    rps = total / duration
    success_rps = successes / duration
    failure_rate = failures / total * 100 if total else 0

    result = {
        "vus": vus,
        "duration_seconds": duration,
        "requests": total,
        "successes": successes,
        "failures": failures,
        "rps": round(rps, 2),
        "success_rps": round(success_rps, 2),
        "failure_rate_percent": round(failure_rate, 2),
        "latency_ms_min": round(min(latencies), 2) if latencies else 0,
        "latency_ms_avg": round(statistics.mean(latencies), 2) if latencies else 0,
        "latency_ms_p50": round(percentile(latencies, 50), 2),
        "latency_ms_p95": round(percentile(latencies, 95), 2),
        "latency_ms_p99": round(percentile(latencies, 99), 2),
        "top_errors": sorted(errors.items(), key=lambda item: item[1], reverse=True)[:5],
    }

    print(json.dumps(result, indent=2))
    return result


def parse_stage(stage):
    vus, duration = stage.lower().split("x", 1)
    return int(vus), int(duration)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--base-url", default="http://localhost:5058")
    parser.add_argument("--forwarded-host", default="localhost:5058")
    parser.add_argument("--client-id", default="debug-client")
    parser.add_argument("--redirect-uri", default="http://localhost:5058/debug/callback")
    parser.add_argument("--scope", default="openid profile email offline_access orders.read")
    parser.add_argument("--email", default="docker-admin@example.com")
    parser.add_argument("--password", default="Admin123!Admin123!")
    parser.add_argument("--prepare-workers", type=int, default=20)
    parser.add_argument("--stage", action="append",
                        help="Stage format: VUSxSECONDS. Can be repeated.")
    args = parser.parse_args()

    if not args.stage:
        args.stage = ["10x20", "25x20", "50x20"]

    results = [run_stage(args, *parse_stage(stage)) for stage in args.stage]
    print("\nSummary")
    print(json.dumps(results, indent=2))


if __name__ == "__main__":
    main()
