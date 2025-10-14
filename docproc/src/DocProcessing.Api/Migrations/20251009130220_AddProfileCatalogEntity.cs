using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocProcessing.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileCatalogEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProfileCatalogs",
                columns: table => new
                {
                    ProfileName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Version = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    ConfigJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Checksum = table.Column<string>(type: "char(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileCatalogs", x => new { x.ProfileName, x.Version });
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProfileCatalog_ProfileName_IsDefault_Unique",
                table: "ProfileCatalogs",
                columns: new[] { "ProfileName", "IsDefault" },
                unique: true,
                filter: "[Status] = 'Active' AND [IsDefault] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ProfileCatalog_Status",
                table: "ProfileCatalogs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProfileCatalogs");
        }
    }
}
