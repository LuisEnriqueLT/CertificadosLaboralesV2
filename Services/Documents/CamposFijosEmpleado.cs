using CertificadosLaboralesV2.Models;

namespace CertificadosLaboralesV2.Services.Documents
{
    public static class CamposFijosEmpleado
    {
        public static readonly Dictionary<string, string> Campos = new()
        {
            { "nombrecompleto", "Nombre completo" },
            { "email", "Correo electrónico" },
            { "cedula", "Cédula" },
            { "cargo", "Cargo" },
            { "salariomensual", "Salario mensual" },
            { "tiposalario", "Tipo de salario" },
            { "tipocontrato", "Tipo de contrato" },
            { "ciudaddetrabajo", "Ciudad de trabajo" },
            { "tipoid", "Tipo de identificación" },
            { "fechaingreso", "Fecha de ingreso" },
            { "estadolaboral", "Estado laboral" }
        };

        public static string? ObtenerValor(Empleado? empleado, string clave) => clave switch
        {
            "nombrecompleto" => empleado?.NombreCompleto,
            "email" => empleado?.Email,
            "cedula" => empleado?.Cedula,
            "cargo" => empleado?.Cargo,
            "salariomensual" => empleado?.SalarioMensual,
            "tiposalario" => empleado?.tipoSalario,
            "tipocontrato" => empleado?.TipoContrato,
            "ciudaddetrabajo" => empleado?.CiudadDeTrabajo,
            "tipoid" => empleado?.TipoId,
            "fechaingreso" => empleado?.FechaIngreso.ToString("yyyy-MM-dd"),
            "estadolaboral" => empleado == null ? null : (empleado.EstadoLaboral ? "Activo" : "Inactivo"),
            _ => null
        };
    }
}
