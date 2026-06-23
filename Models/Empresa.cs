namespace CertificadosLaboralesV2.Models
{
    public class Empresa
    {
        public int Id { get; set; }
        public string? Nombre { get; set; }
        public string? Domicilio { get; set; }
        public string? Email { get; set; }
        public int? Telefono { get; set; }
        public string? Nit { get; set; }
        public int RepresentanteLegalId { get; set; }
        public byte[]? Logo { get; set; }
        public int? PaisId { get; set; }
        public Pais? Pais { get; set; }
        public ICollection<Empleado> Empleados { get; set; } = new List<Empleado>();
    }
}
