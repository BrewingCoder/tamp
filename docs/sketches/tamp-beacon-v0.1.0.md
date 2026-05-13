# `tamp-beacon` — v0.1.0 sketch (single-image OTel receiver + dashboard)

**Status:** directional sketch (TAM-128). Captures v0.1.0 scope and the extension points we leave reserved. Lives under `docs/sketches/` because the scope firms up only after v0.1.0 is in adopters' hands.

**Predecessor work:** ADR 0018 (Tamp.Core 1.4.0) pinned what Tamp builds emit — three `ActivitySource`s + one `Meter` with ~80 tags. `tamp-beacon` consumes exactly that contract; no Core changes needed.

## North star

A team runs `docker run -p 4318:4318 -v ./beacon-data:/var/lib/tamp-beacon ghcr.io/tamp-build/tamp-beacon:latest`, points every developer's and CI runner's Tamp at the resulting `OTEL_EXPORTER_OTLP_ENDPOINT`, and gets:
- a queryable history of every build run by the team, by repo, by target;
- a browser dashboard that ranks slowest targets, surfaces flaky tests, charts allocation regressions;
- a phone-notification ping on build failure (Web Push, opt-in per device);
- nothing else to operate — one container, one SQLite file, backed up by `cp`.

## v0.1.0 scope ceiling — single image, polling dashboard, Web Push

**In:**
- Single Docker image `ghcr.io/tamp-build/tamp-beacon:0.1.0`.
- **OTLP/HTTP-JSON receiver** on `/v1/traces` and `/v1/metrics` (the canonical wire — easy to test from curl, friendly with corp proxies, no protobuf-codegen surprises). gRPC receiver reserved for 0.2.0.
- SQLite storage at `/var/lib/tamp-beacon/db.sqlite`. Schema sketched below.
- HTTP/JSON query API under `/api/*`.
- React + Vite + Tailwind + shadcn/ui SPA served from `wwwroot/`. Polls the query API via React Query.
- Web Push subscriptions for build-failure alerts. VAPID-keyed; subscription stored in SQLite per device.
- Filters by project + area (the `tamp.build.project.name` / `area` tags ADR 0018 declared) so the polyrepo case Just Works.
- Authless v0.1.0 — designed for trusted-network deployment behind a reverse proxy / Cloudflare tunnel. Token auth reserved for 0.2.0.

**Out — explicitly deferred for v0.1.0:**

- **Public-CI telemetry pipe.** GitHub Actions public runners can't reach a self-hosted beacon's ingress without a tunnel + auth story. v0.1.0 is **local-only exercise**: developer workstations + private/self-hosted runners point at the beacon; public CI workflows skip the OTel exporter env var. The Cloudflare-tunnel + token-auth path lands in a later wave alongside the `Tamp.Otel` satellite's auth options.

