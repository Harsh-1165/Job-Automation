# Engineering Notes

## Architecture

JobFlow uses a **layered monolith** — a single deployable backend split into clear projects rather than microservices.

```
JobAutomation.Api          → HTTP, Swagger, middleware, composition root
JobAutomation.Application  → Interfaces, DTOs, validation, service contracts
JobAutomation.Domain       → Entities, enums, domain rules (no infrastructure deps)
JobAutomation.Infrastructure → EF Core, Hangfire, JWT, service implementations
JobAutomation.Worker       → Dedicated Hangfire worker process
```

**Dependency direction:** Api → Application → Domain. Infrastructure implements Application interfaces and is referenced only by Api and Worker.

## Authentication

Phase 2 implements JWT-based authentication:

- **Registration:** `POST /api/auth/register` — validates email/password, hashes password with ASP.NET Core `PasswordHasher<User>`, stores user with normalized email
- **Login:** `POST /api/auth/login` — verifies password, returns JWT with `sub` (user ID) and `email` claims
- **Token validation:** Issuer, audience, signature, and lifetime are all validated on protected endpoints
- **Production safety:** Application fails startup in Production if `JWT_SECRET` is missing or too short. Development allows a placeholder secret.

Passwords are never stored in plaintext and never returned by the API.

## Authorization

Every job belongs to a user via `Job.UserId`. Authorization is enforced server-side:

- All job endpoints require `[Authorize]`
- `ICurrentUserService` extracts the user ID from JWT claims — never from request body
- Job queries filter by authenticated user ID
- Cross-user access returns **404 Not Found** (ownership-safe, does not leak resource existence)

The frontend route protection is a UX convenience only; the API is the real authorization boundary.

## Job Lifecycle

```
Draft → Active → Paused ↔ Active
         ↓          ↓
       Archived ← Archived
```

| Transition | Endpoint | Rule |
|------------|----------|------|
| Create | `POST /api/jobs` | New jobs start as **Active** (immediately usable for future Run Now) |
| Disable | `POST /api/jobs/{id}/disable` | Active → Paused |
| Enable | `POST /api/jobs/{id}/enable` | Paused → Active |
| Archive | `DELETE /api/jobs/{id}` | Any non-archived → Archived |

Archived jobs are terminal — they cannot be edited, enabled, or disabled.

## Data Integrity

- **Normalized email:** `User.NormalizedEmail` stores lowercase email for case-insensitive uniqueness
- **Database constraint:** Unique index on `NormalizedEmail` prevents duplicate accounts even under concurrent registration
- **FK behavior:** `Job.UserId → User.Id` uses `Restrict` delete — users cannot be deleted while jobs exist, preserving execution history
- **Idempotency key:** Unique index on `(JobId, IdempotencyKey)` prepared for Phase 3 execution deduplication

## Archive Decision

Jobs are **archived**, not physically deleted, because:

- Execution records reference jobs and must remain understandable
- Historical audit trails should survive job retirement
- Soft delete allows future "restore" if needed

Default job listing excludes archived jobs unless `includeArchived=true`.

## Execution Architecture

```
User → POST /api/jobs/{id}/run
     → PostgreSQL transaction
         ├── Execution (Queued)
         └── OutboxMessage (ExecutionEnqueue)
     → commit
     → Outbox Dispatcher (Worker hosted service)
     → Hangfire enqueue (Execution ID only)
     → Worker receives Execution ID
     → ExecutionService.ProcessExecutionAsync
     → HttpJobExecutor performs HTTP request
     → Execution updated (Succeeded/Failed/Retrying/Cancelled)
     → Job.LastRunAtUtc updated
```

The API **never** performs user HTTP requests. Only the Worker executes them.

## Execution State Machine

```
Queued → Running → Succeeded
Queued → Running → Failed (non-retryable or retries exhausted)
Running → Retrying → Queued → Running → ...
Running → Queued   (stale recovery only — expired lease)
Queued → Cancelled
Retrying → Cancelled
Failed → Queued    (manual retry only — TriggerType=ManualRetry, attempt reset)
```

