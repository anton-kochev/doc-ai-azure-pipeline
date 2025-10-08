using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessJobsEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcessJobs",
                columns: table => new
                {
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    Stage = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastErrorCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LastErrorMessage = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CorrelationId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    ExtractionProfile = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Priority = table.Column<byte>(type: "tinyint", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessJobs", x => x.JobId);
                    table.ForeignKey(
                        name: "FK_ProcessJobs_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "DocumentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessJobs_CorrelationId",
                table: "ProcessJobs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessJobs_DocumentId",
                table: "ProcessJobs",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessJobs_IdempotencyKey",
                table: "ProcessJobs",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessJobs_Status_Priority",
                table: "ProcessJobs",
                columns: new[] { "Status", "Priority" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessJobs");
        }
    }
}