**Out — deferred (other):**
- OTLP/gRPC receiver (HTTP-JSON covers the primary path; gRPC adds protobuf-stack debt for a marginal perf win).
- Multi-tenant separation. v0.1.0 assumes one tenant per container.
- Long-term retention policies. v0.1.0 stores every row; operators rotate by `mv db.sqlite db-2026.sqlite`.
- Aggregation/rollup jobs. v0.1.0 queries fresh on every dashboard load (cheap at the row counts we see).
- General-purpose OTel collector. Beacon listens only on Tamp's source-name prefix; non-Tamp telemetry is rejected at ingress (status: 422, body: "this is tamp-beacon; route non-Tamp telemetry to your collector of choice").
- SSO / OIDC / SAML. Reverse-proxy-bolted-on if needed.
- SSE / WebSocket dashboard streaming. Polling-only per the established no-streaming policy.
- ML-powered "is this build slower than usual" anomaly detection. Charts now; ML later.
- Webhook integrations (Slack, Teams). Web Push is the v0.1.0 alert path; webhooks reserved.
- Trace context propagation from external systems. Beacon is a receiver, not a propagator.

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  Single Docker image  ghcr.io/tamp-build/tamp-beacon:0.1.0  │
│                                                             │
│  ┌────────────────────────────────────────────────────┐     │
│  │  .NET 10 host (Kestrel)                            │     │
│  │   ├─ POST /v1/traces       (OTLP/HTTP-JSON)        │     │
│  │   ├─ POST /v1/metrics      (OTLP/HTTP-JSON)        │     │
│  │   ├─ GET  /api/builds      (filterable, paginated) │     │
│  │   ├─ GET  /api/builds/{id} (build + targets + cmds)│     │
│  │   ├─ GET  /api/projects    (distinct names+areas)  │     │
│  │   ├─ POST /api/push/subscribe (VAPID register)     │     │
│  │   ├─ /  ←── wwwroot/ (React SPA, static-served)    │     │
│  │   └─ /healthz                                      │     │
│  │                                                    │     │
│  │  ┌──────────────────────────────────────────────┐  │     │
│  │  │ SQLite — /var/lib/tamp-beacon/db.sqlite      │  │     │
│  │  │  builds, targets, commands, events, push_subs│  │     │
│  │  └──────────────────────────────────────────────┘  │     │
│  └────────────────────────────────────────────────────┘     │
└─────────────────────────────────────────────────────────────┘
```

Port: **4318** (the OTLP/HTTP-JSON default). Dashboard on same port at `/`.

### Backend

- .NET 10 minimal-API host. Single binary via `dotnet publish -c Release -p:PublishAot=false` (start with self-contained; AOT later if startup matters).
- **EF Core 10 + Microsoft.Data.Sqlite** for storage. Migrations bundled in the image; applied on boot.
- **Dapper** for hot read paths (the dashboard queries).
- **`webpush` library** for Web Push delivery (or hand-rolled — the protocol is RFC 8030 + RFC 8291 + RFC 8292; a few hundred lines).
- OTLP/HTTP-JSON request handling: parse JSON envelope (well-known OTLP schema), extract spans/metrics, validate that source names start with `Tamp.Build`, persist.
- Background failure-alert worker: on inserting a build span with `outcome=failure`, enqueue a push to every subscription whose project/area matches; pop the queue every ~1s.

### Frontend

- **React + Vite + Tailwind + shadcn/ui.** Stack matches DoTrack/HoldFast/Some-Error — same component library, same Tailwind tokens already mapped in those projects' `index.css`.
- React Query for data fetching. Polling on a 5s default cadence, configurable per view.
- `wwwroot/` shipped in the .NET host's output via the build script copying Vite's `dist/` after `Yarn.Run("build")`.
- Service worker registered for Web Push subscriptions.

**Pages (v0.1.0):**
1. **Builds** — recent builds list, filter by project/area/outcome/branch/time-range. Click → detail.
2. **Build detail** — header (project, target list, exit code, durations, host facets), expandable target spans with their commands, summary event, stdout/stderr byte sparkline.
3. **Targets** — slowest, most-failing, highest-allocating targets across the time range. Grouped by `target.name`.
4. **Projects** — list of distinct `project.name`s with last-seen + counts.
5. **Alerts** — Web Push subscription management for the current device.

(One scoped chart per page, no dashboard customization — that's 0.2.0.)

### Real-time

- Dashboard refresh: React Query polling every 5s on Builds; every 15s on slower-changing views. Monotonic `seq` column on each row so the client can ask `/api/builds?since_seq=N` and only get deltas.
- Failure alerts: Web Push subscription (`POST /api/push/subscribe`) with VAPID keys baked into the image at first boot (regenerated if `/var/lib/tamp-beacon/vapid.key` doesn't exist). On a failure build write, server iterates matching subscriptions and POSTs the push body to each subscription's endpoint. Standard RFC 8030 path — fully testable with `curl` against a captured subscription.

## Storage — SQLite schema sketch

```sql
-- One row per build (TampBuild.Execute<T> invocation).
CREATE TABLE builds (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    seq             INTEGER NOT NULL UNIQUE,                    -- monotonic, dashboard-deltas
    project_name    TEXT NOT NULL,
    project_area    TEXT,
    cli_version     TEXT,
    started_unix_ns INTEGER NOT NULL,
    duration_ns     INTEGER NOT NULL,
    exit_code       INTEGER NOT NULL,
    outcome         TEXT NOT NULL,                              -- success / failure
    targets_total   INTEGER NOT NULL,
    targets_failed  INTEGER NOT NULL,
    commands_total  INTEGER NOT NULL,
    failure_target  TEXT,
    host_os         TEXT,
    host_arch       TEXT,
    ci_vendor       TEXT,
    peak_memory_b   INTEGER NOT NULL,
    raw_tags        TEXT NOT NULL                                -- JSON: full tag dict for any future-tag we forgot to index
);
CREATE INDEX ix_builds_seq ON builds(seq);
CREATE INDEX ix_builds_started ON builds(started_unix_ns);
CREATE INDEX ix_builds_project ON builds(project_name, project_area);
CREATE INDEX ix_builds_outcome ON builds(outcome, started_unix_ns);

