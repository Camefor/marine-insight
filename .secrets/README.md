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

In Docker, mount the key via the key-per-file pattern: a file whose name is `AI__ApiKey` under `/run/secrets`, or set the `AI__ApiKey` environment variable.

For the Tianditu map (browser-side key), run `scripts/configure-tianditu-secret.ps1` to store `Map:Tianditu:Key` in .NET User Secrets; pass `-Disable` to remove it. In Docker, create `.secrets/tianditu_key` containing only the key, then start Compose with `-f compose.tianditu.yaml`:

```powershell
docker compose -f compose.yaml -f compose.production.yaml -f compose.tianditu.yaml up -d --build
```

Even though the Tianditu key is a browser-side key that appears in tile URLs, it is still kept out of `appsettings.json` per the project secret convention; without it the map picker degrades to coordinate input.

Prefer an external secret manager or a host file readable only by the deployment account in staging and production.
