using System.ComponentModel.DataAnnotations.Schema;

namespace CertificadosLaboralesV2.Models
{
    public class Empleado
    {
        public int Id { get; set; }
        public string? NombreCompleto { get; set; }
        public string? Email { get; set; }
        public string? Cedula { get; set; }
        public int EmpresaId { get; set; }

        public string? Cargo { get; set; }
        public string? SalarioMensual { get; set; }
        public string? tipoSalario { get; set; }
        public DateOnly FechaIngreso { get; set; }
        public string? TipoContrato { get; set; }
        public string? CiudadDeTrabajo { get; set; }
        public string? TipoId { get; set; }

        public string? DatosVariables { get; set; } // JSON

        public bool EstadoLaboral { get; set; }

        public Empresa? Empresa { get; set; }

        [NotMapped] public bool TieneUsuarioIdentity { get; set; }
        [NotMapped] public string? Rol { get; set; }
    }
}
