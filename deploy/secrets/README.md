# Container Secret Templates

The two `*.example` files are local-only development placeholders. Before deployment, create secret files outside the repository and point these variables at them:

- `MARINE_INSIGHT_POSTGRES_PASSWORD_FILE`
- `MARINE_INSIGHT_DB_CONNECTION_SECRET_FILE`

The PostgreSQL password in both files must match. Never use the checked-in example value outside a disposable local environment.
