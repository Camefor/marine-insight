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

Prefer an external secret manager or a host file readable only by the deployment account in staging and production.
