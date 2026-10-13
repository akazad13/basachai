# Starts PostgreSQL 17 in Docker (port 5433) and runs the API on http://localhost:5080 in the Test environment,
# so the /v1/test hooks used by the E2E suite are available. Reads secrets from .dev.env (git-ignored).
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Get-Content (Join-Path $root '.dev.env') | Where-Object { $_ -match '^﻿?[A-Za-z0-9_]+=' } | ForEach-Object {
  $name, $value = $_ -split '=', 2
  Set-Item "env:$name" $value
}

$running = docker ps --filter 'name=^basachai-db$' --format '{{.Names}}'
if (-not $running) {
  $exists = docker ps -a --filter 'name=^basachai-db$' --format '{{.Names}}'
  if ($exists) { docker start basachai-db | Out-Null }
  else {
    docker run -d --name basachai-db -p 5433:5432 -e POSTGRES_USER=basachai -e POSTGRES_DB=basachai_test `
      -e "POSTGRES_PASSWORD=$env:POSTGRES_PASSWORD" postgres:17 | Out-Null
  }
  Start-Sleep -Seconds 4
}

$env:ASPNETCORE_ENVIRONMENT = 'Test'
$env:ASPNETCORE_URLS = 'http://localhost:5080'
$env:Auth__TestTotpBypass = 'true'
$env:Web__Origin = 'http://localhost:3000'
$env:Web__RevalidateUrl = 'http://localhost:3000/api/revalidate'   # purges the web app's cached pages
dotnet run --project (Join-Path $root 'src/BasaChai.Api') --no-launch-profile
