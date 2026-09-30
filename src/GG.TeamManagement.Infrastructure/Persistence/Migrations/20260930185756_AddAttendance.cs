using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GG.TeamManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_DeletionRequests_Status_Target",
                table: "DeletionRequests",
                newName: "IX_DeletionRequests_Status_TargetType_TargetId");

            migrationBuilder.CreateTable(
                name: "MeetingAttendances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedByMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingAttendances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingAttendances_Members_CreatedByMemberId",
                        column: x => x.CreatedByMemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingAttendanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    Present = table.Column<bool>(type: "boolean", nullable: false),
                    AttendedWeeklyClass = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_MeetingAttendances_MeetingAttendanceId",
                        column: x => x.MeetingAttendanceId,
                        principalTable: "MeetingAttendances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingAttendanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomQuestions_MeetingAttendances_MeetingAttendanceId",
                        column: x => x.MeetingAttendanceId,
                        principalTable: "MeetingAttendances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttendanceRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomQuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceAnswers_AttendanceRecords_AttendanceRecordId",
                        column: x => x.AttendanceRecordId,
                        principalTable: "AttendanceRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttendanceAnswers_CustomQuestions_CustomQuestionId",
                        column: x => x.CustomQuestionId,
                        principalTable: "CustomQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceAnswers_AttendanceRecordId_CustomQuestionId",
                table: "AttendanceAnswers",
                columns: new[] { "AttendanceRecordId", "CustomQuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceAnswers_CustomQuestionId",
                table: "AttendanceAnswers",
                column: "CustomQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_MeetingAttendanceId_MemberId",
                table: "AttendanceRecords",
                columns: new[] { "MeetingAttendanceId", "MemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_MemberId",
                table: "AttendanceRecords",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomQuestions_MeetingAttendanceId",
                table: "CustomQuestions",
                column: "MeetingAttendanceId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingAttendances_CreatedByMemberId",
                table: "MeetingAttendances",
                column: "CreatedByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingAttendances_Date",
                table: "MeetingAttendances",
                column: "Date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceAnswers");

            migrationBuilder.DropTable(
                name: "AttendanceRecords");

            migrationBuilder.DropTable(
                name: "CustomQuestions");

            migrationBuilder.DropTable(
                name: "MeetingAttendances");

            migrationBuilder.RenameIndex(
                name: "IX_DeletionRequests_Status_TargetType_TargetId",
                table: "DeletionRequests",
                newName: "IX_DeletionRequests_Status_Target");
        }
    }
}
