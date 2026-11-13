using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dogity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClubExams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "club_exams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClubId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    Location = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    JudgeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RegistrationDeadline = table.Column<DateOnly>(type: "date", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_club_exams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_club_exams_clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "club_exam_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegulationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartOrder = table.Column<int>(type: "integer", nullable: false),
                    MemberUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DogId = table.Column<Guid>(type: "uuid", nullable: true),
                    GuestHandlerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GuestDogName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GuestInfo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Result = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    ResultNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    GoalId = table.Column<Guid>(type: "uuid", nullable: true),
                    GoalCreated = table.Column<bool>(type: "boolean", nullable: false),
                    ResultEnteredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResultByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_club_exam_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_club_exam_entries_club_exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "club_exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_club_exam_entries_dogs_DogId",
                        column: x => x.DogId,
                        principalTable: "dogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_club_exam_entries_goals_GoalId",
                        column: x => x.GoalId,
                        principalTable: "goals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_club_exam_entries_regulations_RegulationId",
                        column: x => x.RegulationId,
                        principalTable: "regulations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "club_exam_regulations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegulationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_club_exam_regulations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_club_exam_regulations_club_exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "club_exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_club_exam_regulations_regulations_RegulationId",
                        column: x => x.RegulationId,
                        principalTable: "regulations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_club_exam_entries_DogId",
                table: "club_exam_entries",
                column: "DogId");

            migrationBuilder.CreateIndex(
                name: "IX_club_exam_entries_ExamId_RegulationId_StartOrder",
                table: "club_exam_entries",
                columns: new[] { "ExamId", "RegulationId", "StartOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_club_exam_entries_GoalId",
                table: "club_exam_entries",
                column: "GoalId");

            migrationBuilder.CreateIndex(
                name: "IX_club_exam_entries_MemberUserId",
                table: "club_exam_entries",
                column: "MemberUserId");

            migrationBuilder.CreateIndex(
                name: "IX_club_exam_entries_RegulationId",
                table: "club_exam_entries",
                column: "RegulationId");

            migrationBuilder.CreateIndex(
                name: "IX_club_exam_regulations_ExamId_RegulationId",
                table: "club_exam_regulations",
                columns: new[] { "ExamId", "RegulationId" });

            migrationBuilder.CreateIndex(
                name: "IX_club_exam_regulations_RegulationId",
                table: "club_exam_regulations",
                column: "RegulationId");

            migrationBuilder.CreateIndex(
                name: "IX_club_exams_ClubId_Date",
                table: "club_exams",
                columns: new[] { "ClubId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "club_exam_entries");

            migrationBuilder.DropTable(
                name: "club_exam_regulations");

            migrationBuilder.DropTable(
                name: "club_exams");
        }
    }
}
