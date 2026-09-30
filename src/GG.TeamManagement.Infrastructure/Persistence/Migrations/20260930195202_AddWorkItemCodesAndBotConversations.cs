using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GG.TeamManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemCodesAndBotConversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "work_item_code_seq");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "WorkItems",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    r RECORD;
                    n INT := 0;
                BEGIN
                    FOR r IN SELECT "Id" FROM "WorkItems" ORDER BY "CreatedAt", "Id"
                    LOOP
                        n := n + 1;
                        UPDATE "WorkItems" SET "Code" = 'WORK-' || n WHERE "Id" = r."Id";
                    END LOOP;

                    IF n = 0 THEN
                        PERFORM setval('work_item_code_seq', 1, false);
                    ELSE
                        PERFORM setval('work_item_code_seq', n, true);
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "WorkItems",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false);

            migrationBuilder.CreateTable(
                name: "BotConversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TelegramUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CurrentStep = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Payload = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotConversations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Code",
                table: "WorkItems",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BotConversations_ExpiresAt",
                table: "BotConversations",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_BotConversations_TelegramUserId",
                table: "BotConversations",
                column: "TelegramUserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BotConversations");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_Code",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "WorkItems");

            migrationBuilder.DropSequence(
                name: "work_item_code_seq");
        }
    }
}
