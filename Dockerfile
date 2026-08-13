# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY MarineInsight.slnx dotnet-tools.json ./
COPY src/MarineInsight.Domain/MarineInsight.Domain.csproj src/MarineInsight.Domain/
COPY src/MarineInsight.Application/MarineInsight.Application.csproj src/MarineInsight.Application/
COPY src/MarineInsight.Infrastructure/MarineInsight.Infrastructure.csproj src/MarineInsight.Infrastructure/
COPY src/MarineInsight.Migrations.PostgreSql/MarineInsight.Migrations.PostgreSql.csproj src/MarineInsight.Migrations.PostgreSql/
COPY src/MarineInsight.Web/MarineInsight.Web.csproj src/MarineInsight.Web/
RUN dotnet restore src/MarineInsight.Web/MarineInsight.Web.csproj

COPY src ./src
RUN dotnet publish src/MarineInsight.Web/MarineInsight.Web.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /var/lib/marine-insight/keys \
    && chown -R $APP_UID:$APP_UID /var/lib/marine-insight
WORKDIR /app
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080
USER $APP_UID
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl --fail --silent --show-error http://127.0.0.1:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "MarineInsight.Web.dll"]
