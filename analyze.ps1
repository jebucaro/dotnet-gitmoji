#Requires -Version 7.3
# PowerShell equivalent of analyze.sh. Requires a running local SonarQube (docker-compose up).

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

$envFile = Join-Path $PSScriptRoot '.env'

if (-not (Test-Path $envFile)) {
    Write-Host "Error: .env file not found at $envFile"
    Write-Host 'Copy .env.example to .env and fill in your SONAR_TOKEN.'
    exit 1
}

foreach ($line in Get-Content $envFile) {
    $trimmed = $line.Trim()
    if ($trimmed -eq '' -or $trimmed.StartsWith('#')) { continue }
    $name, $value = $trimmed -split '=', 2
    if ($null -eq $value) { continue }
    [Environment]::SetEnvironmentVariable($name.Trim(), $value.Trim().Trim('"').Trim("'"))
}

if (-not $env:SONAR_TOKEN) {
    Write-Host 'Error: SONAR_TOKEN is not set in .env'
    exit 1
}

Push-Location $PSScriptRoot
try {
    # Skip the Husky hook-install MSBuild target during restore/build; this is an analysis run, not a commit workflow.
    $env:HUSKY = '0'

    dotnet tool restore

    if (Test-Path 'tests/DotnetGitmoji.Tests/TestResults') {
        Remove-Item -Recurse -Force 'tests/DotnetGitmoji.Tests/TestResults'
    }

    dotnet tool run dotnet-sonarscanner begin `
        /k:"dotnet-gitmoji" `
        /d:sonar.host.url="http://localhost:9000" `
        /d:sonar.token="$env:SONAR_TOKEN" `
        /d:sonar.cs.opencover.reportsPaths="tests/**/coverage.opencover.xml" `
        /d:sonar.exclusions="**/obj/**,**/bin/**"

    dotnet build --no-incremental --disable-build-servers

    dotnet test --collect:"XPlat Code Coverage" --settings coverage.runsettings

    dotnet tool run dotnet-sonarscanner end /d:sonar.token="$env:SONAR_TOKEN"
}
finally {
    Pop-Location
}
