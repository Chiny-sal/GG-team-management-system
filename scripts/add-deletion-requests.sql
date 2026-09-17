-- Two-person deletion approval. Run in the Supabase SQL Editor if you are not
-- applying EF migrations with: dotnet ef database update
-- Safe to re-run: skips when the table already exists.

CREATE TABLE IF NOT EXISTS "DeletionRequests" (
    "Id" uuid NOT NULL,
    "TargetType" integer NOT NULL,
    "TargetId" uuid NOT NULL,
    "TargetName" character varying(200) NOT NULL,
    "RequestedByMemberId" uuid NULL,
    "RequestedAt" timestamp with time zone NOT NULL,
    "Status" integer NOT NULL,
    "ResolvedByMemberId" uuid NULL,
    "ResolvedAt" timestamp with time zone NULL,
    "GroupId" uuid NULL,
    CONSTRAINT "PK_DeletionRequests" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_DeletionRequests_Groups_GroupId" FOREIGN KEY ("GroupId") REFERENCES "Groups" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_DeletionRequests_Members_RequestedByMemberId" FOREIGN KEY ("RequestedByMemberId") REFERENCES "Members" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_DeletionRequests_Members_ResolvedByMemberId" FOREIGN KEY ("ResolvedByMemberId") REFERENCES "Members" ("Id") ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS "IX_DeletionRequests_GroupId" ON "DeletionRequests" ("GroupId");
CREATE INDEX IF NOT EXISTS "IX_DeletionRequests_RequestedByMemberId" ON "DeletionRequests" ("RequestedByMemberId");
CREATE INDEX IF NOT EXISTS "IX_DeletionRequests_ResolvedByMemberId" ON "DeletionRequests" ("ResolvedByMemberId");
CREATE INDEX IF NOT EXISTS "IX_DeletionRequests_Status_Target" ON "DeletionRequests" ("Status", "TargetType", "TargetId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260918010000_AddDeletionRequests', '10.0.11'
WHERE NOT EXISTS (
    SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260918010000_AddDeletionRequests'
);
