using CertificadosLaboralesV2.Data;
using CertificadosLaboralesV2.Models;
using Microsoft.EntityFrameworkCore;

namespace CertificadosLaboralesV2.Services.Core
{
    public class FirmanteService(IDbContextFactory<AppDbContext> contextFactory, ILogger<FirmanteService> logger)
    {
        public async Task<List<Firmante>> ObtenerTodosLosFirmantesAsync()
        {
            using var context = contextFactory.CreateDbContext();
            return await context.Firmantes.OrderBy(f => f.NombreCompleto).ToListAsync();
        }

        public async Task<Firmante?> ObtenerFirmantePorNombre(string nombre)
        {
            using var context = contextFactory.CreateDbContext();
            return await context.Firmantes.FirstOrDefaultAsync(f => f.NombreCompleto == nombre);
        }

        public async Task<Firmante?> ObtenerFirmantePorIdAsync(int id)
        {
            using var context = contextFactory.CreateDbContext();
            return await context.Firmantes.FindAsync(id);
        }

        public async Task<bool> AgregarFirmanteAsync(Firmante firmante)
        {
            try
            {
                using var context = contextFactory.CreateDbContext();
                context.Firmantes.Add(firmante);
                return await context.SaveChangesAsync() > 0;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error al agregar firmante {Nombre}", firmante.NombreCompleto);
                return false;
            }
        }

        public async Task ActualizarFirmanteAsync(Firmante firmante)
        {
            using var context = contextFactory.CreateDbContext();
            context.Firmantes.Update(firmante);
            await context.SaveChangesAsync();
        }

        public async Task EliminarFirmanteAsync(int id)
        {
            using var context = contextFactory.CreateDbContext();
            var firmante = await context.Firmantes.FindAsync(id);
            if (firmante != null)
            {
                context.Firmantes.Remove(firmante);
                await context.SaveChangesAsync();
            }
        }

        public async Task SubirFirmaAsync(int firmanteId, byte[] firma)
        {
            using var context = contextFactory.CreateDbContext();
            var firmante = await context.Firmantes.FindAsync(firmanteId);
            if (firmante != null)
            {
                firmante.Firma = firma;
                context.Firmantes.Update(firmante);
                await context.SaveChangesAsync();
            }
        }
    }
}
