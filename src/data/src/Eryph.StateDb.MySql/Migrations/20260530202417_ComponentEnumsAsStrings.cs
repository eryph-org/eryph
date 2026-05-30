using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eryph.StateDb.MySql.Migrations
{
    /// <inheritdoc />
    public partial class ComponentEnumsAsStrings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Domain",
                table: "ConfigRecords",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "ComponentRegistrations",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "ComponentType",
                table: "ComponentRegistrations",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("MySql:CharSet", "utf8mb4");

            // Convert any existing ordinal values (now stored as the strings '0','1',...) to the
            // enum names that HasConversion<string>() expects. No-op on a fresh database.
            migrationBuilder.Sql(
                "UPDATE `ConfigRecords` SET `Domain` = CASE `Domain` " +
                "WHEN '0' THEN 'PlacementConfig' WHEN '1' THEN 'NetworkProviders' WHEN '2' THEN 'Endpoints' " +
                "ELSE `Domain` END;");
            migrationBuilder.Sql(
                "UPDATE `ComponentRegistrations` SET `Status` = CASE `Status` " +
                "WHEN '0' THEN 'Registering' WHEN '1' THEN 'Active' WHEN '2' THEN 'Stale' WHEN '3' THEN 'Dead' " +
                "ELSE `Status` END;");
            migrationBuilder.Sql(
                "UPDATE `ComponentRegistrations` SET `ComponentType` = CASE `ComponentType` " +
                "WHEN '0' THEN 'VMHostAgent' WHEN '1' THEN 'GenePoolAgent' WHEN '2' THEN 'Network' " +
                "WHEN '3' THEN 'ComputeApi' WHEN '4' THEN 'Identity' ELSE `ComponentType` END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Convert enum names back to their ordinal strings so the string->int cast succeeds.
            migrationBuilder.Sql(
                "UPDATE `ConfigRecords` SET `Domain` = CASE `Domain` " +
                "WHEN 'PlacementConfig' THEN '0' WHEN 'NetworkProviders' THEN '1' WHEN 'Endpoints' THEN '2' " +
                "ELSE `Domain` END;");
            migrationBuilder.Sql(
                "UPDATE `ComponentRegistrations` SET `Status` = CASE `Status` " +
                "WHEN 'Registering' THEN '0' WHEN 'Active' THEN '1' WHEN 'Stale' THEN '2' WHEN 'Dead' THEN '3' " +
                "ELSE `Status` END;");
            migrationBuilder.Sql(
                "UPDATE `ComponentRegistrations` SET `ComponentType` = CASE `ComponentType` " +
                "WHEN 'VMHostAgent' THEN '0' WHEN 'GenePoolAgent' THEN '1' WHEN 'Network' THEN '2' " +
                "WHEN 'ComputeApi' THEN '3' WHEN 'Identity' THEN '4' ELSE `ComponentType` END;");

            migrationBuilder.AlterColumn<int>(
                name: "Domain",
                table: "ConfigRecords",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "ComponentRegistrations",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<int>(
                name: "ComponentType",
                table: "ComponentRegistrations",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
