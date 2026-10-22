$ErrorActionPreference = "Stop"
Push-Location $PSScriptRoot
$previousConnectionString = $env:ConnectionStrings__LibraryDatabase

try {
    if (-not (Test-Path ".env")) {
        if (-not (Test-Path ".env.example")) {
            throw ".env.example was not found in the repository root."
        }

        Copy-Item ".env.example" ".env"
        Write-Host "Created .env from .env.example."
    }

    docker compose up -d --wait postgres redis
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose failed to start PostgreSQL and Redis with exit code $LASTEXITCODE."
    }

    $connectionLine = Get-Content ".env" |
        Where-Object { $_ -match '^ConnectionStrings__LibraryDatabase=' } |
        Select-Object -Last 1
    if (-not $connectionLine) {
        throw "ConnectionStrings__LibraryDatabase was not found in .env."
    }

    $connectionString = $connectionLine.Substring("ConnectionStrings__LibraryDatabase=".Length)
    $connectionString = $connectionString -replace '(?i)Host=postgres(?=;|$)', 'Host=localhost'
    $env:ConnectionStrings__LibraryDatabase = $connectionString

    dotnet tool restore
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet tool restore failed with exit code $LASTEXITCODE."
    }

    dotnet restore
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed with exit code $LASTEXITCODE."
    }

    dotnet ef database update `
        --project src/ApiEmprestimos.Infrastructure/ApiEmprestimos.Infrastructure.csproj `
        --startup-project src/ApiEmprestimos.Api/ApiEmprestimos.Api.csproj
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet ef database update failed with exit code $LASTEXITCODE."
    }

    docker compose up -d --build
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose failed to build and start the API with exit code $LASTEXITCODE."
    }

    Write-Host "Setup complete. The API is up and running at http://localhost:5000."
}
finally {
    if ($null -eq $previousConnectionString) {
        Remove-Item Env:ConnectionStrings__LibraryDatabase -ErrorAction SilentlyContinue
    }
    else {
        $env:ConnectionStrings__LibraryDatabase = $previousConnectionString
    }

    Pop-Location
}
