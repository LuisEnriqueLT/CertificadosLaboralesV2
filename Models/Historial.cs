namespace CertificadosLaboralesV2.Models
{
    public class Historial
    {
        public int Id { get; set; }
        public int EmpresaId { get; set; }
        public int CreadoPorId { get; set; }
        public int CreadoParaId { get; set; }
        public byte[] Contenido { get; set; } = Array.Empty<byte>();
        public string Hash { get; set; } = string.Empty;
        public Guid CodigoVerificacion { get; set; } = Guid.Empty;
        public string NombreDocumento { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    }
}
