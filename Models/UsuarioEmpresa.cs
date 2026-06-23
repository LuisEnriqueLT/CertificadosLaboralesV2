namespace CertificadosLaboralesV2.Models
{
    public class UsuarioEmpresa
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int EmpresaId { get; set; }
        public ApplicationUser? User { get; set; }
        public Empresa? Empresa { get; set; }
    }
}
