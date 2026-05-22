namespace CustomOAuthServer.Api;

internal static class AccountLoginPage
{
    public static string Render(string encodedReturnUrl, string? errorMessage = null, string encodedUsername = "")
    {
        var errorHtml = errorMessage is null
            ? ""
            : $"""
              <div class="alert" role="alert">{System.Net.WebUtility.HtmlEncode(errorMessage)}</div>
              """;

        return $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1" />
          <title>Sign in · CustomOAuthServer</title>
          <style>
            :root {
              color-scheme: light dark;
              --bg: #f4f6f9;
              --bg-accent: #e8eef7;
              --surface: #ffffff;
              --text: #0f172a;
              --text-muted: #64748b;
              --border: #e2e8f0;
              --primary: #2563eb;
              --primary-hover: #1d4ed8;
              --focus-ring: rgba(37, 99, 235, 0.35);
              --shadow: 0 10px 40px rgba(15, 23, 42, 0.08);
            }

            @media (prefers-color-scheme: dark) {
              :root {
                --bg: #0b1120;
                --bg-accent: #111827;
                --surface: #111827;
                --text: #f8fafc;
                --text-muted: #94a3b8;
                --border: #1e293b;
                --primary: #3b82f6;
                --primary-hover: #60a5fa;
                --focus-ring: rgba(59, 130, 246, 0.4);
                --shadow: 0 10px 40px rgba(0, 0, 0, 0.45);
              }
            }

            * { box-sizing: border-box; }

            body {
              margin: 0;
              min-height: 100vh;
              display: flex;
              align-items: center;
              justify-content: center;
              padding: 1.5rem;
              font-family: system-ui, -apple-system, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
              font-size: 1rem;
              line-height: 1.5;
              color: var(--text);
              background:
                radial-gradient(ellipse 80% 50% at 50% -20%, var(--bg-accent), transparent),
                var(--bg);
            }

            .card {
              width: 100%;
              max-width: 24rem;
              background: var(--surface);
              border: 1px solid var(--border);
              border-radius: 0.75rem;
              box-shadow: var(--shadow);
              padding: 2rem;
            }

            .brand {
              display: flex;
              align-items: center;
              gap: 0.625rem;
              margin-bottom: 1.75rem;
            }

            .brand-icon {
              width: 2.25rem;
              height: 2.25rem;
              border-radius: 0.5rem;
              background: linear-gradient(135deg, var(--primary), #7c3aed);
              display: flex;
              align-items: center;
              justify-content: center;
              flex-shrink: 0;
            }

            .brand-icon svg {
              width: 1.25rem;
              height: 1.25rem;
              fill: #fff;
            }

            .brand-name {
              font-size: 0.9375rem;
              font-weight: 600;
              letter-spacing: -0.01em;
            }

            h1 {
              margin: 0 0 0.375rem;
              font-size: 1.5rem;
              font-weight: 700;
              letter-spacing: -0.02em;
            }

            .subtitle {
              margin: 0 0 1.5rem;
              font-size: 0.875rem;
              color: var(--text-muted);
            }

            .alert {
              margin: 0 0 1.25rem;
              padding: 0.75rem 0.875rem;
              font-size: 0.875rem;
              color: #991b1b;
              background: #fef2f2;
              border: 1px solid #fecaca;
              border-radius: 0.5rem;
            }

            @media (prefers-color-scheme: dark) {
              .alert {
                color: #fecaca;
                background: #450a0a;
                border-color: #7f1d1d;
              }
            }

            form { display: flex; flex-direction: column; gap: 1.125rem; }

            .field { display: flex; flex-direction: column; gap: 0.375rem; }

            label {
              font-size: 0.8125rem;
              font-weight: 500;
              color: var(--text-muted);
            }

            input[type="text"],
            input[type="password"] {
              width: 100%;
              padding: 0.625rem 0.75rem;
              font: inherit;
              color: var(--text);
              background: var(--bg);
              border: 1px solid var(--border);
              border-radius: 0.5rem;
              transition: border-color 0.15s, box-shadow 0.15s;
            }

            input[type="text"]:focus,
            input[type="password"]:focus {
              outline: none;
              border-color: var(--primary);
              box-shadow: 0 0 0 3px var(--focus-ring);
            }

            button[type="submit"] {
              margin-top: 0.25rem;
              width: 100%;
              padding: 0.6875rem 1rem;
              font: inherit;
              font-weight: 600;
              color: #fff;
              background: var(--primary);
              border: none;
              border-radius: 0.5rem;
              cursor: pointer;
              transition: background 0.15s;
            }

            button[type="submit"]:hover { background: var(--primary-hover); }

            button[type="submit"]:focus-visible {
              outline: none;
              box-shadow: 0 0 0 3px var(--focus-ring);
            }

            .footer {
              margin-top: 1.5rem;
              padding-top: 1.25rem;
              border-top: 1px solid var(--border);
              font-size: 0.75rem;
              color: var(--text-muted);
              text-align: center;
            }
          </style>
        </head>
        <body>
          <main class="card">
            <div class="brand">
              <div class="brand-icon" aria-hidden="true">
                <svg viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
                  <path d="M12 2C9.24 2 7 4.24 7 7v1H6a2 2 0 0 0-2 2v10a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V10a2 2 0 0 0-2-2h-1V7c0-2.76-2.24-5-5-5zm0 2a3 3 0 0 1 3 3v1H9V7a3 3 0 0 1 3-3z"/>
                </svg>
              </div>
              <span class="brand-name">CustomOAuthServer</span>
            </div>
            <h1>Sign in</h1>
            <p class="subtitle">Use your account to continue to the application.</p>
            {{errorHtml}}
            <form method="post" action="/account/login">
              <input type="hidden" name="returnUrl" value="{{encodedReturnUrl}}" />
              <div class="field">
                <label for="username">Username</label>
                <input id="username" name="username" type="text" autocomplete="username" value="{{encodedUsername}}" required autofocus />
              </div>
              <div class="field">
                <label for="password">Password</label>
                <input id="password" name="password" type="password" autocomplete="current-password" required />
              </div>
              <button type="submit">Sign in</button>
            </form>
            <p class="footer">Secured sign-in for OAuth authorization</p>
          </main>
        </body>
        </html>
        """;
    }
}
