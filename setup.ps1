$ErrorActionPreference = "Stop"
Push-Location $PSScriptRoot

try {
    if (-not (Test-Path ".env")) {
        if (-not (Test-Path ".env.example")) {
            throw ".env.example was not found in the repository root."
        }

        Copy-Item ".env.example" ".env"
        Write-Host "Created .env from .env.example."
    }

    docker compose up -d --build
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose failed with exit code $LASTEXITCODE."
    }

    Write-Host "Setup complete. The API is up and running at http://localhost:5000."
}
finally {
    Pop-Location
}
