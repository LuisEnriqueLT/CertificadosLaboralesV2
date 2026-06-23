namespace CertificadosLaboralesV2.Models
{
    public class Placeholder
    {
        public int Id { get; set; }
        public string Texto { get; set; } = string.Empty;
        public string PlaceholderTexto { get; set; } = string.Empty;
        public int? DatoVariableId { get; set; }
        public DatoVariable? DatoVariable { get; set; }
        public string? CampoFijo { get; set; }
    }
}
