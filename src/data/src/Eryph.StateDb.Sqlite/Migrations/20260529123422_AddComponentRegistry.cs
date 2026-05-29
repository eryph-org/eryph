using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eryph.StateDb.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddComponentRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ComponentRegistrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ComponentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ComponentType = table.Column<int>(type: "INTEGER", nullable: false),
                    InstanceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MachineName = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<string>(type: "TEXT", nullable: true),
                    InboundQueue = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastHeartbeat = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AppliedConfigVersionsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CapabilitiesJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComponentRegistrations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConfigRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Domain = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    Payload = table.Column<string>(type: "TEXT", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComponentRegistrations_ComponentId",
                table: "ComponentRegistrations",
                column: "ComponentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConfigRecords_Domain",
                table: "ConfigRecords",
                column: "Domain",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComponentRegistrations");

            migrationBuilder.DropTable(
                name: "ConfigRecords");
        }
    }
}
