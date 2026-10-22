# ApiEmprestimos

## Overview

ApiEmprestimos is a REST API built with .NET 10 LTS for managing a library catalog, users, and loan operations. The solution was designed to preserve data integrity in concurrent scenarios while maintaining domain traceability and operational observability.

Core stack:

- .NET 10 LTS
- ASP.NET Core Web API
- PostgreSQL 17
- Redis
- Docker Compose
- OpenTelemetry + Prometheus
- EF Core + Npgsql

The application exposes endpoints for books, users, loans, health checks, metrics, and a domain audit trail for relevant business events.

## Prerequisites and Execution

### Prerequisites

- .NET 10 SDK
- Docker + Docker Compose
- PostgreSQL and Redis running locally or via Compose

### Run with Docker Compose

1. Clone the repository and navigate to the project folder:
```bash
git clone https://github.com/isaacgomesds/api-emprestimos-dotnet.git
cd api-emprestimos-dotnet
```

As an automated alternative to the manual steps below, run the script from the repository root:

```bash
./setup.sh
```

On Windows PowerShell, use:

```powershell
.\setup.ps1
```

The script creates `.env` from `.env.example` if needed and runs `docker compose up -d --build`. The API will be available at `http://localhost:5000`.

2. Copy `.env.example` to `.env` before running Docker Compose:

```bash
# Linux/macOS
cp .env.example .env
```

```powershell
# Windows PowerShell
Copy-Item .env.example .env
```

3. Start the services and build the API image:

```bash
docker compose up --build -d
```

The API will be available at:

- http://localhost:5000
- https://localhost:7000 (when enabled in local configuration)

Main endpoints:

