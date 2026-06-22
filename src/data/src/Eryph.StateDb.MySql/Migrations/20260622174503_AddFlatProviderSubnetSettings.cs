using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eryph.StateDb.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddFlatProviderSubnetSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Gateway",
                table: "Subnet",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ProviderSubnet_DnsServersV4",
                table: "Subnet",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "ProviderSubnet_MTU",
                table: "Subnet",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Gateway",
                table: "Subnet");

            migrationBuilder.DropColumn(
                name: "ProviderSubnet_DnsServersV4",
                table: "Subnet");

            migrationBuilder.DropColumn(
                name: "ProviderSubnet_MTU",
                table: "Subnet");
        }
    }
}
