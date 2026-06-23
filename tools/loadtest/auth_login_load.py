#!/usr/bin/env python3
import argparse
import json
import statistics
import time
from types import SimpleNamespace
from concurrent.futures import ThreadPoolExecutor, as_completed

from auth_refresh_load import get_initial_refresh_token


def percentile(values, pct):
    if not values:
        return 0

    values = sorted(values)
    index = int(round((len(values) - 1) * pct / 100))
    return values[index]


def user_email(args, index):
    return f"{args.prefix}{index:06d}@{args.domain}"


def login_worker(args, worker_id, stop_at):
    successes = 0
    failures = 0
    latencies_ms = []
    errors = {}
    next_user = args.user_start + worker_id

    while time.monotonic() < stop_at:
        started = time.perf_counter()

        try:
            request_args = SimpleNamespace(**vars(args))
            request_args.email = user_email(args, next_user % args.user_count)
            get_initial_refresh_token(request_args, next_user)
            successes += 1
        except Exception as exception:
            failures += 1
            key = type(exception).__name__ + ": " + str(exception)
            errors[key] = errors.get(key, 0) + 1
        finally:
            latencies_ms.append((time.perf_counter() - started) * 1000)
            next_user += args.vus

    return successes, failures, latencies_ms, errors


def run_stage(args, vus, duration):
    print(f"\nRunning full login/code/token load: vus={vus}, duration={duration}s")
    args.vus = vus
    stop_at = time.monotonic() + duration

    with ThreadPoolExecutor(max_workers=vus) as executor:
        futures = [
            executor.submit(login_worker, args, worker_id, stop_at)
            for worker_id in range(vus)
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
    result = {
        "vus": vus,
        "duration_seconds": duration,
        "flows": total,
        "successes": successes,
        "failures": failures,
        "flows_per_second": round(total / duration, 2),
        "success_flows_per_second": round(successes / duration, 2),
        "failure_rate_percent": round(failures / total * 100, 2) if total else 0,
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
    parser.add_argument("--prefix", default="loaduser")
    parser.add_argument("--domain", default="example.test")
    parser.add_argument("--password", default="Load123!Load123!")
    parser.add_argument("--user-start", type=int, default=0)
    parser.add_argument("--user-count", type=int, default=100_000)
    parser.add_argument("--stage", action="append",
                        help="Stage format: VUSxSECONDS. Can be repeated.")
    args = parser.parse_args()

    if not args.stage:
        args.stage = ["5x20", "10x20", "25x20"]

    results = [run_stage(args, *parse_stage(stage)) for stage in args.stage]
    print("\nSummary")
    print(json.dumps(results, indent=2))


if __name__ == "__main__":
    main()
