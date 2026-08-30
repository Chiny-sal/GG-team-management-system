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

Copy these into user secrets, environment variables, or `src/GG.TeamManagement.Api/appsettings.Development.json`. Do not commit real values.

| Placeholder / key | Where it is read | Purpose |
| --- | --- | --- |
| `<<SUPABASE_CONNECTION_STRING>>` | `ConnectionStrings:SUPABASE_CONNECTION_STRING` or env `SUPABASE_CONNECTION_STRING` | PostgreSQL pooler connection string (EF Core + Hangfire) |
| `<<TELEGRAM_BOT_TOKEN>>` | `Telegram:BotToken` or env `TELEGRAM_BOT_TOKEN` | Telegram bot token (never hardcoded) |
| `<<PUBLIC_API_URL>>` | `PUBLIC_API_URL` or `Telegram:PublicApiUrl` | Public base URL used to register `POST /api/telegram/webhook` |
| `<<JWT_KEY>>` | `Jwt:Key` or env `JWT_KEY` | HMAC signing key (use a long random string, 32+ characters) |
| `<<SEED_DEFAULT_PASSWORD>>` | `Seed:DefaultPassword` or env `SEED_DEFAULT_PASSWORD` | Password for seeded Identity users (must satisfy Identity rules: 8+ chars, upper, lower, digit) |

Optional:

| Key | Purpose |
| --- | --- |
| `Telegram:WebhookSecret` / `TELEGRAM_WEBHOOK_SECRET` | Sent to Telegram `setWebhook` and checked on `X-Telegram-Bot-Api-Secret-Token` |
| `FRONTEND_ORIGIN` | CORS origin, default `http://localhost:3000` |
| `Jwt:Issuer` / `Jwt:Audience` / `Jwt:ExpiryMinutes` | JWT metadata (defaults are set in `appsettings.json`) |
| `NEXT_PUBLIC_API_URL` | Frontend API + SignalR base URL, default `http://localhost:5145` |

Supabase: use the **pooler** endpoint. Hangfire prefers session-mode pooling (typically pooler port `5432`). Transaction-mode port `6543` can be unreliable for background jobs.

## How to run migrations

From the repository root:

```powershell
dotnet ef migrations add <Name> `
  --project src/GG.TeamManagement.Infrastructure `
  --startup-project src/GG.TeamManagement.Api `
  --output-dir Persistence/Migrations
```

Apply the initial migration (also runs automatically on API startup):

```powershell
$env:SUPABASE_CONNECTION_STRING = "<<SUPABASE_CONNECTION_STRING>>"
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
$env:PUBLIC_API_URL = "<<PUBLIC_API_URL>>"
$env:JWT_KEY = "<<JWT_KEY>>"
$env:SEED_DEFAULT_PASSWORD = "<<SEED_DEFAULT_PASSWORD>>"
dotnet run --project src/GG.TeamManagement.Api --launch-profile http
```

- HTTP: `http://localhost:5145`
- SignalR hub: `http://localhost:5145/hubs/activity-feed` (JWT via `access_token` query)
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

All API endpoints require `[Authorize]` except `POST /api/auth/login` and `POST /api/telegram/webhook`.
