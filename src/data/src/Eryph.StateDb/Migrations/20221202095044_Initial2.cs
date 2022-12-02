using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eryph.StateDb.Migrations
{
    public partial class Initial2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NetworkPorts_VirtualNetworks_VirtualNetworkPort_NetworkId",
                table: "NetworkPorts");

            migrationBuilder.DropIndex(
                name: "IX_NetworkPorts_NetworkId",
                table: "NetworkPorts");

            migrationBuilder.DropIndex(
                name: "IX_NetworkPorts_VirtualNetworkPort_NetworkId",
                table: "NetworkPorts");

            migrationBuilder.RenameColumn(
                name: "VirtualNetworkPort_NetworkId",
                table: "NetworkPorts",
                newName: "RoutedNetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkPorts_NetworkId",
                table: "NetworkPorts",
                column: "NetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkPorts_RoutedNetworkId",
                table: "NetworkPorts",
                column: "RoutedNetworkId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_NetworkPorts_VirtualNetworks_RoutedNetworkId",
                table: "NetworkPorts",
                column: "RoutedNetworkId",
                principalTable: "VirtualNetworks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NetworkPorts_VirtualNetworks_RoutedNetworkId",
                table: "NetworkPorts");

            migrationBuilder.DropIndex(
                name: "IX_NetworkPorts_NetworkId",
                table: "NetworkPorts");

            migrationBuilder.DropIndex(
                name: "IX_NetworkPorts_RoutedNetworkId",
                table: "NetworkPorts");

            migrationBuilder.RenameColumn(
                name: "RoutedNetworkId",
                table: "NetworkPorts",
                newName: "VirtualNetworkPort_NetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkPorts_NetworkId",
                table: "NetworkPorts",
                column: "NetworkId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkPorts_VirtualNetworkPort_NetworkId",
                table: "NetworkPorts",
                column: "VirtualNetworkPort_NetworkId");

            migrationBuilder.AddForeignKey(
                name: "FK_NetworkPorts_VirtualNetworks_VirtualNetworkPort_NetworkId",
                table: "NetworkPorts",
                column: "VirtualNetworkPort_NetworkId",
                principalTable: "VirtualNetworks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
