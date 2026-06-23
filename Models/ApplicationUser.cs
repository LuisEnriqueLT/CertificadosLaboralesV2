using Microsoft.AspNetCore.Identity;

namespace CertificadosLaboralesV2.Models
{
    public class ApplicationUser : IdentityUser
    {
        public int? EmpleadoId { get; set; }
        public Empleado? Empleado { get; set; }
        public int? PaisId { get; set; }
        public Pais? Pais { get; set; }
    }
}
