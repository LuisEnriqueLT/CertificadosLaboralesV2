namespace CertificadosLaboralesV2.Models
{
    public class Firmante
    {
        public int Id { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string? Cargo { get; set; }
        public byte[]? Firma { get; set; }
    }
}