One logical execution row represents the entire Run Now operation (including manual retries). `AttemptNumber` tracks the current attempt (Attempt 1 = first run, Attempt 2 = first automatic retry, etc.). Manual retry resets `AttemptNumber` to 1 as a new user-requested attempt cycle on the same execution row.

## Retry State Machine

After a retryable failure while `Running`:

1. `IRetryPolicy` evaluates the failure
2. If retries remain: `Running → Retrying`, set `NextRetryAtUtc`, create `OutboxMessage (RetryPreparation)` in the same transaction
3. Outbox dispatcher publishes delayed Hangfire job
4. When delay elapses: `PrepareRetryAsync` transitions `Retrying → Queued`, increments `AttemptNumber`
4. Worker claims `Queued → Running` and executes the next attempt

`Job.MaxRetries` is the maximum number of retries **after** the initial attempt:

| MaxRetries | Total attempts |
|------------|----------------|
| 0 | 1 |
| 1 | 2 |
| 3 | 4 |

## Retry Classification

| Category | Examples | Retries? |
|----------|----------|----------|
| Retryable HTTP | 408, 429, 500, 502, 503, 504 | Yes (if retries remain) |
| Non-retryable HTTP | 400, 401, 403, 404, 405 | No |
| Timeout / network | connection failure, request timeout | Yes |
| Request construction | invalid URL, invalid headers | No |
| Authorization | 401, 403 | No — configuration issue, not transient |

## Exponential Backoff

```
delay = min(BaseDelay × 2^(attempt - 1) + jitter, MaxBackoffSeconds)
```

Defaults: `BaseDelaySeconds = 5`, `MaxBackoffSeconds = 300`, `JitterPercentage = 25`.

Example (no jitter): Attempt 1 failure → 5s, Attempt 2 → 10s, Attempt 3 → 20s.

## Jitter

Random jitter (0–25% of calculated delay by default) prevents synchronized retry storms when many jobs fail at once.

Set `JitterPercentage = 0` in tests for deterministic delay assertions.

## Retry-After

HTTP 429 responses may include `Retry-After`. When present and valid, that value is used (clamped to `MaxBackoffSeconds`). Invalid or missing `Retry-After` falls back to exponential backoff.

## Worker Crash vs Retry

These are different mechanisms:

- **Retry policy:** application deliberately schedules a new attempt after a retryable failure (`Running → Retrying → Queued`)
- **Stale recovery:** worker crash detector for `Running` executions with expired lease (`Running → Queued`, same attempt number)

Stale recovery does **not** touch `Retrying` executions — they wait for their scheduled Hangfire delayed job.

If a job becomes Paused/Archived while an execution is `Retrying`, the pending retry is cancelled and the execution is marked `Failed`.

## Worker Model

Multiple Worker instances can run against the same Hangfire PostgreSQL storage. Hangfire distributes queued jobs across available workers. Each worker runs `ExecuteJobBackgroundTask` which delegates to `IExecutionService.ProcessExecutionAsync`.

The API retains Hangfire storage for enqueueing and a dev dashboard. It does **not** run a Hangfire worker server.

## HTTP Execution

- Uses `IHttpClientFactory` with a named `"JobExecutor"` client
- Per-job timeout via `CancellationTokenSource` (`Job.TimeoutSeconds`, max 300)
- **2xx = Succeeded**, non-2xx = Failed (stored, not thrown)
- Network errors and timeouts → Failed with safe error message
- Response body limited to **1 MB** (truncated with indicator)
- Sensitive headers (Authorization, API keys) are never logged

## Transactional Outbox (Phase 7)

Execution persistence and Hangfire enqueue intent are **atomic**:

1. Application writes business state + `OutboxMessage` in one PostgreSQL transaction
2. `OutboxDispatcherHostedService` (Worker) polls pending messages
3. Dispatcher atomically claims a message (`Pending → Processing` with lease)
4. `HangfireOutboxPublisher` enqueues/schedules Hangfire jobs
5. Message marked `Processed` on success; on failure, `AttemptCount` incremented with exponential backoff

