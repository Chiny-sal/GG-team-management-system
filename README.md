# GG Team Management System

Community activity-tracking app: weekly group boards, live activity feed, Telegram topic suggestions, and Hangfire follow-through alerts.

## Stack

- Backend: ASP.NET Core / .NET 10, Clean Architecture (Domain, Application, Infrastructure, API)
- PostgreSQL via Supabase pooler + EF Core code-first migrations
- Hangfire + Hangfire.PostgreSql (persistent storage)
- SignalR activity feed
- ASP.NET Identity + JWT (`Lead`, `Member`)
- Telegram.Bot webhook
- ClosedXML Excel export
- Frontend: Next.js, React, TypeScript, Tailwind CSS, dnd-kit

## Environment variables and placeholders

Set these as **flat environment variables** with the exact names below (never nested keys like `Jwt:Key`). The API reads them only through `AppEnvironment`. Required variables are checked at startup; if any are missing the process exits with a single message listing the exact names.

| Environment variable | Required | Purpose |
| --- | --- | --- |
| `SUPABASE_CONNECTION_STRING` | Yes (runtime + `dotnet ef`) | PostgreSQL pooler connection string (EF Core + Hangfire) |
| `JWT_KEY` | Yes (runtime) | HMAC signing key (32+ random characters) |
| `TELEGRAM_BOT_TOKEN` | No (webhook skipped) | Telegram bot token |
| `PUBLIC_API_URL` | No (webhook skipped) | Public API base URL for `POST /api/telegram/webhook` |
| `SEED_DEFAULT_PASSWORD` | No (identity users skipped) | Seeded Identity password (8+ chars, upper, lower, digit) |

Optional extras (also flat names): `API_URL` (backend listen URL, default `http://localhost:7223`), `TELEGRAM_WEBHOOK_SECRET`, `FRONTEND_ORIGIN`, `JWT_ISSUER`, `JWT_AUDIENCE`, `JWT_EXPIRY_MINUTES`, `NEXT_PUBLIC_API_URL` (frontend; must match `API_URL`).

## Supabase pooler ports (required)

Supabase exposes two pooler modes on the same host. Use the **correct port for the job**:

| Use | Port | Mode | When |
| --- | --- | --- | --- |
| App runtime (`dotnet run`, Hangfire, API) | **6543** | Transaction pooler | Everyday traffic |
| Migrations (`dotnet ef database update`) | **5432** | Session pooler | Schema changes only |

EF’s migration-history check needs a session-scoped connection. The transaction pooler on **6543** does not support that reliably, which is why the API never calls `MigrateAsync()` on startup.

**Runtime connection string (port 6543):**

```
Host=aws-0-<region>.pooler.supabase.com;Port=6543;Database=postgres;Username=postgres.<project-ref>;Password=<password>;SSL Mode=Require;Trust Server Certificate=true
```

**Migration connection string (port 5432):**

```
Host=aws-0-<region>.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.<project-ref>;Password=<password>;SSL Mode=Require;Trust Server Certificate=true
```

Swap only the port (and keep `Username=postgres.<project-ref>`, not `postgres` alone). Set `$env:SUPABASE_CONNECTION_STRING` to the 5432 string **before** `dotnet ef database update`, then switch it back to 6543 before `dotnet run`.

## How to run migrations

From the repository root:

```powershell
dotnet ef migrations add <Name> `
  --project src/GG.TeamManagement.Infrastructure `
  --startup-project src/GG.TeamManagement.Api `
  --output-dir Persistence/Migrations
```

Apply migrations explicitly (never from app startup). Use the **session pooler (port 5432)** string:

```powershell
$env:SUPABASE_CONNECTION_STRING = "Host=...pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.<project-ref>;Password=<password>;SSL Mode=Require;Trust Server Certificate=true"
dotnet ef database update `
  --project src/GG.TeamManagement.Infrastructure `
  --startup-project src/GG.TeamManagement.Api
