using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eryph.StateDb.Sqlite.Migrations
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
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderSubnet_DnsServersV4",
                table: "Subnet",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProviderSubnet_MTU",
                table: "Subnet",
                type: "INTEGER",
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
