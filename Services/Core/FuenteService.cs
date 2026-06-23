using CertificadosLaboralesV2.Data;
using CertificadosLaboralesV2.Models;
using Microsoft.EntityFrameworkCore;

namespace CertificadosLaboralesV2.Services.Core
{
    public class FuenteService(IDbContextFactory<AppDbContext> contextFactory, ILogger<FuenteService> logger)
    {
        public async Task<List<Fuente>> ObtenerFuentesActivasAsync()
        {
            using var context = contextFactory.CreateDbContext();
            return await context.Fuentes
                .Where(f => f.Activa)
                .OrderBy(f => f.Nombre)
                .ToListAsync();
        }

        public async Task<List<Fuente>> ObtenerTodasAsync()
        {
            using var context = contextFactory.CreateDbContext();
            return await context.Fuentes.OrderBy(f => f.Nombre).ToListAsync();
        }

        public async Task<Fuente?> ObtenerPorIdAsync(int id)
        {
            using var context = contextFactory.CreateDbContext();
            return await context.Fuentes.FindAsync(id);
        }

        public async Task<bool> AgregarFuenteAsync(Fuente fuente)
        {
            try
            {
                using var context = contextFactory.CreateDbContext();
                bool slugExiste = await context.Fuentes.AnyAsync(f => f.Slug == fuente.Slug);
                if (slugExiste)
                    return false;

                context.Fuentes.Add(fuente);
                return await context.SaveChangesAsync() > 0;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error al agregar fuente {Nombre}", fuente.Nombre);
                return false;
            }
        }

        public async Task<bool> ActualizarFuenteAsync(Fuente fuente)
        {
            try
            {
                using var context = contextFactory.CreateDbContext();
                context.Fuentes.Update(fuente);
                return await context.SaveChangesAsync() > 0;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error al actualizar fuente {Id}", fuente.Id);
                return false;
            }
        }

        public async Task EliminarFuenteAsync(int id)
        {
            using var context = contextFactory.CreateDbContext();
            var fuente = await context.Fuentes.FindAsync(id);
            if (fuente != null)
            {
                context.Fuentes.Remove(fuente);
                await context.SaveChangesAsync();
            }
        }
    }
}