Message types:

| Type | Hangfire operation |
|------|-------------------|
| `ExecutionEnqueue` | `BackgroundJob.Enqueue(ExecuteAsync)` |
| `RetryPreparation` | `BackgroundJob.Schedule(PrepareRetryAsync)` |

### Outbox consistency model

- **At-least-once publication** to Hangfire — duplicate publish is possible if mark-Processed fails after enqueue
- **Execution idempotency** — atomic claiming ensures duplicate Hangfire jobs do not cause duplicate active HTTP attempts
- **Leases** — processing messages use `LockedUntilUtc` / `LockId`; expired locks are reclaimed
- **Cleanup** — `Processed` messages older than 7 days deleted by recurring job

Configuration section: `Outbox` (`BatchSize`, `PollIntervalSeconds`, `LeaseDurationSeconds`, `ProcessedRetentionDays`, etc.)

## Concurrency

When two workers receive the same Hangfire job (same Execution ID), a naive `SELECT` then `UPDATE` allows both to read `Queued` and both to execute the HTTP request.

Phase 4 uses an **atomic conditional update**:

```sql
UPDATE executions
SET status = 'Running', lease_id = @leaseId, lease_expires_at_utc = @expires, ...
WHERE id = @executionId AND status = 'Queued';
```

If **rows affected = 1**, the worker claimed the execution. If **0**, another worker already claimed it or the execution is no longer executable — the worker stops without calling HTTP.

Duplicate Hangfire deliveries are expected; `ExecutionAlreadyClaimed` is logged and is not treated as a failure.

## Idempotency

Run Now requires an `Idempotency-Key` header (GUID). The same `(JobId, IdempotencyKey)` always maps to the same execution record.

- **Database constraint:** unique index on `(JobId, IdempotencyKey)` is the final guard
- **Race handling:** concurrent requests with the same key — one insert wins, the other catches the unique violation and returns the existing execution
- **Responses:** `202 Accepted` for newly created executions, `200 OK` when reusing a key
- **Ownership:** lookup always includes authenticated user + job ownership — keys do not cross users
- **Intentional re-runs:** different keys create different executions

Idempotency protects duplicate **API requests**. Execution claiming protects duplicate **worker processing**. Both are required.

## Worker Leases

When a worker claims an execution it receives:

- `LeaseId` — unique GUID identifying this claim (not worker name)
- `LeaseExpiresAtUtc` — now + lease duration (default 60s, configurable)
- `LastHeartbeatAtUtc` — updated periodically while processing

While processing, a background heartbeat loop extends the lease every 20s (configurable). Heartbeat and completion updates require matching `LeaseId`:

```sql
UPDATE executions SET ... WHERE id = @id AND status = 'Running' AND lease_id = @leaseId;
```

If a worker loses its lease, it stops updating the execution and logs `ExecutionLeaseLost`.

## Stale Recovery

A Hangfire recurring job (`ExecutionRecoveryJob`, every minute) finds Running executions whose lease has expired:

```sql
UPDATE executions SET status = 'Queued', lease_id = NULL, ...
WHERE id = @id AND status = 'Running' AND lease_expires_at_utc < NOW();
```

If **rows affected = 1**, an `ExecutionEnqueue` outbox message is created. Multiple sweepers are safe — only one atomic update succeeds per execution.

This is an **at-least-once** recovery strategy, not exactly-once.

## Orphaned Retry Recovery (Phase 7)

A recurring `RetryRecoveryJob` (every minute) finds executions where:

- `Status = Retrying`
- `NextRetryAtUtc <= now`
- No pending/processing `RetryPreparation` outbox message

It atomically transitions `Retrying → Queued` (incrementing `AttemptNumber`) and creates an `ExecutionEnqueue` outbox message. Concurrent recovery and normal retry preparation are safe — only one atomic transition succeeds.

## Manual Retry (Phase 7)

`POST /api/executions/{id}/retry` with `Idempotency-Key` header:

