# JobFlow

JobFlow is a job automation platform that lets authenticated users create, schedule, and monitor automated HTTP/API jobs with full execution history, failure tracking, and retries.

## Problem

Teams often need to run recurring or on-demand HTTP calls — health checks, webhook triggers, data syncs — but ad-hoc scripts and cron jobs lack visibility, retry logic, and a unified history of what ran and what failed. JobFlow provides a structured platform for defining jobs, running them on demand or on a schedule, and understanding every execution outcome.

## Architecture

```
Next.js Frontend
       ↓
ASP.NET Core API
       ↓
PostgreSQL
```

Background processing:

```
Application
       ↓
PostgreSQL transaction
       ├── Execution state
       └── OutboxMessage
              ↓
       Outbox Dispatcher (Worker)
              ↓
           Hangfire
              ↓
           Worker → HTTP Executor
```

The API persists execution state and outbox messages atomically. The Worker runs an outbox dispatcher that publishes pending messages to Hangfire, then executes HTTP jobs. This closes the dangerous DB-commit → Hangfire-enqueue gap.

## Tech Stack

| Layer | Technology |
|-------|------------|
| Frontend | Next.js 15, React 19, TypeScript, Tailwind CSS |
| Backend API | ASP.NET Core 8, C# |
| Worker | .NET Worker Service + Hangfire |
| Database | PostgreSQL 16 |
| ORM | Entity Framework Core |
| Background jobs | Hangfire (PostgreSQL storage) |
| Auth | JWT Bearer tokens, ASP.NET Core PasswordHasher |
| Tests | xUnit |
| Infrastructure | Docker, Docker Compose |

## Project Structure

