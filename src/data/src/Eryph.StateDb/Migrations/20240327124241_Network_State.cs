using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eryph.StateDb.Migrations
{
    public partial class Network_State : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NetworkPorts_Catlets_CatletId",
                table: "NetworkPorts");

            migrationBuilder.DropIndex(
                name: "IX_NetworkPorts_CatletId",
                table: "NetworkPorts");

            migrationBuilder.DropColumn(
                name: "CatletId",
                table: "NetworkPorts");

            migrationBuilder.AddColumn<Guid>(
                name: "CatletMetadataId",
                table: "NetworkPorts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkPorts_CatletMetadataId",
                table: "NetworkPorts",
                column: "CatletMetadataId");

            migrationBuilder.AddForeignKey(
                name: "FK_NetworkPorts_Metadata_CatletMetadataId",
                table: "NetworkPorts",
                column: "CatletMetadataId",
                principalTable: "Metadata",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
