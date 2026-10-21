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

### Run locally

1. Clone the repository and navigate to the project folder:
```bash
git clone https://github.com/isaacgomesds/api-emprestimos-dotnet.git
cd api-emprestimos-dotnet
```

Create a `.env` file with the variables expected by Compose (for example: `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`).

```bash
docker compose up -d
```

The API will be available at:

- http://localhost:5000
- https://localhost:7000 (when enabled in local configuration)

Main endpoints:

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

```bash
dotnet ef database update --project src/ApiEmprestimos.Infrastructure --startup-project src/ApiEmprestimos.Api
```

If the EF CLI is not installed globally:

```bash
dotnet tool restore
dotnet ef database update --project src/ApiEmprestimos.Infrastructure --startup-project src/ApiEmprestimos.Api
```

### Tests

```bash
dotnet test
```

The suite covers unit tests, integration tests with PostgreSQL and Redis in containers, concurrent allocation tests for the last available copy, and the idempotency flow for loan creation.

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

Loan creation uses a conditional update strategy in PostgreSQL to ensure the last available copy is not shared by two concurrent transactions.

The main behavior is:

- validate availability and business rules inside the same transaction;
- apply an `UPDATE` with filter `AvailableCopies > 0` on `Books`;
- validate the number of affected rows to decide success or rejection.

This approach avoids negative inventory and keeps the source of truth in PostgreSQL, without depending on cache state for the final availability decision.

### Idempotency with Redis (`Idempotency-Key`)

The loan creation endpoint accepts the `Idempotency-Key` header and stores the successful response in Redis using a key derived from the supplied token.

Repeated requests with the same key:

- reuse the cached response;
- do not re-run business logic;
- do not decrement available copies again.

If the same key is reused with a different body, the API rejects the request with a conflict error, preserving safe idempotent semantics.

### Read Cache and Invalidation

Catalog and availability reads use Redis to reduce read latency. The cache middleware tracks a catalog version and uses versioned keys to prevent stale responses from being served after recent changes.

When events such as these occur:

- book creation, update, or deactivation;
- loan creation;
- return;
- cancellation;

the system increments the catalog version and/or invalidates the relevant keys so reads stay consistent with the current domain state.

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

The manifests in `k8s/` include a Deployment and Service for the API. The application is designed to behave correctly across multiple replicas while keeping concurrency and idempotency rules intact.

### Safe behavior between 2 and 11 replicas

- PostgreSQL remains the source of truth for real stock data.
- Availability control is atomic in the database, reducing the risk of race conditions between pods.
- Redis stores shared idempotent responses and cache entries across replicas.
- Cache reads and writes are versioned to avoid serving stale responses after book updates.
- Health checks and probes ensure only ready pods receive traffic.

Between 2 and 11 replicas, the solution remains safe because the successful loan decision depends on a consistent database-level concurrency control, not on the local state of a single instance.

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

The solution balances transactional consistency, idempotency, read caching, and domain-level traceability. This combination is essential for library loan APIs running in multi-replica environments with real concurrent access.
