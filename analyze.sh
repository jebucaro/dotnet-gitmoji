#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="$SCRIPT_DIR/.env"

if [[ -f "$ENV_FILE" ]]; then
  set -a
  # shellcheck source=.env
  source "$ENV_FILE"
  set +a
else
  echo "Error: .env file not found at $ENV_FILE"
  echo "Copy .env.example to .env and fill in your SONAR_TOKEN."
  exit 1
fi

if [[ -z "${SONAR_TOKEN:-}" ]]; then
  echo "Error: SONAR_TOKEN is not set in .env"
  exit 1
fi

dotnet tool restore

dotnet tool run dotnet-sonarscanner begin \
  /k:"dotnet-gitmoji" \
  /d:sonar.host.url="http://localhost:9000" \
  /d:sonar.token="$SONAR_TOKEN" \
  /d:sonar.cs.opencover.reportsPaths="tests/**/coverage.opencover.xml" \
  /d:sonar.exclusions="**/obj/**,**/bin/**"

dotnet build --no-incremental --disable-build-servers

rm -rf tests/DotnetGitmoji.Tests/TestResults/

dotnet test --collect:"XPlat Code Coverage" --settings coverage.runsettings

dotnet tool run dotnet-sonarscanner end /d:sonar.token="$SONAR_TOKEN"
