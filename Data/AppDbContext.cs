using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using CertificadosLaboralesV2.Models;

namespace CertificadosLaboralesV2.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<Empresa> Empresas { get; set; }
        public DbSet<Empleado> Empleados { get; set; }
        public DbSet<Firmante> Firmantes { get; set; }
        public DbSet<PlantillaHtml> PlantillaHtml { get; set; }
        public DbSet<Historial> Historial { get; set; }
        public DbSet<UsuarioEmpresa> UsuarioEmpresas { get; set; }
        public DbSet<Pais> Paises { get; set; }
        public DbSet<DatoVariable> DatosVariables { get; set; }
        public DbSet<Placeholder> Placeholders { get; set; }
        public DbSet<Fuente> Fuentes { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Pais>()
                .HasMany(p => p.Empresas)
                .WithOne(e => e.Pais)
                .HasForeignKey(e => e.PaisId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Pais>()
                .HasMany(p => p.Usuarios)
                .WithOne(u => u.Pais)
                .HasForeignKey(u => u.PaisId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Empresa>()
                .HasMany(e => e.Empleados)
                .WithOne(e => e.Empresa)
                .HasForeignKey(e => e.EmpresaId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Empleado>()
                .Property(e => e.DatosVariables)
                .HasColumnType("nvarchar(max)");

            builder.Entity<ApplicationUser>()
                .HasOne(u => u.Pais)
                .WithMany(p => p.Usuarios)
                .HasForeignKey(u => u.PaisId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Placeholder>()
                .HasOne(p => p.DatoVariable)
                .WithMany()
                .HasForeignKey(p => p.DatoVariableId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
