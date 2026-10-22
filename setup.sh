#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
cd "$ROOT_DIR"

if [ ! -f .env ]; then
    if [ ! -f .env.example ]; then
        echo "Error: .env.example was not found in the repository root." >&2
        exit 1
    fi
    cp .env.example .env
    echo "Created .env from .env.example."
fi

docker compose up -d --wait postgres redis

connection_string="$(sed -n 's/^ConnectionStrings__LibraryDatabase=//p' .env | tail -n 1)"
if [ -z "$connection_string" ]; then
    echo "Error: ConnectionStrings__LibraryDatabase was not found in .env." >&2
    exit 1
fi

# Compose uses the service hostname; EF Core runs on the host during setup.
connection_string="${connection_string/Host=postgres/Host=localhost}"
export ConnectionStrings__LibraryDatabase="$connection_string"

dotnet tool restore
dotnet restore
dotnet ef database update \
    --project src/ApiEmprestimos.Infrastructure/ApiEmprestimos.Infrastructure.csproj \
    --startup-project src/ApiEmprestimos.Api/ApiEmprestimos.Api.csproj

docker compose up -d --build

echo "Setup complete. The API is up and running at http://localhost:5000."