- Allowed only for `Failed` executions owned by the authenticated user
- Reuses the same execution row (preserves history/logs)
- Sets `TriggerType = ManualRetry`, resets `AttemptNumber = 1`, clears completion fields
- Stores `ManualRetryIdempotencyKey` for duplicate-request protection
- Creates `ExecutionEnqueue` outbox message atomically with state change
- Does not consume or modify automatic retry budget semantics — represents a new user-requested cycle

## Cancellation (Phase 7)

`POST /api/executions/{id}/cancel`:

- Allowed for `Queued` and `Retrying` only
- Terminal state: `Cancelled` — no transitions out, no automatic retry
- Worker paths check status before claim/prepare — cancelled executions never execute HTTP
- **Running cancellation not supported** — HttpClient abort is not wired through the worker pipeline

## Exactly-Once Semantics

**The platform does not guarantee exactly-once external side effects.**

What we guarantee:

1. Exactly one worker can successfully **claim** a queued execution at a time
2. Duplicate Hangfire deliveries do not cause multiple active workers on the same queued execution
3. A stale worker cannot overwrite a newer owner's result (lease-validated completion)

What we do **not** guarantee:

- External HTTP requests never happen twice
- Distributed exactly-once execution end-to-end

If a worker sends an HTTP request then crashes before persisting success, stale recovery may requeue the execution and another worker may repeat the external call. External APIs that honor idempotency keys can reduce this risk, but that depends on the target system.

## Worker Crash Scenarios

| Scenario | Outcome |
|----------|---------|
| Crash before HTTP | Lease expires → recovery → requeue → no prior external side effect |
| Crash during HTTP | Recovery may requeue → external API may receive duplicate request |
| HTTP succeeds, DB updated | Status `Succeeded` — no recovery |
| HTTP succeeds, crash before DB commit | Recovery may requeue → possible duplicate external request |

## Cron Scheduling (Phase 6)

```
Cron Schedule (Job.CronExpression, UTC)
      ↓
Hangfire Recurring Job (job:{jobId}:recurring)
      ↓
ScheduledJobBackgroundTask
      ↓
CreateScheduledExecutionAsync
      ↓
Execution (Queued, TriggerType=Scheduled) + OutboxMessage
      ↓
Outbox dispatcher → Hangfire enqueue
      ↓
Worker → atomic claim → HTTP executor → Succeeded / Retrying / Failed / Cancelled
```

### Cron format

Standard **5-field** cron (minute hour day-of-month month day-of-week). **All schedules use UTC.**

Validated server-side with Cronos before any Hangfire registration.

### Job lifecycle integration

| Job state | Recurring Hangfire job |
|-----------|------------------------|
| Active + valid schedule | Registered/updated |
| Paused | Removed |
| Archived | Removed |
| Active, no schedule | Removed (manual-only) |

Enable/disable/archive/update all sync the recurring registration using the deterministic ID `job:{jobId}:recurring`.

### Scheduled idempotency

Each cron occurrence uses a deterministic key:

```
scheduled:{jobId}:{occurrenceUtc}
```

Example: `scheduled:8b6f...:2026-09-14T10:00:00Z`

Protected by the existing unique index on `(JobId, IdempotencyKey)`. Duplicate scheduler invocations for the same occurrence return the existing execution safely.

### Retry interaction

Scheduled executions are normal executions. Retry policy applies identically. Each cron occurrence is a **separate** execution — retries do not merge with the next cron tick.

### Missed schedule policy

No custom catch-up engine. Hangfire's default recurring behavior applies. If the application is down during a scheduled time, missed occurrences are not backfilled in this phase.

### Scheduling consistency gap

Job persistence and Hangfire recurring registration are **not atomic**. A DB save can succeed while Hangfire registration fails (user receives `SCHEDULE_REGISTRATION_FAILED`). Scheduled **execution creation** uses the transactional outbox; recurring job registration does not.

### Execution enqueue consistency (Phase 7)

Scheduled execution creation and outbox message are committed atomically. If Hangfire is temporarily unavailable, the outbox dispatcher retries publication.

## Hangfire Architecture

