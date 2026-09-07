using System;
using GG.TeamManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GG.TeamManagement.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260907193700_AddMeetingNotesAndWorkItemMeetingLink")]
    public partial class AddMeetingNotesAndWorkItemMeetingLink : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Meetings",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "TIMESTAMPTZ '1970-01-01 00:00:00+00'");

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Meetings",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Meetings"
                SET "CreatedAt" = ("ScheduledDate"::timestamp AT TIME ZONE 'UTC')
                WHERE "CreatedAt" = TIMESTAMPTZ '1970-01-01 00:00:00+00';
                """);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Meetings",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "TIMESTAMPTZ '1970-01-01 00:00:00+00'");

            migrationBuilder.AddColumn<Guid>(
                name: "MeetingId",
                table: "WorkItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_MeetingId",
                table: "WorkItems",
                column: "MeetingId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItems_Meetings_MeetingId",
                table: "WorkItems",
                column: "MeetingId",
                principalTable: "Meetings",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkItems_Meetings_MeetingId",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_MeetingId",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "MeetingId",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Meetings");
        }
    }
}