```
enrichly-job-automation/
├── frontend/          # Next.js App Router application
├── backend/           # .NET solution (Api, Application, Domain, Infrastructure, Worker)
├── tests/             # Unit and integration test projects
├── docker-compose.yml # PostgreSQL for local development
├── .env.example       # Environment variable template
└── ENGINEERING.md     # Architecture and engineering decisions
```

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 20+](https://nodejs.org/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for PostgreSQL)

## Environment Variables

Copy `.env.example` to `.env` and fill in values:

```bash
cp .env.example .env
```

| Variable | Description |
|----------|-------------|
| `POSTGRES_DB` | PostgreSQL database name |
| `POSTGRES_USER` | PostgreSQL username |
| `POSTGRES_PASSWORD` | PostgreSQL password |
| `DATABASE_CONNECTION_STRING` | Full connection string for the backend |
| `JWT_SECRET` | Secret key for JWT signing (min 32 chars; required in production) |
| `JWT_ISSUER` | JWT token issuer |
| `JWT_AUDIENCE` | JWT token audience |
| `JWT_EXPIRATION_MINUTES` | Token lifetime in minutes (default 60) |
| `NEXT_PUBLIC_API_URL` | Backend API URL for the frontend |
| `TEST_DATABASE_CONNECTION_STRING` | Optional separate DB for integration tests |
| `CORS_ALLOWED_ORIGINS` | Comma-separated frontend origins (required in production) |
| `RATE_LIMITING__AUTHPERMITLIMIT` | Login/register requests per window per IP (default 10/min) |
| `RATE_LIMITING__RUNPERMITLIMIT` | Run Now requests per window per user (default 30/min) |
| `RATE_LIMITING__RETRYPERMITLIMIT` | Manual retry requests per window per user (default 20/min) |
| `API_PORT` | Host port for API container (default 8080) |
| `FRONTEND_PORT` | Host port for frontend container (default 3000) |
| `POSTGRES_PORT` | Host port for PostgreSQL (default 5433 when 5432 is occupied locally) |

## Running Locally

### 1. Start PostgreSQL

```bash
docker compose up -d postgres
```

### 2. Start the API

```powershell
cd backend
$env:DATABASE_CONNECTION_STRING="Host=localhost;Port=5433;Database=jobautomation;Username=jobautomation;Password=change_me_in_local_env"
$env:JWT_SECRET="change_me_to_a_long_random_secret_at_least_32_chars"
dotnet run --project JobAutomation.Api
```

The API runs at **http://localhost:5000**.

### 3. Start the Worker (optional, separate terminal)

```powershell
cd backend
$env:DATABASE_CONNECTION_STRING="Host=localhost;Port=5433;Database=jobautomation;Username=jobautomation;Password=change_me_in_local_env"
dotnet run --project JobAutomation.Worker
```

### 4. Start the Frontend

```bash
cd frontend
npm install
npm run dev
```

The frontend runs at **http://localhost:3000**.

## Running with Docker (Full Stack)

Build and start all services (PostgreSQL, API, Worker, Frontend):

```bash
docker compose up -d --build
```

Verify services:

```bash
docker compose ps
curl http://localhost:8080/health/live
curl http://localhost:8080/health/ready
```

- API: **http://localhost:8080**
- Frontend: **http://localhost:3000**
- PostgreSQL host port: **5433** (configurable via `POSTGRES_PORT` in `.env`)

Set `CORS_ALLOWED_ORIGINS` and a strong `JWT_SECRET` before deploying to production.

## Authentication

Register at `/register` or via API:

```bash
curl -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"user@example.com","password":"StrongPassword123!"}'
```

Login:

```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"user@example.com","password":"StrongPassword123!"}'
```

Use the returned `accessToken` as a Bearer token for protected endpoints:

```bash
curl http://localhost:5000/api/jobs \
  -H "Authorization: Bearer <token>"
```

## Running a Job

1. **Login** at http://localhost:3000/login
2. **Create an Active job** with a valid HTTP/HTTPS URL
3. Click **Run Now** on the jobs page (generates an idempotency key per click)
4. The API creates an execution (status `Queued`) and an outbox message in one transaction
5. The **Worker** outbox dispatcher publishes to Hangfire, then executes the HTTP request
6. View execution status on the detail page (polls while Queued/Running/Retrying)
7. **Retry** failed executions or **Cancel** queued/retrying executions from the detail page

Start the worker in a separate terminal:

```powershell
cd backend
$env:DATABASE_CONNECTION_STRING="Host=localhost;Port=5433;Database=jobautomation;Username=jobautomation;Password=change_me_in_local_env"
dotnet run --project JobAutomation.Worker
```

## API Endpoints

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/api/auth/register` | No | Register a new user |
| POST | `/api/auth/login` | No | Login and receive JWT |
| GET | `/health` | No | Overall health check |
| GET | `/health/live` | No | Liveness probe |
| GET | `/health/ready` | No | Readiness probe |
| GET | `/api/auth/me` | Yes | Current user profile (includes `isAdmin`) |
| GET | `/api/dashboard/summary` | Yes | User-scoped dashboard metrics (`?range=24h\|7d\|30d`) |
| GET | `/api/admin/system` | Admin | System health (workers, outbox, infrastructure) |
| GET | `/api/jobs` | Yes | List own jobs (paginated, searchable) |
| POST | `/api/jobs` | Yes | Create a job (status → Active) |
| GET | `/api/jobs/{id}` | Yes | Get own job by ID |
| PUT | `/api/jobs/{id}` | Yes | Update own job |
| DELETE | `/api/jobs/{id}` | Yes | Archive job (soft delete) |
| POST | `/api/jobs/{id}/enable` | Yes | Paused → Active |
| POST | `/api/jobs/{id}/disable` | Yes | Active → Paused |
| POST | `/api/jobs/{id}/run` | Yes | Run Now — requires `Idempotency-Key` header (GUID) |
| GET | `/api/jobs/{id}/executions` | Yes | Execution history for a job |
| GET | `/api/executions/{id}` | Yes | Execution detail with logs |
| POST | `/api/executions/{id}/retry` | Yes | Manual retry (Failed only) — requires `Idempotency-Key` |
| POST | `/api/executions/{id}/cancel` | Yes | Cancel Queued or Retrying execution |

### Job ownership

Every job belongs to exactly one user via `Job.UserId`. The authenticated user's ID comes from the JWT `sub` claim — never from the request body. Users can only access their own jobs; cross-user access returns **404 Not Found**.

### Archive behavior

`DELETE /api/jobs/{id}` sets status to `Archived` rather than physically deleting the row. This preserves execution history for future phases.

## Swagger

In Development mode, Swagger UI is available at **http://localhost:5000/swagger** with JWT Bearer authentication support.

Hangfire dashboard (Development only): **http://localhost:5000/hangfire**

## Testing

```bash
# Unit tests
dotnet test tests/JobAutomation.UnitTests

# Integration tests (requires PostgreSQL)
$env:TEST_DATABASE_CONNECTION_STRING="Host=localhost;Port=5433;Database=jobautomation_test;Username=jobautomation;Password=change_me_in_local_env"
dotnet test tests/JobAutomation.IntegrationTests

# Frontend
cd frontend && npm run typecheck && npm run build
```

## What Is Implemented

**Phase 1:** Foundation — domain model, EF Core, Hangfire infrastructure, health endpoint, frontend shell.

**Phase 2:** Authentication, authorization, and job management.

**Phase 3:** End-to-end execution path — Run Now, Hangfire enqueue, Worker HTTP execution, history/detail UI.

**Phase 4:** Reliability and concurrency — atomic claiming, idempotency, worker leases, stale recovery.

**Phase 5:** Automatic retry engine with exponential backoff, jitter, and `Retry-After` support.

**Phase 6:** Cron / recurring scheduling — 5-field UTC cron validation (Cronos), Hangfire recurring jobs with deterministic ID `job:{jobId}:recurring`, scheduled executions via worker pipeline (`TriggerType.Scheduled`), deterministic occurrence idempotency, lifecycle integration, frontend cron input.

**Phase 7:** Production reliability — transactional outbox, outbox dispatcher, orphaned retry recovery, manual retry with idempotency, execution cancellation (`Queued`/`Retrying`), frontend Retry/Cancel controls.

**Phase 8:** Observability — liveness/readiness health checks, worker heartbeats, outbox health, dashboard metrics API, admin system health, request correlation IDs, operational dashboard UI.

**Phase 9:** Production hardening — SSRF protection (DNS + IP validation, redirect safety), sensitive header sanitization, rate limiting, CORS lockdown, security headers, production error sanitization, request size limits, Docker full-stack deployment, expanded security/reliability tests.

## Production Considerations

- Set `JWT_SECRET` to at least 32 random characters; startup fails in production with a weak/missing secret.
- Set `CORS_ALLOWED_ORIGINS` to your frontend URL(s); unrestricted CORS is not allowed outside development/testing.
- Job target URLs are validated against SSRF (localhost, private/link-local/metadata IPs blocked at create/update and execution).
- HTTP redirects are validated manually (max 3); redirect targets to private networks are rejected.
- Rate limits protect auth, Run Now, and manual retry endpoints (configurable via `RateLimiting` section).
- API request body limit: **256 KB**. Job payload max: **64 KB**. Max custom headers: **20**.
- HTTP job timeout: configurable per job, max **300 seconds**.
- Sensitive headers (`Authorization`, `Cookie`, API keys) are redacted in API responses and never logged.
- Production error responses do not expose stack traces, SQL, or internal paths. All errors include `X-Request-Id`.

## Health Endpoints

| Endpoint | Purpose |
|----------|---------|
| `GET /health` | Overall health (database + Hangfire) |
| `GET /health/live` | Liveness — process is alive |
| `GET /health/ready` | Readiness — dependencies available |

## Dashboard

Authenticated users can access `GET /api/dashboard/summary?range=24h|7d|30d` for job/execution metrics scoped to their account.

Administrators (`User.IsAdmin`) can access `GET /api/admin/system` for worker health, outbox backlog, and infrastructure status.

All API responses include `X-Request-Id` for correlation.

## Cron Scheduling

Jobs can include an optional **UTC cron schedule** (5 fields: `minute hour day month weekday`).

Examples:

| Expression | Meaning |
|------------|---------|
| `*/5 * * * *` | Every 5 minutes |
| `0 * * * *` | Every hour |
| `0 9 * * *` | Daily at 09:00 UTC |
| `0 9 * * 1-5` | Weekdays at 09:00 UTC |

Active jobs with a schedule automatically create executions at each occurrence. The scheduler creates a `Queued` execution and enqueues it — it never calls HTTP directly.

## Retry Policy

`Job.MaxRetries` controls how many times a failed attempt may be retried **after** the initial run:

- `MaxRetries = 0` → 1 total attempt
- `MaxRetries = 3` → up to 4 total attempts (initial + 3 retries)

Retryable failures (e.g. 500, 503, timeout, network) schedule a delayed retry with exponential backoff. Non-retryable failures (e.g. 400, 401, invalid URL) fail immediately.

Example with `MaxRetries = 3`:

```
Attempt 1 → HTTP 503 → Retrying (5s delay)
Attempt 2 → HTTP 503 → Retrying (10s delay)
Attempt 3 → HTTP 503 → Retrying (20s delay)
Attempt 4 → HTTP 503 → Failed (retries exhausted)
```

## Reliability

- **Transactional outbox:** execution state and Hangfire enqueue intent are committed atomically; a dispatcher publishes to Hangfire
- **At-least-once publication:** duplicate Hangfire messages are safe because execution claiming is atomic
- **Atomic claiming:** only one worker can claim a `Queued` execution via `UPDATE ... WHERE status = 'Queued'`
- **Idempotency keys:** same key for the same job returns the same execution (202 new, 200 existing)
- **Worker leases:** each claim gets a unique `LeaseId` with periodic heartbeat extension
- **Stale recovery:** expired leases are atomically reset to `Queued` with a new outbox message (every minute)
- **Retry recovery:** orphaned `Retrying` executions (due but no retry outbox) are recovered to `Queued` (every minute)
- **Manual retry:** user-initiated retry of `Failed` executions reuses the same execution row (`TriggerType.ManualRetry`, attempt reset to 1)
- **Cancellation:** `Queued` and `Retrying` executions can be cancelled to terminal `Cancelled` state
- **Outbox cleanup:** processed outbox messages older than 7 days are deleted (configurable)

**Exactly-once external HTTP side effects are not guaranteed.** The system provides durable, at-least-once background-job publication. Duplicate Hangfire delivery or worker crashes may cause the external HTTP call to run more than once. See [ENGINEERING.md](./ENGINEERING.md).

Running execution cancellation is **not supported** — cancellation is available for `Queued` and `Retrying` only.

## What Is NOT Implemented Yet

- Missed schedule catch-up
- Running execution cancellation (requires HttpClient abort wiring)
- Notifications, billing, distributed tracing platform
- Redis/Kafka/external message brokers (PostgreSQL + Hangfire + outbox remain the dispatch path)

See [ENGINEERING.md](./ENGINEERING.md) for detailed engineering notes.
