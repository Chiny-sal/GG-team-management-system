using System;
using GG.TeamManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GG.TeamManagement.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260909190000_AddActivityGroupId")]
    public partial class AddActivityGroupId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GroupId",
                table: "ActivityLogEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogEntries_GroupId",
                table: "ActivityLogEntries",
                column: "GroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_ActivityLogEntries_Groups_GroupId",
                table: "ActivityLogEntries",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.Sql("""
                UPDATE "ActivityLogEntries" AS e
                SET "GroupId" = w."GroupId"
                FROM "WorkItems" AS w
                WHERE e."EntityType" = 'WorkItem' AND e."EntityId" = w."Id";
                """);

            migrationBuilder.Sql("""
                UPDATE "ActivityLogEntries" AS e
                SET "GroupId" = m."GroupId"
                FROM "Members" AS m
                WHERE e."EntityType" = 'Member' AND e."EntityId" = m."Id";
                """);

            migrationBuilder.Sql("""
                UPDATE "ActivityLogEntries" AS e
                SET "GroupId" = COALESCE(mem."GroupId", w."GroupId")
                FROM "Notifications" AS n
                LEFT JOIN "Members" AS mem ON n."MemberId" = mem."Id"
                LEFT JOIN "WorkItems" AS w ON n."WorkItemId" = w."Id"
                WHERE e."EntityType" = 'Notification' AND e."EntityId" = n."Id";
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActivityLogEntries_Groups_GroupId",
                table: "ActivityLogEntries");

            migrationBuilder.DropIndex(
                name: "IX_ActivityLogEntries_GroupId",
                table: "ActivityLogEntries");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "ActivityLogEntries");
        }
    }
}
