using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class UpdateProcessJobsUniqueConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProcessJobs_IdempotencyKey",
                table: "ProcessJobs");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessJobs_DocumentId_IdempotencyKey",
                table: "ProcessJobs",
                columns: new[] { "DocumentId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProcessJobs_DocumentId_IdempotencyKey",
                table: "ProcessJobs");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessJobs_IdempotencyKey",
                table: "ProcessJobs",
                column: "IdempotencyKey",
                unique: true);
        }
    }
}
