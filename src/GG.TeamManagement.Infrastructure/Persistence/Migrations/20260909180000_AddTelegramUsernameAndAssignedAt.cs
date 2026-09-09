using System;
using GG.TeamManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GG.TeamManagement.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260909180000_AddTelegramUsernameAndAssignedAt")]
    public partial class AddTelegramUsernameAndAssignedAt : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TelegramUsername",
                table: "Members",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AssignedAt",
                table: "WorkItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "WorkItems"
                SET "AssignedAt" = "CreatedAt"
                WHERE "AssignedMemberId" IS NOT NULL AND "AssignedAt" IS NULL;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TelegramUsername",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "AssignedAt",
                table: "WorkItems");
        }
    }
}
