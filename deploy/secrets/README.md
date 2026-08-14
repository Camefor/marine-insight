# Container Secret Templates

The two `*.example` files are local-only development placeholders. Before deployment, create secret files outside the repository and point these variables at them:

- `MARINE_INSIGHT_POSTGRES_PASSWORD_FILE`
- `MARINE_INSIGHT_DB_CONNECTION_SECRET_FILE`

The PostgreSQL password in both files must match. Never use the checked-in example value outside a disposable local environment.

WorldTides is opt-in through `compose.worldtides.yaml`. Store its API key outside the repository and set `MARINE_INSIGHT_WORLDTIDES_SECRET_FILE` to that file, or use the ignored `.secrets/worldtides_api_key` path for local Compose only. The file must contain only the key. Do not add a real WorldTides secret or a realistic placeholder under `deploy/secrets/`.

The Tianditu map key is opt-in through `compose.tianditu.yaml`. Store its browser key outside the repository and set `MARINE_INSIGHT_TIANDITU_SECRET_FILE` to that file, or use the ignored `.secrets/tianditu_key` path for local Compose only. The file must contain only the key; it must never be committed to `appsettings.json`.
