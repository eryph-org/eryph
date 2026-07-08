using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eryph.StateDb.MySql.Migrations
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
                type: "longtext",
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");
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
