using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CertificadosLaboralesV2.Migrations
{
    /// <inheritdoc />
    public partial class MakePlaceholderCampoFijoOpcional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "DatoVariableId",
                table: "Placeholders",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "CampoFijo",
                table: "Placeholders",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CampoFijo",
                table: "Placeholders");

            migrationBuilder.AlterColumn<int>(
                name: "DatoVariableId",
                table: "Placeholders",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