```
API (registers recurring jobs + enqueues executions, no worker server)
  ↓
Hangfire PostgreSQL storage
  ↓
Worker process(es) (execute recurring + background jobs)
```

Phase 2 removed the Hangfire worker server from the API process. The API retains Hangfire storage configuration and dashboard (dev only) for visibility. The Worker project is the sole job processor and syncs active job schedules on startup.

## Current Limitations

- External HTTP side effects are not guaranteed exactly once
- Hangfire recurring job registration is not transactional with DB job state
- Running execution cancellation not supported
- No custom missed-schedule catch-up
- Dashboard shows job counts only, not execution metrics
- Failed outbox messages (after max publish attempts) require operational attention

## Failure Scenarios (Phase 7)

| Scenario | Behavior |
|----------|----------|
| DB failure during execution+outbox create | Transaction rolls back — no partial state |
| Hangfire unavailable during dispatch | Outbox stays `Pending`, dispatcher retries with backoff |
| Dispatcher crash after claim | Lease expires, message reclaimed |
| Publish succeeds, mark-Processed fails | Duplicate Hangfire job — safe via atomic claiming |
| Worker crash during HTTP | Stale recovery (Phase 4) still applies |
| Retry + cancel race | Atomic DB transitions — only one wins |
| Cancelled execution reaches worker | No-op before HTTP |

## Observability (Phase 8)

### Liveness vs readiness

- **Liveness** (`GET /health/live`): the process is running. Does not check PostgreSQL.
- **Readiness** (`GET /health/ready`): PostgreSQL and Hangfire storage are reachable.

### Worker heartbeat model

Each Worker process has a unique `WorkerId` and writes to `worker_heartbeats` every ~20 seconds (configurable). Stale if `LastHeartbeatAtUtc` exceeds threshold (default 60s). Records are retained for operational history.

### Outbox health model

Admin system health exposes pending/processing/failed counts and oldest pending age. Thresholds (configurable via `Observability` section):

| Status | Condition |
|--------|-----------|
| Unhealthy | Any failed outbox message, or pending ≥ 500, or oldest pending ≥ 30 min |
| Degraded | Pending ≥ 100, or oldest pending ≥ 5 min |
| Healthy | Otherwise |

### Dashboard aggregation

`GET /api/dashboard/summary` uses database `GROUP BY` and bounded queries (`Take(5000)` for duration, `Take(20)` for recent activity). Success rate = `Succeeded / (Succeeded + Failed)` — **Cancelled excluded** from denominator.

### Ownership boundaries

Dashboard metrics filter by `Job.UserId`. Admin system endpoints require `Admin` role JWT claim.

### Correlation IDs

`RequestCorrelationMiddleware` accepts or generates `X-Request-Id` (GUID), sets `HttpContext.TraceIdentifier`, adds response header, and scopes structured logs.

## Production Security (Phase 9)

### SSRF protection

User-configured HTTP job URLs are validated by `ISsrTargetValidator` at job create/update and again immediately before each HTTP request (including redirect targets).

Blocked targets include:

- Localhost hostnames and loopback addresses (`127.0.0.1`, `::1`, `0.0.0.0`)
- RFC1918 private IPv4 (`10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`)
- Link-local IPv4 (`169.254.0.0/16`, including `169.254.169.254`)
- Private/link-local IPv6 (ULA, link-local)
- Non-HTTP(S) schemes (`file://`, `ftp://`, etc.)

Validation resolves hostnames via DNS and rejects if **any** resolved address is blocked. Literal IP addresses are checked directly.

### Redirect SSRF protection

`HttpJobExecutor` disables automatic redirects (`AllowAutoRedirect = false`) and follows redirects manually (max 3). Each redirect target is SSRF-validated before the next request.

### Secret sanitization

`SensitiveDataSanitizer` redacts sensitive header names/values in API responses and log output. Headers such as `Authorization`, `Cookie`, `X-API-Key`, and names containing `password`, `secret`, or `token` are replaced with `[REDACTED]`.

### Rate limiting

ASP.NET Core built-in rate limiting protects:

