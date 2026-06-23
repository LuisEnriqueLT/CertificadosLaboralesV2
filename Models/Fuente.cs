using System.ComponentModel.DataAnnotations;

namespace CertificadosLaboralesV2.Models
{
    public class Fuente
    {
        [Key] public int Id { get; set; }
        [Required][MaxLength(100)] public string Nombre { get; set; } = "";
        [Required][MaxLength(80)]  public string Slug { get; set; } = "";
        public byte[] Archivo { get; set; } = [];
        [MaxLength(10)] public string Extension { get; set; } = ".ttf";
        public bool Activa { get; set; } = true;
        public DateTime FechaSubida { get; set; } = DateTime.UtcNow;
    }
}
