using Microsoft.AspNetCore.Mvc;

namespace AuthService.Api.Controllers;

public sealed class DebugController : Controller
{
    [HttpGet("~/")]
    public IActionResult Home()
    {
        return Redirect("/demo");
    }

    [HttpGet("~/demo")]
    public IActionResult Demo()
    {
        return Content(DemoHtml, "text/html; charset=utf-8");
    }

    [HttpGet("~/debug/callback")]
    public IActionResult Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? iss,
        [FromQuery] string? error,
        [FromQuery] string? error_description
        )
    {
        return Content(CallbackHtml, "text/html; charset=utf-8");
    }

    [HttpGet("~/debug/logout-callback")]
    public IActionResult LogoutCallback()
    {
        return Content(LogoutHtml, "text/html; charset=utf-8");
    }

    private const string DemoHtml =
        """
        <!doctype html>
        <html lang="en">
        <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>AuthService Demo</title>
            <style>
                :root {
                    color-scheme: light;
                    --bg: #f6f7f9;
                    --panel: #ffffff;
                    --text: #17202a;
                    --muted: #667085;
                    --line: #d9dee7;
                    --accent: #126f5b;
                    --accent-2: #0b4f9f;
                }
                * { box-sizing: border-box; }
                body {
                    margin: 0;
                    min-height: 100vh;
                    font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
                    color: var(--text);
                    background: var(--bg);
                }
                main {
                    max-width: 1040px;
                    margin: 0 auto;
                    padding: 48px 24px;
                }
                .topline {
                    display: flex;
                    align-items: center;
                    justify-content: space-between;
                    gap: 16px;
                    margin-bottom: 28px;
                }
                .brand {
                    display: flex;
                    align-items: center;
                    gap: 12px;
                    font-weight: 700;
                    letter-spacing: 0;
                }
                .mark {
                    width: 40px;
                    height: 40px;
                    border-radius: 8px;
                    display: grid;
                    place-items: center;
                    background: var(--accent);
                    color: white;
                    font-weight: 800;
                }
                .status {
                    border: 1px solid #badbcc;
                    background: #ecf8f1;
                    color: #116149;
                    padding: 8px 12px;
                    border-radius: 999px;
                    font-size: 14px;
                    font-weight: 650;
                }
                h1 {
                    max-width: 820px;
                    margin: 0 0 12px;
                    font-size: clamp(34px, 5vw, 58px);
                    line-height: 1.02;
                    letter-spacing: 0;
                }
                .lead {
                    max-width: 760px;
                    margin: 0 0 32px;
                    color: var(--muted);
                    font-size: 18px;
                    line-height: 1.55;
                }
                .grid {
                    display: grid;
                    grid-template-columns: minmax(0, 1fr) 360px;
                    gap: 20px;
                    align-items: start;
                }
                .panel {
                    background: var(--panel);
                    border: 1px solid var(--line);
                    border-radius: 8px;
                    padding: 22px;
                    box-shadow: 0 12px 28px rgba(16, 24, 40, .06);
                }
                .flow {
                    display: grid;
                    gap: 12px;
                }
                .step {
                    display: grid;
                    grid-template-columns: 34px 1fr;
                    gap: 12px;
                    align-items: start;
                    padding: 14px;
                    border: 1px solid var(--line);
                    border-radius: 8px;
                    background: #fbfcfe;
                }
                .num {
                    width: 34px;
                    height: 34px;
                    border-radius: 8px;
                    display: grid;
                    place-items: center;
                    background: #e8f1fb;
                    color: var(--accent-2);
                    font-weight: 800;
                }
                .step strong { display: block; margin-bottom: 3px; }
                .step span { color: var(--muted); line-height: 1.45; }
                button {
                    width: 100%;
                    min-height: 48px;
                    border: 0;
                    border-radius: 8px;
                    background: var(--accent);
                    color: white;
                    font-size: 16px;
                    font-weight: 750;
                    cursor: pointer;
                }
                button:hover { background: #0f604e; }
                .meta {
                    margin-top: 16px;
                    display: grid;
                    gap: 10px;
                    color: var(--muted);
                    font-size: 14px;
                }
                code {
                    display: block;
                    overflow-wrap: anywhere;
                    padding: 10px;
                    border-radius: 8px;
                    background: #f1f3f6;
                    color: #283444;
                    font-size: 13px;
                }
                @media (max-width: 820px) {
                    main { padding: 28px 16px; }
                    .grid { grid-template-columns: 1fr; }
                    .topline { align-items: flex-start; flex-direction: column; }
                }
            </style>
        </head>
        <body>
            <main>
                <div class="topline">
                    <div class="brand"><div class="mark">A</div><span>AuthService</span></div>
                    <div class="status">Docker staging is ready</div>
                </div>
                <h1>Visual OAuth login demo</h1>
                <p class="lead">
                    This page starts the same Authorization Code + PKCE flow used by real mobile and web clients.
                    After sign in, the callback page exchanges the authorization code for tokens and shows the verified result.
                </p>
                <div class="grid">
                    <section class="panel flow" aria-label="Authentication flow">
                        <div class="step"><div class="num">1</div><div><strong>Client creates PKCE challenge</strong><span>The browser creates a code verifier and sends only its SHA-256 challenge to AuthService.</span></div></div>
                        <div class="step"><div class="num">2</div><div><strong>User signs in</strong><span>AuthService authenticates the user with ASP.NET Core Identity and writes audit/session data.</span></div></div>
                        <div class="step"><div class="num">3</div><div><strong>AuthService returns authorization code</strong><span>The code is short-lived and can be exchanged only with the original PKCE verifier.</span></div></div>
                        <div class="step"><div class="num">4</div><div><strong>Callback receives tokens</strong><span>The demo exchanges the code for access, identity and refresh tokens.</span></div></div>
                    </section>
                    <aside class="panel">
                        <button id="start">Sign in with AuthService</button>
                        <div class="meta">
                            <div>Client</div>
                            <code>demo-client</code>
                            <div>Redirect URI</div>
                            <code id="redirectUri"></code>
                            <div>Scopes</div>
                            <code>openid profile email offline_access orders.read</code>
                        </div>
                    </aside>
                </div>
            </main>
            <script>
                const clientId = "demo-client";
                const scope = "openid profile email offline_access orders.read";
                const redirectUri = `${window.location.origin}/debug/callback`;
                document.getElementById("redirectUri").textContent = redirectUri;

                function base64Url(buffer) {
                    return btoa(String.fromCharCode(...new Uint8Array(buffer)))
                        .replace(/\+/g, "-")
                        .replace(/\//g, "_")
                        .replace(/=+$/g, "");
                }

                async function sha256(value) {
                    return crypto.subtle.digest("SHA-256", new TextEncoder().encode(value));
                }

                function randomValue(bytes) {
                    const data = new Uint8Array(bytes);
                    crypto.getRandomValues(data);
                    return base64Url(data);
                }

                document.getElementById("start").addEventListener("click", async () => {
                    const verifier = randomValue(32);
                    const challenge = base64Url(await sha256(verifier));
                    const state = randomValue(18);

                    sessionStorage.setItem("demo.pkce.verifier", verifier);
                    sessionStorage.setItem("demo.oauth.state", state);

                    const query = new URLSearchParams({
                        client_id: clientId,
                        response_type: "code",
                        scope,
                        redirect_uri: redirectUri,
                        code_challenge: challenge,
                        code_challenge_method: "S256",
                        state
                    });

                    window.location.href = `/connect/authorize?${query}`;
                });
            </script>
        </body>
        </html>
        """;

    private const string CallbackHtml =
        """
        <!doctype html>
        <html lang="en">
        <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>AuthService Login Result</title>
            <style>
                :root {
                    color-scheme: light;
                    --bg: #f6f7f9;
                    --panel: #ffffff;
                    --text: #17202a;
                    --muted: #667085;
                    --line: #d9dee7;
                    --ok: #126f5b;
                    --bad: #b42318;
                    --blue: #0b4f9f;
                }
                * { box-sizing: border-box; }
                body {
                    margin: 0;
                    min-height: 100vh;
                    font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
                    color: var(--text);
                    background: var(--bg);
                }
                main {
                    max-width: 1080px;
                    margin: 0 auto;
                    padding: 40px 24px;
                }
                .banner {
                    display: grid;
                    grid-template-columns: 56px 1fr;
                    gap: 16px;
                    align-items: center;
                    background: var(--panel);
                    border: 1px solid var(--line);
                    border-radius: 8px;
                    padding: 22px;
                    box-shadow: 0 12px 28px rgba(16, 24, 40, .06);
                    margin-bottom: 20px;
                }
                .icon {
                    width: 56px;
                    height: 56px;
                    border-radius: 8px;
                    display: grid;
                    place-items: center;
                    background: #ecf8f1;
                    color: var(--ok);
                    font-size: 30px;
                    font-weight: 900;
                }
                .icon.error {
                    background: #fff1f0;
                    color: var(--bad);
                }
                h1 { margin: 0 0 6px; font-size: clamp(28px, 4vw, 44px); letter-spacing: 0; }
                p { margin: 0; color: var(--muted); line-height: 1.5; }
                .grid {
                    display: grid;
                    grid-template-columns: repeat(2, minmax(0, 1fr));
                    gap: 16px;
                }
                .panel {
                    background: var(--panel);
                    border: 1px solid var(--line);
                    border-radius: 8px;
                    padding: 18px;
                }
                .wide { grid-column: 1 / -1; }
                .label {
                    color: var(--muted);
                    font-size: 13px;
                    font-weight: 700;
                    text-transform: uppercase;
                    margin-bottom: 8px;
                }
                code, pre {
                    display: block;
                    overflow-wrap: anywhere;
                    white-space: pre-wrap;
                    margin: 0;
                    padding: 12px;
                    border-radius: 8px;
                    background: #f1f3f6;
                    color: #283444;
                    font-size: 13px;
                    line-height: 1.45;
                }
                .actions {
                    display: flex;
                    gap: 10px;
                    margin-top: 18px;
                    flex-wrap: wrap;
                }
                a.button {
                    min-height: 42px;
                    display: inline-flex;
                    align-items: center;
                    justify-content: center;
                    padding: 0 14px;
                    border-radius: 8px;
                    background: var(--blue);
                    color: white;
                    text-decoration: none;
                    font-weight: 750;
                }
                a.secondary {
                    background: white;
                    color: var(--text);
                    border: 1px solid var(--line);
                }
                @media (max-width: 820px) {
                    main { padding: 24px 16px; }
                    .banner { grid-template-columns: 1fr; }
                    .grid { grid-template-columns: 1fr; }
                }
            </style>
        </head>
        <body>
            <main>
                <section class="banner">
                    <div id="icon" class="icon">✓</div>
                    <div>
                        <h1 id="title">Completing login...</h1>
                        <p id="subtitle">The callback received an authorization response from AuthService.</p>
                        <div class="actions">
                            <a class="button" href="/demo">Run again</a>
                            <a class="button secondary" href="/account/logout">Logout</a>
                        </div>
                    </div>
                </section>
                <section class="grid">
                    <div class="panel">
                        <div class="label">Authorization code</div>
                        <code id="code"></code>
                    </div>
                    <div class="panel">
                        <div class="label">Issuer</div>
                        <code id="issuer"></code>
                    </div>
                    <div class="panel">
                        <div class="label">Token response</div>
                        <pre id="token"></pre>
                    </div>
                    <div class="panel">
                        <div class="label">Identity token claims</div>
                        <pre id="claims"></pre>
                    </div>
                    <div class="panel wide">
                        <div class="label">Access token preview</div>
                        <code id="access"></code>
                    </div>
                </section>
            </main>
            <script>
                const params = new URLSearchParams(window.location.search);
                const code = params.get("code");
                const state = params.get("state");
                const issuer = params.get("iss") || "(issuer was not returned)";
                const error = params.get("error");
                const errorDescription = params.get("error_description");
                const expectedState = sessionStorage.getItem("demo.oauth.state");
                const verifier = sessionStorage.getItem("demo.pkce.verifier");

                const title = document.getElementById("title");
                const subtitle = document.getElementById("subtitle");
                const icon = document.getElementById("icon");

                document.getElementById("code").textContent = code || "(missing)";
                document.getElementById("issuer").textContent = issuer;

                function fail(message) {
                    title.textContent = "Login failed";
                    subtitle.textContent = message;
                    icon.textContent = "!";
                    icon.classList.add("error");
                }

                function decodeJwtPayload(jwt) {
                    const parts = jwt.split(".");
                    if (parts.length < 2) {
                        return null;
                    }

                    const base64 = parts[1].replace(/-/g, "+").replace(/_/g, "/");
                    const padded = base64 + "=".repeat((4 - base64.length % 4) % 4);
                    return JSON.parse(decodeURIComponent(escape(atob(padded))));
                }

                async function exchangeCode() {
                    if (error) {
                        fail(`${error}: ${errorDescription || "authorization server returned an error"}`);
                        return;
                    }

                    if (!code) {
                        fail("Authorization code is missing.");
                        return;
                    }

                    if (!verifier) {
                        fail("PKCE verifier is missing. Start the flow from /demo again.");
                        return;
                    }

                    if (expectedState && state !== expectedState) {
                        fail("OAuth state mismatch.");
                        return;
                    }

                    const body = new URLSearchParams({
                        grant_type: "authorization_code",
                        client_id: "demo-client",
                        redirect_uri: `${window.location.origin}/debug/callback`,
                        code,
                        code_verifier: verifier
                    });

                    const response = await fetch("/connect/token", {
                        method: "POST",
                        headers: { "Content-Type": "application/x-www-form-urlencoded" },
                        body
                    });

                    const payload = await response.json();

                    if (!response.ok) {
                        fail(payload.error_description || payload.error || "Token exchange failed.");
                        document.getElementById("token").textContent = JSON.stringify(payload, null, 2);
                        return;
                    }

                    title.textContent = "Login completed successfully";
                    subtitle.textContent = "AuthService issued tokens for the signed-in user.";

                    document.getElementById("token").textContent = JSON.stringify({
                        token_type: payload.token_type,
                        expires_in: payload.expires_in,
                        scope: payload.scope,
                        has_access_token: Boolean(payload.access_token),
                        has_id_token: Boolean(payload.id_token),
                        has_refresh_token: Boolean(payload.refresh_token)
                    }, null, 2);

                    document.getElementById("claims").textContent = JSON.stringify(decodeJwtPayload(payload.id_token), null, 2);
                    document.getElementById("access").textContent = `${payload.access_token.slice(0, 96)}...`;

                    sessionStorage.removeItem("demo.pkce.verifier");
                    sessionStorage.removeItem("demo.oauth.state");
                }

                exchangeCode().catch(error => fail(error.message));
            </script>
        </body>
        </html>
        """;

    private const string LogoutHtml =
        """
        <!doctype html>
        <html lang="en">
        <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Logged out</title>
            <style>
                body {
                    margin: 0;
                    min-height: 100vh;
                    display: grid;
                    place-items: center;
                    font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
                    background: #f6f7f9;
                    color: #17202a;
                }
                section {
                    width: min(560px, calc(100vw - 32px));
                    background: white;
                    border: 1px solid #d9dee7;
                    border-radius: 8px;
                    padding: 24px;
                    box-shadow: 0 12px 28px rgba(16, 24, 40, .06);
                }
                h1 { margin: 0 0 8px; letter-spacing: 0; }
                p { color: #667085; line-height: 1.5; }
                a {
                    min-height: 42px;
                    display: inline-flex;
                    align-items: center;
                    justify-content: center;
                    padding: 0 14px;
                    border-radius: 8px;
                    background: #0b4f9f;
                    color: white;
                    text-decoration: none;
                    font-weight: 750;
                }
            </style>
        </head>
        <body>
            <section>
                <h1>Logged out</h1>
                <p>The local AuthService session was ended.</p>
                <a href="/demo">Open demo</a>
            </section>
        </body>
        </html>
        """;
}