| Policy | Endpoints | Default |
|--------|-----------|---------|
| `auth` | `POST /api/auth/register`, `POST /api/auth/login` | 10/min per IP |
| `run-job` | `POST /api/jobs/{id}/run` | 30/min per user |
| `retry` | `POST /api/executions/{id}/retry` | 20/min per user |

Limits are configurable via the `RateLimiting` configuration section. Integration tests use a dedicated `RateLimitTesting` environment with real limits; the `Testing` environment disables limits by default.

### CORS

Production requires explicit `CORS_ALLOWED_ORIGINS`. Development, Testing, and RateLimitTesting allow `http://localhost:3000` and `http://127.0.0.1:3000` by default.

### Security headers

The API adds `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, and a restrictive `Content-Security-Policy` on API responses. The Next.js frontend sets compatible headers via `next.config.ts`.

### Error handling

`GlobalExceptionHandlerMiddleware` returns a consistent JSON error shape with `code`, `message`, and `requestId`. Production responses never expose stack traces, SQL, connection strings, or internal paths.

### Request size limits

- Kestrel max request body: **256 KB**
- Job HTTP payload max: **64 KB** (validated server-side)
- Max custom headers per job: **20**
- Stored HTTP response body max: **1 MB** (truncated)

### Authorization (IDOR)

Cross-user access to jobs, executions, execution history, and dashboard metrics returns **404**. Admin system health (`GET /api/admin/system`) requires `[Authorize(Roles = "Admin")]`.

### Correlation IDs

`X-Request-Id` middleware accepts a valid GUID or generates one. Invalid or oversized values are replaced. The header is always returned on responses and included in structured API logs.

## Docker Deployment (Phase 9)

Full-stack `docker-compose.yml` includes:

| Service | Role |
|---------|------|
| `postgres` | PostgreSQL 16 with `pg_isready` health check |
| `api` | ASP.NET Core API (`/health/live`, `/health/ready`) |
| `worker` | Hangfire worker + outbox dispatcher (health via DB heartbeat) |
| `frontend` | Next.js production build |

Architecture invariant preserved: **API does not execute user HTTP jobs.** Only the Worker processes executions.

Containers run as non-root where practical. Secrets are supplied via environment variables, not baked into images.

## Production Deployment

### Local public demo (ngrok + Docker Compose)

For assignment review, the stack can be exposed with:

1. `docker compose up -d --build`
2. `ngrok start frontend --config ngrok.local.yml` (frontend tunnel on port 3000)

The Next.js frontend proxies `/api/*` and `/health/*` to the internal API service (`API_INTERNAL_URL=http://api:8080`). Browser requests stay same-origin; CORS is not required for proxied API calls.

### Render Blueprint (`render.yaml`)

For persistent cloud deployment:

| Service | Type | Notes |
|---------|------|-------|
| `jobautomation-db` | PostgreSQL | Managed persistent database |
| `jobautomation-api` | Web (Docker) | Migrations on startup; `/health/live` health check |
| `jobautomation-worker` | Worker (Docker) | **Required** for job execution; Starter plan on Render |
| `jobautomation-frontend` | Web (Docker) | Proxies API via `API_INTERNAL_URL` |

Deploy: Render Dashboard → **New Blueprint** → connect GitHub repo → apply.

Set `JWT_SECRET` via Render generated secret. Set `CORS_ALLOWED_ORIGINS` to the frontend `RENDER_EXTERNAL_URL`.

### Architecture invariants (production)

- API does **not** execute user HTTP jobs
- Worker is the sole HTTP job processor
- PostgreSQL remains source of truth
- Hangfire uses PostgreSQL storage
- Transactional outbox remains the dispatch reliability layer

### Known deployment limitations

- ngrok free URLs change when the tunnel restarts unless a reserved domain is configured
- Render free web services may sleep after inactivity (cold start delay)
- Render background workers require a paid Starter plan
- Exactly-once external HTTP side effects are not guaranteed

## Future Improvements

- Missed schedule catch-up / advanced scheduler recovery
- Running execution cancellation via CancellationToken propagation
- Notifications
- CI pipeline with test, lint, and build gates
