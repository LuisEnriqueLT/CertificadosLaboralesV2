using CertificadosLaboralesV2.Data;
using CertificadosLaboralesV2.Models;
using Microsoft.EntityFrameworkCore;

namespace CertificadosLaboralesV2.Services.Core
{
    public class HistorialService(IDbContextFactory<AppDbContext> contextFactory)
    {
        public async Task<List<Historial>> ObtenerHistorialCreadoPorAsync(int creadorId)
        {
            using var context = contextFactory.CreateDbContext();
            return await context.Historial
                .Where(h => h.CreadoPorId == creadorId)
                .ToListAsync();
        }

        public async Task<List<Historial>> ObtenerHistorialCreadoParaAsync(int creadoParaId)
        {
            using var context = contextFactory.CreateDbContext();
            return await context.Historial
                .Where(h => h.CreadoParaId == creadoParaId)
                .ToListAsync();
        }

        public async Task<List<Historial>> ObtenerTodoHistorialAsync()
        {
            using var context = contextFactory.CreateDbContext();
            return await context.Historial
                .OrderByDescending(h => h.FechaCreacion)
                .ToListAsync();
        }

        public async Task<Historial?> BuscarPorCodigoAsync(Guid codigo)
        {
            using var context = contextFactory.CreateDbContext();
            return await context.Historial
                .FirstOrDefaultAsync(h => h.CodigoVerificacion == codigo);
        }
    }
}
