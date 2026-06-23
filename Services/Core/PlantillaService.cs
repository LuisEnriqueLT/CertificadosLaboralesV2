using CertificadosLaboralesV2.Data;
using CertificadosLaboralesV2.Models;
using Microsoft.EntityFrameworkCore;

namespace CertificadosLaboralesV2.Services.Core
{
    public class PlantillaService(IDbContextFactory<AppDbContext> contextFactory)
    {
        public async Task<List<PlantillaHtml>> ObtenerTodasLasPlantillasAsync()
        {
            using var context = contextFactory.CreateDbContext();
            return await context.PlantillaHtml.OrderBy(p => p.NombrePlantilla).ToListAsync();
        }

        public async Task<List<PlantillaHtml>> ObtenerPlantillasPorEmpresaIdAsync(int empresaId)
        {
            using var context = contextFactory.CreateDbContext();
            return await context.PlantillaHtml
                .Where(p => p.EmpresaId == empresaId)
                .OrderBy(p => p.NombrePlantilla)
                .ToListAsync();
        }

        public async Task<PlantillaHtml?> ObtenerPlantillaPorIdAsync(int id)
        {
            using var context = contextFactory.CreateDbContext();
            return await context.PlantillaHtml.FindAsync(id);
        }

        public async Task<bool> ExistePlantillaConNombreAsync(int empresaId, string nombrePlantilla)
        {
            using var context = contextFactory.CreateDbContext();
            return await context.PlantillaHtml
                .AnyAsync(p => p.EmpresaId == empresaId && p.NombrePlantilla == nombrePlantilla);
        }

        public async Task<bool> AgregarPlantillaAsync(PlantillaHtml plantilla)
        {
            using var context = contextFactory.CreateDbContext();
            context.PlantillaHtml.Add(plantilla);
            return await context.SaveChangesAsync() > 0;
        }

        public async Task<bool> ActualizarPlantillaAsync(PlantillaHtml plantilla)
        {
            using var context = contextFactory.CreateDbContext();
            context.PlantillaHtml.Update(plantilla);
            return await context.SaveChangesAsync() > 0;
        }

        public async Task EliminarPlantillaAsync(int id)
        {
            using var context = contextFactory.CreateDbContext();
            var plantilla = await context.PlantillaHtml.FindAsync(id);
            if (plantilla != null)
            {
                context.PlantillaHtml.Remove(plantilla);
                await context.SaveChangesAsync();
            }
        }
    }
}
