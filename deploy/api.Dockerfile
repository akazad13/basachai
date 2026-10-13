# The API image. Build from the repository root:
#   docker build -f deploy/api.Dockerfile -t basachai-api:local .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Restore first so package downloads are cached until a project file changes.
COPY src/BasaChai.Domain/BasaChai.Domain.csproj src/BasaChai.Domain/
COPY src/BasaChai.Application/BasaChai.Application.csproj src/BasaChai.Application/
COPY src/BasaChai.Infrastructure/BasaChai.Infrastructure.csproj src/BasaChai.Infrastructure/
COPY src/BasaChai.Api/BasaChai.Api.csproj src/BasaChai.Api/
RUN dotnet restore src/BasaChai.Api/BasaChai.Api.csproj
COPY src/ src/
RUN dotnet publish src/BasaChai.Api/BasaChai.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
# curl is only for the container health check.
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
# The pilot dataset, used only by Staging's test hooks (never loaded in Production).
COPY deploy/seed/pilot.json deploy/seed/pilot.json
# The base image already listens on 8080 (ASPNETCORE_HTTP_PORTS).
ENV DOTNET_gcServer=0 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DataProtection__KeyPath=/keys
RUN mkdir -p /keys && chown app:app /keys
USER app
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=3 CMD curl -fsS http://localhost:8080/health/ready || exit 1
ENTRYPOINT ["dotnet", "BasaChai.Api.dll"]