- `GET /users` (lists all users with their ID, name, email, and creation timestamp)
- `POST /users` (creates a user)
- `GET /users/{id}/loans` (gets a user's loan history)
- `GET /health/live`
- `GET /health/ready`
- `GET /metrics`
- `POST /loans` (requires `Idempotency-Key` header)

## Sample Seed Data

The database is migrated automatically when the API starts and includes the following data for testing:

**Books**

- `11111111-1111-1111-1111-111111111111` — The Hobbit
- `11111111-1111-1111-1111-111111111112` — Pride and Prejudice
- `11111111-1111-1111-1111-111111111113` — Nineteen Eighty-Four

**Users**

- `22222222-2222-2222-2222-222222222222` — Standard User
- `22222222-2222-2222-2222-222222222223` — Administrator
- `22222222-2222-2222-2222-222222222224` — Librarian

## Migrations and Tests

### EF Core Migrations

The API automatically applies pending migrations at startup through `Database.MigrateAsync()`. In development, the local tool below can apply migrations manually as an alternative to starting the API first; it is not an additional required step.

```bash
dotnet tool restore
dotnet ef database update --project src/ApiEmprestimos.Infrastructure/ApiEmprestimos.Infrastructure.csproj --startup-project src/ApiEmprestimos.Api/ApiEmprestimos.Api.csproj
```

The versioned `.config/dotnet-tools.json` manifest pins `dotnet-ef` to `10.0.13`, matching the EF Core version used by the project. Configure `ConnectionStrings__LibraryDatabase` to a local development database before running the command. Do not run manual migrations against production without following its change-management process.

To create a migration, run `dotnet ef migrations add <MigrationName>` with the same `--project` and `--startup-project` arguments.

### Tests

```bash
dotnet test
```

The suite includes unit tests and integration tests with PostgreSQL and Redis containers for concurrent availability, returns, duplicate ISBNs, idempotency, loan history, and business metrics. Integration tests require Docker to be running.

### Postman Collection

With the API running, import `postman/ApiEmprestimos.postman_collection.json` from the repository root using Postman's **Import** menu. The collection uses `http://localhost:5000` by default; update the collection's `baseUrl` variable if the API is running at a different address, then run the requests you need.

## Environment Variables

Check `.env.example` for configuration reference. The expected variables include:

```env
POSTGRES_DB=apiemprestimos
POSTGRES_USER=postgres
POSTGRES_PASSWORD=postgres
ASPNETCORE_ENVIRONMENT=Development
ConnectionStrings__LibraryDatabase=Host=postgres;Port=5432;Database=apiemprestimos;Username=postgres;Password=postgres
Redis__ConnectionString=redis:6379
```

Important:

- do not commit real secrets;
- keep `ConnectionStrings__LibraryDatabase` and `Redis__ConnectionString` out of version control in production;
- use Secret Manager, environment variables, or cluster secret solutions instead.

## Architectural Decisions

### Atomic Concurrency in PostgreSQL

Loan creation uses a conditional update strategy in PostgreSQL to ensure the last available copy is not shared by two concurrent transactions. Stock edits and returns also use atomic database expressions; a return/cancellation status change and its inventory adjustment run in the same transaction.

The main behavior is:

- validate availability and business rules inside the same transaction;
- apply an `UPDATE` with filter `AvailableCopies > 0` on `Books`;
- validate the number of affected rows to decide success or rejection;
- persist only changed metadata fields instead of overwriting inventory read earlier;
- update stock from the row's current values, avoiding in-memory read-modify-write.

This approach avoids negative inventory and keeps the source of truth in PostgreSQL, without depending on cache state for the final availability decision.

### Idempotency with Redis (`Idempotency-Key`)

The loan creation endpoint accepts the `Idempotency-Key` header. Redis coordinates concurrent requests and speeds up replays, while the request hash and successful response are persisted in PostgreSQL in the same transaction as the loan, stock decrement, and audit record.

Repeated requests with the same key:

- reuse the persisted response from the Redis cache or, on a cache miss/expiration, from the durable PostgreSQL record;
- do not re-run business logic;
- do not decrement available copies again.

A database unique constraint and conditional idempotency-record insert also serialize requests when the cache expires or an instance restarts. If the same key is reused with a different body, the API rejects the request with a conflict error.

### Business Metrics

The `/metrics` endpoint exposes these Prometheus counters without high-cardinality attributes:

- `api_loans_created_total`: loans actually persisted; increments after transaction commit, not on replay.
- `api_loans_rejected_no_available_copies_total`: attempts rejected specifically because no copies are available; unrelated conflicts and errors do not increment it.
- `api_loans_idempotent_replays_total`: repeated requests that reuse a stored response, from either the Redis cache or the durable PostgreSQL record.

Counters are process-local. Prometheus must scrape every replica and aggregate the series to report service-wide totals.

### Read Cache and Invalidation

Catalog and availability reads use Redis to reduce read latency. The cache middleware tracks a catalog version and uses versioned keys. It invalidates the version before a mutation and again after a successful response to limit the stale-response window. Since PostgreSQL and Redis do not share a transaction, a read very close to commit may still observe an older entry; the final availability decision always uses PostgreSQL.

When events such as these occur:

- book creation, update, or deactivation;
- loan creation;
- return;
- cancellation;

the system increments the catalog version and/or invalidates relevant keys to reduce the window for stale responses. Loan availability decisions are still made by PostgreSQL.

### Domain Audit Trail

Every relevant business event is recorded in an audit table with fields such as:

- entity name;
- entity identifier;
- action performed;
- actor;
- UTC timestamp;
- correlationId;
- JSON payload with detailed state.

This preserves a business-level audit trail for support, debugging, and operational investigation without relying only on technical logs.

## Kubernetes Scalability

The manifests in `k8s/` include a Deployment and Service. For multiple replicas, all instances must point to the same PostgreSQL and Redis services. Inventory integrity uses conditional/atomic PostgreSQL updates; idempotency also uses a PostgreSQL unique index and durable response record, while Redis coordinates the fast path and shared cache.

The integration suite includes scenarios intended to validate two API hosts sharing PostgreSQL and Redis. They still need to be run with Docker; when run, they exercise invariants across hosts in the test process but are not load tests and do not validate a specific production replica capacity. Redis availability is still required by the current middleware.

## Known Limitations and Next Steps

### Known limitations

- The current cache strategy is suitable for catalog and availability reads but could be improved with more granular cache keys and entity-specific invalidation.
- The audit trail captures domain events but does not yet integrate external alerts or export to a centralized data platform.
- The metrics and traces satisfy the challenge requirements but can be extended to richer SLOs and dashboards.
- The solution currently relies on local containers for PostgreSQL and Redis; production should use managed services and a stronger secret-management process.

### Next steps for production

- move sensitive configuration to a Secret Manager or cluster secret system;
- add a CI/CD pipeline with build, tests, and quality gates;
- use managed Redis or Redis Cluster with better TTL and observability controls;
- expand business metrics for success rate, rejection rate, and p95 latency;
- consider authentication, authorization, rate limiting, and distributed tracing across services.

## Conclusion

The implementation places inventory updates and durable idempotency records in PostgreSQL, using Redis for coordination and read caching. Confirmation of the concurrency scenarios depends on running the integration tests with Docker.
