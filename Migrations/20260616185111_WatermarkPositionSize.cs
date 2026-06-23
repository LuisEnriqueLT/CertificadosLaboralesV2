using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CertificadosLaboralesV2.Migrations
{
    /// <inheritdoc />
    public partial class WatermarkPositionSize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "PosicionXMarcaAgua",
                table: "PlantillaHtml",
                type: "float",
                nullable: false,
                defaultValue: 50.0);

            migrationBuilder.AddColumn<double>(
                name: "PosicionYMarcaAgua",
                table: "PlantillaHtml",
                type: "float",
                nullable: false,
                defaultValue: 50.0);

            migrationBuilder.AddColumn<int>(
                name: "RotacionMarcaAgua",
                table: "PlantillaHtml",
                type: "int",
                nullable: false,
                defaultValue: -35);

            migrationBuilder.AddColumn<double>(
                name: "TamanoMarcaAgua",
                table: "PlantillaHtml",
                type: "float",
                nullable: false,
                defaultValue: 80.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PosicionXMarcaAgua",
                table: "PlantillaHtml");

            migrationBuilder.DropColumn(
                name: "PosicionYMarcaAgua",
                table: "PlantillaHtml");

            migrationBuilder.DropColumn(
                name: "RotacionMarcaAgua",
                table: "PlantillaHtml");

            migrationBuilder.DropColumn(
                name: "TamanoMarcaAgua",
                table: "PlantillaHtml");
        }
    }
}