-- One row per target span.
CREATE TABLE targets (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    build_id      INTEGER NOT NULL REFERENCES builds(id) ON DELETE CASCADE,
    name          TEXT NOT NULL,
    phase         TEXT,
    status        TEXT NOT NULL,                                -- success / failure / skipped / not_run
    started_unix_ns INTEGER NOT NULL,
    duration_ns   INTEGER NOT NULL,
    cpu_time_ms   REAL NOT NULL,
    gc_allocated_b INTEGER NOT NULL,
    gc_gen0       INTEGER NOT NULL,
    gc_gen1       INTEGER NOT NULL,
    gc_gen2       INTEGER NOT NULL,
    commands_count INTEGER NOT NULL,
    raw_tags      TEXT NOT NULL
);
CREATE INDEX ix_targets_build ON targets(build_id);
CREATE INDEX ix_targets_name ON targets(name);

-- One row per command span.
CREATE TABLE commands (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    target_id     INTEGER NOT NULL REFERENCES targets(id) ON DELETE CASCADE,
    executable    TEXT NOT NULL,
    args_count    INTEGER NOT NULL,
    exit_code     INTEGER NOT NULL,
    duration_ns   INTEGER NOT NULL,
    cpu_total_ms  REAL NOT NULL,
    peak_memory_b INTEGER NOT NULL,
    stdout_bytes  INTEGER NOT NULL,
    stderr_bytes  INTEGER NOT NULL,
    raw_tags      TEXT NOT NULL
);
CREATE INDEX ix_commands_target ON commands(target_id);
CREATE INDEX ix_commands_exe ON commands(executable);

-- Generic span event sink (build summaries, failure handler invocations, retries).
CREATE TABLE events (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    build_id      INTEGER NOT NULL REFERENCES builds(id) ON DELETE CASCADE,
    target_id     INTEGER REFERENCES targets(id) ON DELETE CASCADE,
    command_id    INTEGER REFERENCES commands(id) ON DELETE CASCADE,
    name          TEXT NOT NULL,                                -- tamp.build.summary, tamp.target.retry.attempt, etc.
    at_unix_ns    INTEGER NOT NULL,
    raw_tags      TEXT NOT NULL
);

-- Web Push subscriptions, one row per registered device.
CREATE TABLE push_subscriptions (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    endpoint      TEXT NOT NULL UNIQUE,
    p256dh        TEXT NOT NULL,
    auth          TEXT NOT NULL,
    project_filter TEXT,                                        -- null = all projects
    area_filter   TEXT,                                         -- null = all areas
    created_unix_ns INTEGER NOT NULL
);
```

`raw_tags` columns let new ADR-0018 tags flow through without a migration; the indexed columns are the ones the dashboard filters on hot. Acceptable trade for v0.1.0; rebalance if a query gets popular enough to deserve a column.

## API shape (JSON)

```
GET /api/builds?project=HoldFast&area=frontend&since_seq=12345&limit=50
    → { builds: [{id, seq, project_name, project_area, outcome, duration_ns, ...}], next_seq: 12410 }

