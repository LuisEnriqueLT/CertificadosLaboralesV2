using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CertificadosLaboralesV2.Migrations
{
    /// <inheritdoc />
    public partial class AddWatermarkToPlantilla : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MarcaDeAgua",
                table: "PlantillaHtml",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "OpacidadMarcaAgua",
                table: "PlantillaHtml",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "TipoMarcaAgua",
                table: "PlantillaHtml",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MarcaDeAgua",
                table: "PlantillaHtml");

            migrationBuilder.DropColumn(
                name: "OpacidadMarcaAgua",
                table: "PlantillaHtml");

            migrationBuilder.DropColumn(
                name: "TipoMarcaAgua",
                table: "PlantillaHtml");
        }
    }
}
