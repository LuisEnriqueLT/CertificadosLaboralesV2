using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CertificadosLaboralesV2.Models
{
    public class PlantillaHtml
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Empresa))]
        public int EmpresaId { get; set; }

        [Required]
        [MaxLength(200)]
        public string NombrePlantilla { get; set; } = string.Empty;

        [Required]
        public string HtmlContenido { get; set; } = string.Empty;

        public Empresa Empresa { get; set; } = null!;

        public string? MarcaDeAgua { get; set; }

        public TipoMarcaAgua TipoMarcaAgua { get; set; } = TipoMarcaAgua.Ninguna;

        public double OpacidadMarcaAgua { get; set; } = 0.08;

        public double TamanoMarcaAgua { get; set; } = 80;

        public int RotacionMarcaAgua { get; set; } = -35;

        public double PosicionXMarcaAgua { get; set; } = 50;

        public double PosicionYMarcaAgua { get; set; } = 50;
    }

    public enum TipoMarcaAgua { Ninguna, Texto, Imagen }
}