GET /api/builds/{id}
    → { build, targets: [{...}], commands: [{target_id, executable, exit_code, ...}], events: [{...}] }

GET /api/projects
    → { projects: [{name, area, last_seen_unix_ns, builds_count}] }

GET /api/targets/slowest?project=HoldFast&since_unix_ns=...&limit=20
    → { targets: [{name, project_name, avg_duration_ns, p95_duration_ns, samples}] }

GET /api/targets/flakiest?project=HoldFast&since_unix_ns=...&limit=20
    → { targets: [{name, project_name, fail_rate, samples}] }

POST /api/push/subscribe
    { endpoint, keys: {p256dh, auth}, project_filter?, area_filter? }
    → 201

GET /healthz
    → 200 { status: "ok", db_path, rows_total, vapid_public_key }
```

## Single-image build

The repo's own Build.cs uses the Tamp ecosystem end-to-end:

```csharp
Target FrontendBuild => _ => _
    .Internal()
    .Executes(() => Yarn.Run(YarnTool, s => s.SetWorkingDirectory(WebDir).SetScript("build")));

Target Compile => _ => _
    .DependsOn(Restore, FrontendBuild)
    .Executes(() => DotNet.Build(...))
    .Executes(() => CopyVitistOutputIntoWwwroot());

Target DockerBuild => _ => _
    .DependsOn(Test)
    .Executes(() => Docker.Build(s => s
        .SetContext(RootDirectory)
        .AddTag($"ghcr.io/tamp-build/tamp-beacon:{ImageTag}")
        .AddPlatform("linux/amd64")
        .AddPlatform("linux/arm64")
        .SetPush(IsTagBuild)));

Target SmokeQa => _ => _
    .DependsOn(DockerBuild)
    .Executes(async () =>
    {
        // Start the image, wait for /healthz, post a sample OTLP payload, query /api/builds, assert.
        // Uses HttpProbe.WaitForHealthy (Tamp.Http 0.1.1+).
    });
```

Dogfoods `Tamp.Yarn.V4`, `Tamp.Vite.V5`, `Tamp.NetCli.V10`, `Tamp.Docker.V27`, `Tamp.Http` — every package the beacon needs already on nuget.

Dockerfile shape:

```dockerfile
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-jammy-chiseled
WORKDIR /app
COPY publish/ .
EXPOSE 4318
VOLUME ["/var/lib/tamp-beacon"]
HEALTHCHECK --interval=30s CMD curl -fsS http://localhost:4318/healthz || exit 1
ENTRYPOINT ["./Tamp.Beacon"]
```

Chiseled base = ~50 MB. .NET 10 self-contained binary + wwwroot + SQLite native = ~120 MB total image, give or take. Small enough.

## Web Push flow

1. First page load → service worker registered, subscription requested with VAPID public key (served by `/healthz` so it's discoverable).
2. User permits → browser hands back `{endpoint, keys: {p256dh, auth}}` → SPA POSTs to `/api/push/subscribe` with optional project/area filters.
3. Server inserts row in `push_subscriptions`.
4. On a build write where `outcome=failure`, server iterates matching subscriptions, encrypts the payload (RFC 8291), POSTs to each subscription's endpoint with VAPID auth header (RFC 8292). Standard library or a ~200-line in-house impl.
5. Service worker on the device receives the push → shows native notification → click goes to `/builds/{id}` deep link.

VAPID keys: generated on first boot, stored under `/var/lib/tamp-beacon/vapid.key`. Backed up with the SQLite file via `cp`.

## Repo layout

```
tamp-build/tamp-beacon/
├── src/
│   ├── Tamp.Beacon/                # .NET host
│   └── Tamp.Beacon.Sdk/            # shared types (receive schemas; published as a satellite NuGet for adopters who want them)
├── web/                            # React SPA
│   ├── package.json
│   ├── vite.config.ts
│   └── src/...
├── tests/
│   └── Tamp.Beacon.Tests/
├── build/Build.cs                  # dogfooded
├── Dockerfile
└── .github/workflows/
    ├── ci.yml                      # build + test on PR
    └── release.yml                 # tag-triggered Docker build+push + (optional) Tamp.Beacon.Sdk nupkg push
