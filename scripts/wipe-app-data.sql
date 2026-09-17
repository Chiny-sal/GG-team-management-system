-- Wipe application data for a fresh start.
-- Does NOT drop schema, does NOT touch "__EFMigrationsHistory".
-- Keep "AspNetRoles" (and "AspNetRoleClaims") so the seeder can attach users to Lead/Member.
--
-- Run this in the Supabase SQL Editor, then RESTART the backend so DatabaseSeeder
-- recreates groups, seed members, and Identity logins.

BEGIN;

TRUNCATE TABLE
  "Notifications",
  "ActivityLogEntries",
  "DeletionRequests",
  "WorkItems",
  "WeeklyBoardSnapshots",
  "TopicSuggestions",
  "Meetings",
  "Members",
  "Groups",
  "AspNetUserTokens",
  "AspNetUserLogins",
  "AspNetUserClaims",
  "AspNetUserRoles",
  "AspNetUsers"
RESTART IDENTITY CASCADE;

COMMIT;

-- Hangfire job storage (safe: recurring jobs are re-registered on backend startup).
-- Skip silently if Hangfire has never created its schema.
DO $$
DECLARE
  hangfire_tables text;
BEGIN
  IF NOT EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = 'hangfire') THEN
    RETURN;
  END IF;

  SELECT string_agg(format('%I.%I', table_schema, table_name), ', ')
  INTO hangfire_tables
  FROM information_schema.tables
  WHERE table_schema = 'hangfire'
    AND table_type = 'BASE TABLE';

  IF hangfire_tables IS NOT NULL THEN
    EXECUTE format('TRUNCATE TABLE %s RESTART IDENTITY CASCADE', hangfire_tables);
  END IF;
END $$;
