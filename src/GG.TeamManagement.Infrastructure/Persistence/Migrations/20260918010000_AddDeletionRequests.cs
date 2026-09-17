using GG.TeamManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GG.TeamManagement.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260918010000_AddDeletionRequests")]
    public partial class AddDeletionRequests : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeletionRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RequestedByMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ResolvedByMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeletionRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeletionRequests_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DeletionRequests_Members_RequestedByMemberId",
                        column: x => x.RequestedByMemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DeletionRequests_Members_ResolvedByMemberId",
                        column: x => x.ResolvedByMemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeletionRequests_GroupId",
                table: "DeletionRequests",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_DeletionRequests_RequestedByMemberId",
                table: "DeletionRequests",
                column: "RequestedByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_DeletionRequests_ResolvedByMemberId",
                table: "DeletionRequests",
                column: "ResolvedByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_DeletionRequests_Status_Target",
                table: "DeletionRequests",
                columns: new[] { "Status", "TargetType", "TargetId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "DeletionRequests");
        }
    }
}