```

The initial migration is `src/GG.TeamManagement.Infrastructure/Persistence/Migrations/20260830184901_InitialCreate.cs`. It creates all domain tables plus ASP.NET Identity tables.

Startup seed creates:

- Office Management (`IsOfficeManagementTeam = true`)
- Sub-Group 1–4
- One Lead and five Member domain rows
- Identity users (only when `SEED_DEFAULT_PASSWORD` is set): `lead@gg.local`, `office.member@gg.local`, `sg1.member@gg.local` … `sg4.member@gg.local`

Link a Member to Telegram by setting `Members.TelegramUserId` to their numeric Telegram user id (as a string). Text messages from that account become `TopicSuggestion` rows.

## Run the API

```powershell
$env:SUPABASE_CONNECTION_STRING = "<<SUPABASE_CONNECTION_STRING>>"
$env:TELEGRAM_BOT_TOKEN = "<<TELEGRAM_BOT_TOKEN>>"
$env:API_URL = "http://localhost:7223"
$env:PUBLIC_API_URL = "<<PUBLIC_API_URL>>"
$env:JWT_KEY = "<<JWT_KEY>>"
$env:SEED_DEFAULT_PASSWORD = "<<SEED_DEFAULT_PASSWORD>>"
dotnet run --project src/GG.TeamManagement.Api --launch-profile http
```

The listen address is `API_URL` (default `http://localhost:7223`). The backend logs `API listening on: ...` as soon as Kestrel binds, before seeding finishes.

- HTTP: `http://localhost:7223` (set `API_URL` to change this; frontend `NEXT_PUBLIC_API_URL` must match)
- Health: `GET http://localhost:7223/health` (anonymous; checks database connectivity)
- SignalR hub: `http://localhost:7223/hubs/activity-feed` (JWT via `access_token` query)
- Hangfire dashboard: `/hangfire` (Lead JWT required)
- Telegram webhook: `POST /api/telegram/webhook` (anonymous; Telegram calls this)

On startup the API registers the webhook at `<<PUBLIC_API_URL>>/api/telegram/webhook`. You can also run:

```powershell
.\scripts\set-telegram-webhook.ps1
```

## Recurring jobs

1. Daily: notify members with no `WorkItem` assigned to them created in the last 14 days (`MemberNoAssignmentTwoWeeks`, at most one per member per week).
2. Daily: notify for work items with `Deadline` older than 14 days that are not `Done` (`WorkNotDoneTwoWeeks`, once per work item).
3. Weekly Monday 00:00: refresh the cached Monday-anchored `WeekId` (work items are **not** auto-created).

## Run the frontend

```powershell
cd frontend
copy .env.local.example .env.local
npm install
npm run dev
```

Open `http://localhost:3000`.

- `/` Dashboard (live via SignalR)
- `/activity` Activity log (live, paginated)
- `/notifications` Unread notifications grouped by type
- `/board/[groupId]` Kanban with dnd-kit; Save locks the week; Export downloads `.xlsx`

## Access rules

- **Lead**: create/assign/edit work in any group, save/lock boards, promote Telegram suggestions.
- **Member**: view their group board and drag only their own cards between status columns. They cannot reassign work.

All API endpoints require `[Authorize]` except `POST /api/auth/login`, `POST /api/telegram/webhook`, and `GET /health`.

## Common errors

| Symptom | One-line fix |
| --- | --- |
| `Format of the initialization string does not conform to specification` / Npgsql parse error | Use semicolon-separated ADO.NET keys (`Host=...;Port=...;Database=...;Username=...;Password=...;SSL Mode=Require`). Do not paste a `postgres://` URI unless you convert it. Username must be `postgres.<project-ref>`, not `postgres`. |
| Timeout or hang during `dotnet ef database update` / migration history lock | You are on port **6543**. Set `SUPABASE_CONNECTION_STRING` to the **session pooler (Port=5432)** and rerun `dotnet ef database update`. |
| Browser login `Failed to fetch` / 0 bytes | Frontend `NEXT_PUBLIC_API_URL` must equal backend `API_URL` (default `http://localhost:7223`). Restart `npm run dev` after changing `.env.local`. Wait for console line `API listening on: ...` before signing in. |
