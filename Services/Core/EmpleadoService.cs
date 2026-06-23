using CertificadosLaboralesV2.Data;
using CertificadosLaboralesV2.Models;
using Microsoft.EntityFrameworkCore;

namespace CertificadosLaboralesV2.Services.Core
{
    public class EmpleadoService(IDbContextFactory<AppDbContext> contextFactory, ILogger<EmpleadoService> logger)
    {
        public async Task<List<Empleado>> ObtenerTodosEmpleadosAsync()
        {
            using var context = contextFactory.CreateDbContext();
            return await context.Empleados.OrderBy(e => e.NombreCompleto).ToListAsync();
        }

        public async Task<Empleado?> ObtenerEmpleadoPorIdAsync(int empleadoId)
        {
            using var context = contextFactory.CreateDbContext();
            return await context.Empleados.FindAsync(empleadoId);
        }

        public async Task<Empleado?> ObtenerEmpleadoPorEmailAsync(string email)
        {
            using var context = contextFactory.CreateDbContext();
            return await context.Empleados.FirstOrDefaultAsync(e => e.Email == email);
        }

        public async Task<List<Empleado>> ObtenerEmpleadosPorEmpresaIdAsync(int empresaId)
        {
            using var context = contextFactory.CreateDbContext();
            return await context.Empleados
                .Where(e => e.EmpresaId == empresaId)
                .OrderBy(e => e.NombreCompleto)
                .ToListAsync();
        }

        public async Task<bool> AgregarEmpleadoAsync(Empleado empleado)
        {
            try
            {
                using var context = contextFactory.CreateDbContext();
                context.Empleados.Add(empleado);
                return await context.SaveChangesAsync() > 0;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error al agregar empleado {Email}", empleado.Email);
                return false;
            }
        }

        public async Task ActualizarEmpleadoAsync(Empleado empleado)
        {
            using var context = contextFactory.CreateDbContext();
            context.Empleados.Update(empleado);
            await context.SaveChangesAsync();
        }

        public async Task<bool> EliminarEmpleadoAsync(int empleadoId)
        {
            using var context = contextFactory.CreateDbContext();
            var empleado = await context.Empleados.FindAsync(empleadoId);
            if (empleado == null) return false;
            context.Empleados.Remove(empleado);
            return await context.SaveChangesAsync() > 0;
        }

        public async Task<List<DatoVariable>> ObtenerTodosDatosVariablesAsync()
        {
            using var context = contextFactory.CreateDbContext();
            return await context.DatosVariables.OrderBy(d => d.NombreCampo).ToListAsync();
        }

        public async Task<bool> ExisteDatoVariableAsync(string nombreCampo)
        {
            using var context = contextFactory.CreateDbContext();
            return await context.DatosVariables.AnyAsync(d => d.NombreCampo == nombreCampo);
        }

        public async Task<DatoVariable> CrearDatoVariableAsync(DatoVariable dato)
        {
            using var context = contextFactory.CreateDbContext();
            context.DatosVariables.Add(dato);
            await context.SaveChangesAsync();
            return dato;
        }
    }
}
