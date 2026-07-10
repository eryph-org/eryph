using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eryph.StateDb.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddConfigRecordScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ConfigRecords_Domain",
                table: "ConfigRecords");

            migrationBuilder.AddColumn<string>(
                name: "Scope",
                table: "ConfigRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigRecords_Domain_Scope",
                table: "ConfigRecords",
                columns: new[] { "Domain", "Scope" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ConfigRecords_Domain_Scope",
                table: "ConfigRecords");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "ConfigRecords");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigRecords_Domain",
                table: "ConfigRecords",
                column: "Domain",
                unique: true);
        }
    }
}
