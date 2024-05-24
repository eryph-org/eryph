using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eryph.StateDb.Migrations
{
    /// <inheritdoc />
    public partial class Diskdata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CatletDrives_CatletDisks_AttachedDiskId",
                table: "CatletDrives");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "LastUpdated",
                table: "OperationTasks",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DiskIdentifier",
                table: "CatletDisks",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Geneset",
                table: "CatletDisks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeen",
                table: "CatletDisks",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "LastSeenAgent",
                table: "CatletDisks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "UsedSizeBytes",
                table: "CatletDisks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CatletDrives_CatletDisks_AttachedDiskId",
                table: "CatletDrives",
                column: "AttachedDiskId",
                principalTable: "CatletDisks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CatletDrives_CatletDisks_AttachedDiskId",
                table: "CatletDrives");

            migrationBuilder.DropColumn(
                name: "DiskIdentifier",
                table: "CatletDisks");

            migrationBuilder.DropColumn(
                name: "Geneset",
                table: "CatletDisks");

            migrationBuilder.DropColumn(
                name: "LastSeen",
                table: "CatletDisks");

            migrationBuilder.DropColumn(
                name: "LastSeenAgent",
                table: "CatletDisks");

            migrationBuilder.DropColumn(
                name: "UsedSizeBytes",
                table: "CatletDisks");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "LastUpdated",
                table: "OperationTasks",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "TEXT");

            migrationBuilder.AddForeignKey(
                name: "FK_CatletDrives_CatletDisks_AttachedDiskId",
                table: "CatletDrives",
                column: "AttachedDiskId",
                principalTable: "CatletDisks",
                principalColumn: "Id");
        }
    }
}
