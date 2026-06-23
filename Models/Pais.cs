using System.ComponentModel.DataAnnotations;

namespace CertificadosLaboralesV2.Models
{
    public class Pais
    {
        public int Id { get; set; }

        [Required]
        public string Nombre { get; set; } = string.Empty;

        public ICollection<Empresa> Empresas { get; set; } = new List<Empresa>();
        public ICollection<ApplicationUser> Usuarios { get; set; } = new List<ApplicationUser>();
    }
}
