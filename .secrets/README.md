# Local Secret Files

This directory is ignored except for this guide. Never commit real credentials.

For direct local Web execution, run `scripts/configure-worldtides-secret.ps1`. It prompts without echoing the key and pipes it to .NET User Secrets, avoiding plaintext shell history. Run it again to rotate the key, or pass `-Disable` to remove the key and disable the provider.

For the optional WorldTides Compose profile, create `.secrets/worldtides_api_key` containing only the API key, with no quotes or variable name. Then start Compose with both files:

```powershell
docker compose -f compose.yaml -f compose.worldtides.yaml up -d --build
```

For a secret stored outside the repository, point Compose at its absolute path:

```powershell
$env:MARINE_INSIGHT_WORLDTIDES_SECRET_FILE = 'D:\secure\marine-insight\worldtides_api_key'
docker compose -f compose.yaml -f compose.worldtides.yaml up -d --build
```

For the optional AI explanation engine (OpenAI-compatible), run `scripts/configure-ai-secret.ps1`. It stores `AI:ApiKey` and enables `AI:Enabled` in .NET User Secrets; pass `-Disable` to remove the key and turn the provider off. The AI provider is disabled by default and never required for the deterministic rule-template explanation.

In Docker, create `.secrets/ai_api_key` containing only the key, then start Compose with the AI overlay (`compose.ai.yaml` mounts it as `AI__ApiKey` under `/run/secrets`):

```powershell
docker compose -f compose.yaml -f compose.production.yaml -f compose.ai.yaml up -d --build
```

For the Tianditu map (browser-side key), run `scripts/configure-tianditu-secret.ps1` to store `Map:Tianditu:Key` in .NET User Secrets; pass `-Disable` to remove it. In Docker, create `.secrets/tianditu_key` containing only the key, then start Compose with `-f compose.tianditu.yaml`:

```powershell
docker compose -f compose.yaml -f compose.production.yaml -f compose.tianditu.yaml up -d --build
```

Even though the Tianditu key is a browser-side key that appears in tile URLs, it is still kept out of `appsettings.json` per the project secret convention; without it the map picker degrades to coordinate input.

Since `MI-0059`, WorldTides API keys can be managed directly in the admin backend (`/admin/providers/worldtides`): add multiple candidate keys, test the connection, set the active key and delete keys. Keys are encrypted with ASP.NET Core DataProtection and stored in the database; only the last-4-digit hint is ever displayed, and plaintext never lands in the repo, `.secrets/`, or logs. The local/Compose secret files below remain only as the startup fallback when no database credential exists, and the fallback key never reports health. Keys saved via the backend take precedence over the file fallback and take effect at runtime without a restart.

Prefer an external secret manager or a host file readable only by the deployment account in staging and production.
