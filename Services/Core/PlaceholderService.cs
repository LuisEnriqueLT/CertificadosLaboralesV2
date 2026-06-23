using CertificadosLaboralesV2.Data;
using CertificadosLaboralesV2.Models;
using Microsoft.EntityFrameworkCore;

namespace CertificadosLaboralesV2.Services.Core
{
    public class PlaceholderService(IDbContextFactory<AppDbContext> contextFactory, ILogger<PlaceholderService> logger)
    {
        public async Task<Placeholder?> ObtenerPlaceholderPorIdAsync(int placeholderId)
        {
            using var context = contextFactory.CreateDbContext();
            return await context.Placeholders.FindAsync(placeholderId);
        }

        public async Task<List<Placeholder>> ObtenerPlaceholdersPorDatoVariableIdAsync(int datoVariableId)
        {
            using var context = contextFactory.CreateDbContext();
            return await context.Placeholders
                .Where(p => p.DatoVariableId == datoVariableId)
                .ToListAsync();
        }

        public async Task<List<Placeholder>> ObtenerTodosLosPlaceholdersAsync()
        {
            using var context = contextFactory.CreateDbContext();
            return await context.Placeholders
                .Include(p => p.DatoVariable)
                .ToListAsync();
        }

        public async Task<bool> AgregarPlaceholderAsync(Placeholder placeholder)
        {
            try
            {
                using var context = contextFactory.CreateDbContext();
                context.Placeholders.Add(placeholder);
                return await context.SaveChangesAsync() > 0;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error al agregar placeholder {Texto}", placeholder.Texto);
                return false;
            }
        }

        public async Task ActualizarPlaceholderAsync(Placeholder placeholder)
        {
            using var context = contextFactory.CreateDbContext();
            context.Placeholders.Update(placeholder);
            await context.SaveChangesAsync();
        }

        public async Task<bool> EliminarPlaceholderAsync(int placeholderId)
        {
            using var context = contextFactory.CreateDbContext();
            var placeholder = await context.Placeholders.FindAsync(placeholderId);
            if (placeholder == null) return false;
            context.Placeholders.Remove(placeholder);
            return await context.SaveChangesAsync() > 0;
        }
    }
}
