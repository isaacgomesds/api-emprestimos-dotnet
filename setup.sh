#!/usr/bin/env bash
set -eu

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

docker compose up -d --build

echo "Setup complete. The API is up and running at http://localhost:5000."
