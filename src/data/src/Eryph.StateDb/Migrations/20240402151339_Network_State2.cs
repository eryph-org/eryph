using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eryph.StateDb.Migrations
{
    public partial class Network_State2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IpAssignment_PoolId",
                table: "IpAssignment");

            migrationBuilder.DropColumn(
                name: "Counter",
                table: "IpPools");

            migrationBuilder.AddColumn<string>(
                name: "NextIp",
                table: "IpPools",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IpAssignment_PoolId_Number",
                table: "IpAssignment",
                columns: new[] { "PoolId", "Number" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IpAssignment_PoolId_Number",
                table: "IpAssignment");

            migrationBuilder.DropColumn(
                name: "NextIp",
                table: "IpPools");

            migrationBuilder.AddColumn<int>(
                name: "Counter",
                table: "IpPools",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_IpAssignment_PoolId",
                table: "IpAssignment",
                column: "PoolId");
        }
    }
}
