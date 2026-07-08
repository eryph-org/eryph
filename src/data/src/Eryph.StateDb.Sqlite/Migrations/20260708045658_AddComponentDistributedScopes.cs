using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eryph.StateDb.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddComponentDistributedScopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DistributedConfigScopesJson",
                table: "ComponentRegistrations",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DistributedConfigScopesJson",
                table: "ComponentRegistrations");
        }
    }
}