```

## Extension points reserved for 0.2.0+

- OTLP/gRPC receiver alongside HTTP-JSON
- Token auth (header-based, configurable via env var)
- Per-tenant separation (one DB file per tenant, namespaced API)
- Retention/rollup background jobs
- Webhook outputs (Slack, Teams, generic HTTP)
- "Anomaly" overlay on charts (this build is >2σ slower than the rolling p50)
- Adopter-defined custom views via saved query JSON
- `Tamp.Beacon.Sdk` as a strongly-typed C# client for adopters who want first-class build-history queries inside their own Build.cs

## Tests + smoke

- Unit tests: OTLP-JSON parser against fixture payloads (taken from a real Tamp build piped through `OTEL_EXPORTER_OTLP_ENDPOINT=http://...`), storage round-trips, push-payload encryption.
- Integration: TestContainers-driven Docker-image smoke (start the image, post traces, query, assert).
- Manual sanity: run `tamp Ci` in tamp-core against a local beacon, eyeball the dashboard.

## What ships in v0.1.0

- `tamp-build/tamp-beacon` repo
- Docker image `ghcr.io/tamp-build/tamp-beacon:0.1.0`
- Optional `Tamp.Beacon.Sdk` NuGet package (the schemas) if anyone wants strongly-typed clients
- README with the three-line on-ramp:

  ```bash
  mkdir -p ./beacon-data
  docker run -d -p 4318:4318 -v $PWD/beacon-data:/var/lib/tamp-beacon ghcr.io/tamp-build/tamp-beacon:0.1.0
  export OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4318
  ```

  Plus a Tamp wiki page (`Observing-Your-Builds`) covering `Tamp.Otel` SDK install + beacon URL config + dashboard tour.

## Open questions to answer during implementation

1. **OTLP/HTTP-JSON via `Tamp.Otel` satellite or via direct `OpenTelemetry.*` SDK?** Adopters need an `AddTamp()` extension on `TracerProviderBuilder`. The `Tamp.Otel` satellite (forthcoming, scoped TAM-129?) is the natural home — but tamp-beacon can ship a one-page README workaround until that satellite lands.
2. **Concurrent writers?** SQLite handles modest concurrency via WAL mode; we'll set `journal_mode=WAL` and `synchronous=NORMAL`. Higher-throughput tenants get a queueing layer in 0.2.0 if needed.
3. **Image registry?** GitHub Container Registry (ghcr.io) under `tamp-build` — already auth'd via the same workflow that pushes nupkgs.
4. **Failure-alert rate limiting?** A flaky build that fails 50 times in a row should not push 50 notifications. Coalesce by `project + target + 5-min window` — implementation detail; design for it from the start.

## Pre-implementation tasks

When ready, file as child tickets under TAM-128:

1. Scaffold `tamp-build/tamp-beacon` (csproj, Dockerfile, Build.cs, CI/Release workflows)
2. Schema + migrations + EF Core model
3. OTLP/HTTP-JSON receiver (parser + persistence)
4. Query API
5. React SPA scaffold (Vite + shadcn/ui + React Query + Tailwind)
6. Web Push (VAPID + subscription + delivery worker)
7. Dockerfile + multi-arch image build
8. Smoke test against real Tamp output
9. README + wiki Observing-Your-Builds page

Plus probably **TAM-129 — `Tamp.Otel`** (the OTel-SDK satellite) needs to land in parallel so beacon has something to receive from. ~80-line package.
