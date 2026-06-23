using CertificadosLaboralesV2.Data;
using CertificadosLaboralesV2.Models;
using Microsoft.EntityFrameworkCore;

namespace CertificadosLaboralesV2.Services.Core
{
    public class EmpresaService(IDbContextFactory<AppDbContext> contextFactory, ILogger<EmpresaService> logger)
    {
        public async Task<List<Empresa>> ObtenerEmpresasAsync()
        {
            using var context = contextFactory.CreateDbContext();
            return await context.Empresas.OrderBy(e => e.Nombre).ToListAsync();
        }

        public async Task<List<Empresa?>> ObtenerEmpresasPorUsuarioAsync(string userId)
        {
            using var context = contextFactory.CreateDbContext();
            return await context.UsuarioEmpresas
                .Where(ue => ue.UserId == userId)
                .Select(ue => ue.Empresa)
                .OrderBy(e => e!.Nombre)
                .ToListAsync();
        }

        public async Task<Empresa?> ObtenerEmpresaPorIdAsync(int id)
        {
            using var context = contextFactory.CreateDbContext();
            return await context.Empresas.FindAsync(id);
        }

        public async Task<Empresa?> ObtenerEmpresaPorNitAsync(string nit)
        {
            using var context = contextFactory.CreateDbContext();
            return await context.Empresas.FirstOrDefaultAsync(e => e.Nit == nit);
        }

        public async Task<bool> AgregarEmpresaAsync(Empresa empresa)
        {
            try
            {
                using var context = contextFactory.CreateDbContext();
                context.Empresas.Add(empresa);
                return await context.SaveChangesAsync() > 0;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error al agregar empresa {Nombre}", empresa.Nombre);
                return false;
            }
        }

        public async Task<bool> ActualizarEmpresaAsync(Empresa empresa)
        {
            try
            {
                using var context = contextFactory.CreateDbContext();
                context.Empresas.Update(empresa);
                return await context.SaveChangesAsync() > 0;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error al actualizar empresa {Id}", empresa.Id);
                return false;
            }
        }

        public async Task EliminarEmpresaAsync(int id)
        {
            using var context = contextFactory.CreateDbContext();
            var empresa = await context.Empresas.FindAsync(id);
            if (empresa != null)
            {
                context.Empresas.Remove(empresa);
                await context.SaveChangesAsync();
            }
        }

        public async Task AsignarUsuarioAEmpresa(string userId, int empresaId)
        {
            using var context = contextFactory.CreateDbContext();
            var existe = await context.UsuarioEmpresas
                .AnyAsync(ue => ue.UserId == userId && ue.EmpresaId == empresaId);
            if (!existe)
            {
                context.UsuarioEmpresas.Add(new UsuarioEmpresa { UserId = userId, EmpresaId = empresaId });
                await context.SaveChangesAsync();
            }
        }

        public async Task RemoverUsuarioDeEmpresa(string userId, int empresaId)
        {
            using var context = contextFactory.CreateDbContext();
            var rel = await context.UsuarioEmpresas
                .FirstOrDefaultAsync(ue => ue.UserId == userId && ue.EmpresaId == empresaId);
            if (rel != null)
            {
                context.UsuarioEmpresas.Remove(rel);
                await context.SaveChangesAsync();
            }
        }

        public async Task SubirLogoEmpresaAsync(int empresaId, byte[] logoByte)
        {
            using var context = contextFactory.CreateDbContext();
            var empresa = await context.Empresas.FindAsync(empresaId);
            if (empresa != null)
            {
                empresa.Logo = logoByte;
                context.Empresas.Update(empresa);
                await context.SaveChangesAsync();
            }
        }
    }
}
