using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Eryph.StateDb.MySql.Migrations
{
    public partial class InitialMigrations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                "Agents",
                table => new
                {
                    Name = table.Column<string>(nullable: false)
                },
                constraints: table => { table.PrimaryKey("PK_Agents", x => x.Name); });

            migrationBuilder.CreateTable(
                "Networks",
                table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    Name = table.Column<string>(nullable: false),
                    VLanId = table.Column<ulong>(nullable: false)
                },
                constraints: table => { table.PrimaryKey("PK_Networks", x => x.Id); });

            migrationBuilder.CreateTable(
                "Operations",
                table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    MachineGuid = table.Column<Guid>(nullable: false),
                    Status = table.Column<int>(nullable: false),
                    StatusMessage = table.Column<string>(nullable: true)
                },
                constraints: table => { table.PrimaryKey("PK_Operations", x => x.Id); });

            migrationBuilder.CreateTable(
                "Machines",
                table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    Name = table.Column<string>(nullable: true),
                    AgentName = table.Column<string>(nullable: true),
                    Status = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Machines", x => x.Id);
                    table.ForeignKey(
                        "FK_Machines_Agents_AgentName",
                        x => x.AgentName,
                        "Agents",
                        "Name",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                "AgentNetworks",
                table => new
                {
                    AgentName = table.Column<string>(nullable: false),
                    NetworkId = table.Column<Guid>(nullable: false),
                    VirtualSwitchName = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentNetworks", x => new {x.NetworkId, x.AgentName});
                    table.ForeignKey(
                        "FK_AgentNetworks_Agents_AgentName",
                        x => x.AgentName,
                        "Agents",
                        "Name",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        "FK_AgentNetworks_Networks_NetworkId",
                        x => x.NetworkId,
                        "Networks",
                        "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                "Subnets",
                table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    NetworkId = table.Column<Guid>(nullable: false),
                    IsPublic = table.Column<bool>(nullable: false),
                    DhcpEnabled = table.Column<bool>(nullable: false),
                    IpVersion = table.Column<byte>(nullable: false),
                    GatewayAddress = table.Column<string>(nullable: true),
                    Address = table.Column<string>(nullable: true),
                    DnsServers = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subnets", x => x.Id);
                    table.ForeignKey(
                        "FK_Subnets_Networks_NetworkId",
                        x => x.NetworkId,
                        "Networks",
                        "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                "OperationTasks",
                table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    OperationId = table.Column<Guid>(nullable: true),
                    Status = table.Column<int>(nullable: false),
                    AgentName = table.Column<string>(nullable: true),
                    Name = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationTasks", x => x.Id);
                    table.ForeignKey(
                        "FK_OperationTasks_Operations_OperationId",
                        x => x.OperationId,
                        "Operations",
                        "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                "MachineNetwork",
                table => new
                {
                    MachineId = table.Column<Guid>(nullable: false),
                    AdapterName = table.Column<string>(nullable: false),
                    IpV4Addresses = table.Column<string>(nullable: true),
                    IpV6Addresses = table.Column<string>(nullable: true),
                    IPv4DefaultGateway = table.Column<string>(nullable: true),
                    IPv6DefaultGateway = table.Column<string>(nullable: true),
                    DnsServers = table.Column<string>(nullable: true),
                    IpV4Subnets = table.Column<string>(nullable: true),
                    IpV6Subnets = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineNetwork", x => new {x.MachineId, x.AdapterName});
                    table.ForeignKey(
                        "FK_MachineNetwork_Machines_MachineId",
                        x => x.MachineId,
                        "Machines",
                        "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                "VirtualMachines",
                table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    Path = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VirtualMachines", x => x.Id);
                    table.ForeignKey(
                        "FK_VirtualMachines_Machines_Id",
                        x => x.Id,
                        "Machines",
                        "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                "Logs",
                table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    Message = table.Column<string>(nullable: true),
                    OperationId = table.Column<Guid>(nullable: true),
                    TaskId = table.Column<Guid>(nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Logs", x => x.Id);
                    table.ForeignKey(
                        "FK_Logs_Operations_OperationId",
                        x => x.OperationId,
                        "Operations",
                        "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        "FK_Logs_OperationTasks_TaskId",
                        x => x.TaskId,
                        "OperationTasks",
                        "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                "VirtualMachineNetworkAdapters",
                table => new
                {
                    MachineId = table.Column<Guid>(nullable: false),
                    Name = table.Column<string>(nullable: false),
                    SwitchName = table.Column<string>(nullable: true),
                    MacAddress = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VirtualMachineNetworkAdapters", x => new {x.MachineId, x.Name});
                    table.ForeignKey(
                        "FK_VirtualMachineNetworkAdapters_VirtualMachines_MachineId",
                        x => x.MachineId,
                        "VirtualMachines",
                        "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                "IX_AgentNetworks_AgentName",
                "AgentNetworks",
                "AgentName");

            migrationBuilder.CreateIndex(
                "IX_Logs_OperationId",
                "Logs",
                "OperationId");

            migrationBuilder.CreateIndex(
                "IX_Logs_TaskId",
                "Logs",
                "TaskId");

            migrationBuilder.CreateIndex(
                "IX_Machines_AgentName",
                "Machines",
                "AgentName");

            migrationBuilder.CreateIndex(
                "IX_OperationTasks_OperationId",
                "OperationTasks",
                "OperationId");

            migrationBuilder.CreateIndex(
                "IX_Subnets_Address",
                "Subnets",
                "Address");

            migrationBuilder.CreateIndex(
                "IX_Subnets_NetworkId",
                "Subnets",
                "NetworkId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                "AgentNetworks");

            migrationBuilder.DropTable(
                "Logs");

            migrationBuilder.DropTable(
                "MachineNetwork");

            migrationBuilder.DropTable(
                "Subnets");

            migrationBuilder.DropTable(
                "VirtualMachineNetworkAdapters");

            migrationBuilder.DropTable(
                "OperationTasks");

            migrationBuilder.DropTable(
                "Networks");

            migrationBuilder.DropTable(
                "VirtualMachines");

            migrationBuilder.DropTable(
                "Operations");

            migrationBuilder.DropTable(
                "Machines");

            migrationBuilder.DropTable(
                "Agents");
        }
    }
}