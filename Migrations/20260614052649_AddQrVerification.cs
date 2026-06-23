using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CertificadosLaboralesV2.Migrations
{
    /// <inheritdoc />
    public partial class AddQrVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CodigoVerificacion",
                table: "Historial",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "NombreDocumento",
                table: "Historial",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CodigoVerificacion", table: "Historial");
            migrationBuilder.DropColumn(name: "NombreDocumento", table: "Historial");
        }
    }
}
