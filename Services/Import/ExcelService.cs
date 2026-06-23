using CertificadosLaboralesV2.Models;
using CertificadosLaboralesV2.Services.Core;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace CertificadosLaboralesV2.Services.Import
{
    public class ExcelService(
        EmpresaService empresaService,
        EmpleadoService empleadoService,
        FirmanteService firmanteService,
        PlaceholderService placeholderService,
        IConfiguration configuration,
        IWebHostEnvironment env,
        ILogger<ExcelService> logger)
    {
        private readonly string _path = configuration["ExcelSettings:UploadFolder"]!;

        public bool ExisteArchivo()
        {
            if (string.IsNullOrWhiteSpace(_path)) return false;
            var fullFolder = Path.Combine(env.ContentRootPath, _path);
            if (!Directory.Exists(fullFolder)) return false;
            return Directory.GetFiles(fullFolder, "*.xlsx").Any();
        }

        public async Task<List<List<Empresa>>> ActualizarEmpresasExcel()
        {
            var nuevas = new List<Empresa>();
            var cambios = new List<Empresa>();
            var fullPath = ObtenerRutaExcel();
            using var ms = new MemoryStream(File.ReadAllBytes(fullPath));
            using var doc = SpreadsheetDocument.Open(ms, false);

            var wb = doc.WorkbookPart!;
            var ws = wb.WorksheetParts.Last();
            var logos = ObtenerImagenesPorFila(ws);
            var sheet = ws.Worksheet!.Elements<SheetData>().First();

            foreach (var row in sheet.Elements<Row>().Skip(1))
            {
                var nit = GetCellValue(wb, row, 0);
                if (string.IsNullOrWhiteSpace(nit)) continue;

                var nombre = GetCellValue(wb, row, 1);
                var domicilio = GetCellValue(wb, row, 2);
                var telStr = GetCellValue(wb, row, 3);
                var email = GetCellValue(wb, row, 4);
                var representanteStr = GetCellValue(wb, row, 5);

                logos.TryGetValue((int)row.RowIndex!.Value, out var logoBytes);

                var empresa = await empresaService.ObtenerEmpresaPorNitAsync(nit);
                var representante = await firmanteService.ObtenerFirmantePorNombre(representanteStr ?? "");

                if (empresa == null)
                {
                    nuevas.Add(new Empresa
                    {
                        Nit = nit, Nombre = nombre, Domicilio = domicilio,
                        Telefono = int.TryParse(telStr, out var t) ? t : null,
                        Email = email, RepresentanteLegalId = representante?.Id ?? 0, Logo = logoBytes
                    });
                }
                else
                {
                    bool mod = false;
                    void Set<T>(T value, T current, Action<T> setter)
                    {
                        if (value != null && !value.Equals(current)) { setter(value); mod = true; }
                    }

                    Set(nombre, empresa.Nombre, v => empresa.Nombre = v);
                    Set(domicilio, empresa.Domicilio, v => empresa.Domicilio = v);
                    if (int.TryParse(telStr, out var t2)) Set(t2, empresa.Telefono ?? 0, v => empresa.Telefono = v);
                    Set(email, empresa.Email, v => empresa.Email = v);
                    if (representante != null) Set(representante.Id, empresa.RepresentanteLegalId, v => empresa.RepresentanteLegalId = v);
                    if (logoBytes != null && (empresa.Logo == null || !empresa.Logo.SequenceEqual(logoBytes)))
                    { empresa.Logo = logoBytes; mod = true; }

                    if (mod) cambios.Add(empresa);
                }
            }

            foreach (var e in nuevas) await empresaService.AgregarEmpresaAsync(e);
            foreach (var e in cambios) await empresaService.ActualizarEmpresaAsync(e);

            return [nuevas, cambios];
        }

        public async Task<List<List<Empleado>>> ActualizarEmpleadosExcel()
        {
            logger.LogInformation("Iniciando ActualizarEmpleadosExcel");

            var nuevos = new List<Empleado>();
            var cambios = new List<Empleado>();
            var fullPath = ObtenerRutaExcel();
            using var ms = new MemoryStream(File.ReadAllBytes(fullPath));
            using var doc = SpreadsheetDocument.Open(ms, false);

            var wb = doc.WorkbookPart!;
            var ws = wb.WorksheetParts.ElementAt(1);
            var sheet = ws.Worksheet!.Elements<SheetData>().First();

            var filas = sheet.Elements<Row>().ToList();
            logger.LogInformation("Total filas (incluye header): {Count}", filas.Count);

            if (filas.Count < 2)
                return [nuevos, cambios];

            var headerRow = filas[0];
            var headers = headerRow.Elements<Cell>()
                .Select((c, i) => GetCellValue(wb, headerRow, i) ?? "")
                .ToList();

            var existentes = await empleadoService.ObtenerTodosDatosVariablesAsync();
            var existentesDict = existentes.ToDictionary(x => x.Clave);

            foreach (var h in headers)
            {
                var clave = NormalizarClave(h);
                if (EsCampoFijoConocido(clave) || existentesDict.ContainsKey(clave)) continue;

                var nuevoDato = await empleadoService.CrearDatoVariableAsync(new DatoVariable
                {
                    NombreCampo = h, Clave = clave, TipoDato = TipoDatoVariable.Texto
                });

                await placeholderService.AgregarPlaceholderAsync(new Placeholder
                {
                    Texto = h,
                    PlaceholderTexto = clave,
                    DatoVariableId = nuevoDato.Id
                });
            }

            foreach (var row in filas.Skip(1))
            {
                var values = row.Elements<Cell>()
                    .Select((c, i) => GetCellValue(wb, row, i))
                    .ToList();

                string? SafeValue(string campo)
                {
                    var i = BuscarIndice(headers, campo);
                    return i >= 0 ? values.ElementAtOrDefault(i) : null;
                }

                if (values.All(string.IsNullOrWhiteSpace)) continue;

                var dataDict = new Dictionary<string, string>();
                for (int i = 0; i < headers.Count; i++)
                {
                    var clave = NormalizarClave(headers[i]);
                    if (EsCampoFijoConocido(clave)) continue;
                    dataDict[clave] = values.ElementAtOrDefault(i) ?? "";
                }

                var email = SafeValue("email");
                if (string.IsNullOrWhiteSpace(email)) continue;

                var empleado = await empleadoService.ObtenerEmpleadoPorEmailAsync(email);
                var jsonVariables = JsonSerializer.Serialize(dataDict);

                if (empleado == null)
                {
                    var empresaId = await ObtenerEmpresaId(values, headers);
                    var nuevoEmpleado = new Empleado
                    {
                        NombreCompleto = SafeValue("nombrecompleto"),
                        Email = email,
                        Cedula = SafeValue("cedula"),
                        Cargo = SafeValue("cargo"),
                        SalarioMensual = SafeValue("salariomensual"),
                        tipoSalario = SafeValue("tiposalario"),
                        TipoContrato = SafeValue("tipocontrato"),
                        CiudadDeTrabajo = SafeValue("ciudaddetrabajo"),
                        TipoId = SafeValue("tipoid"),
                        DatosVariables = jsonVariables,
                        EmpresaId = empresaId
                    };
                    var fecha = ParseExcelDate(SafeValue("fechaingreso"));
                    if (fecha.HasValue) nuevoEmpleado.FechaIngreso = fecha.Value;
                    nuevos.Add(nuevoEmpleado);
                }
                else
                {
                    bool mod = false;
                    var nuevaFecha = ParseExcelDate(SafeValue("fechaingreso"));
                    if (nuevaFecha.HasValue && empleado.FechaIngreso != nuevaFecha.Value)
                    { empleado.FechaIngreso = nuevaFecha.Value; mod = true; }

                    void Set<T>(T value, T current, Action<T> setter)
                    {
                        if (value != null && !value.Equals(current)) { setter(value); mod = true; }
                    }

                    Set(SafeValue("nombrecompleto"), empleado.NombreCompleto, v => empleado.NombreCompleto = v);
                    Set(SafeValue("cedula"), empleado.Cedula, v => empleado.Cedula = v);
                    Set(SafeValue("cargo"), empleado.Cargo, v => empleado.Cargo = v);
                    Set(SafeValue("salariomensual"), empleado.SalarioMensual, v => empleado.SalarioMensual = v);
                    Set(SafeValue("tiposalario"), empleado.tipoSalario, v => empleado.tipoSalario = v);
                    Set(SafeValue("tipocontrato"), empleado.TipoContrato, v => empleado.TipoContrato = v);
                    Set(SafeValue("ciudaddetrabajo"), empleado.CiudadDeTrabajo, v => empleado.CiudadDeTrabajo = v);
                    Set(SafeValue("tipoid"), empleado.TipoId, v => empleado.TipoId = v);

                    if (empleado.DatosVariables != jsonVariables)
                    { empleado.DatosVariables = jsonVariables; mod = true; }

                    if (mod) cambios.Add(empleado);
                }
            }

            foreach (var e in nuevos) await empleadoService.AgregarEmpleadoAsync(e);
            foreach (var e in cambios) await empleadoService.ActualizarEmpleadoAsync(e);

            logger.LogInformation("Finalizado. Nuevos: {Nuevos}, Cambios: {Cambios}", nuevos.Count, cambios.Count);
            return [nuevos, cambios];
        }

        public async Task<List<List<Firmante>>> ActualizarFirmantesExcel()
        {
            var nuevos = new List<Firmante>();
            var cambios = new List<Firmante>();
            var fullPath = ObtenerRutaExcel();
            using var ms = new MemoryStream(File.ReadAllBytes(fullPath));
            using var doc = SpreadsheetDocument.Open(ms, false);

            var wb = doc.WorkbookPart!;
            var ws = wb.WorksheetParts.First();
            var firmas = ObtenerImagenesPorFila(ws);
            var sheet = ws.Worksheet!.Elements<SheetData>().First();

            foreach (var row in sheet.Elements<Row>().Skip(1))
            {
                var nombre = GetCellValue(wb, row, 0);
                var cargo = GetCellValue(wb, row, 2);
                if (string.IsNullOrWhiteSpace(nombre)) continue;

                firmas.TryGetValue((int)row.RowIndex!.Value, out var firmaBytes);
                var firmante = await firmanteService.ObtenerFirmantePorNombre(nombre);

                if (firmante == null)
                {
                    nuevos.Add(new Firmante { NombreCompleto = nombre, Cargo = cargo, Firma = firmaBytes });
                }
                else
                {
                    bool mod = false;
                    void Set<T>(T value, T current, Action<T> setter)
                    {
                        if (value != null && !value.Equals(current)) { setter(value); mod = true; }
                    }

                    Set(nombre, firmante.NombreCompleto, v => firmante.NombreCompleto = v);
                    Set(cargo, firmante.Cargo, v => firmante.Cargo = v);
                    if (firmaBytes != null && (firmante.Firma == null || !firmante.Firma.SequenceEqual(firmaBytes)))
                    { firmante.Firma = firmaBytes; mod = true; }

                    if (mod) cambios.Add(firmante);
                }
            }

            foreach (var f in nuevos) await firmanteService.AgregarFirmanteAsync(f);
            foreach (var f in cambios) await firmanteService.ActualizarFirmanteAsync(f);

            return [nuevos, cambios];
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private string ObtenerRutaExcel()
        {
            var fullFolder = Path.Combine(env.ContentRootPath, _path);
            var file = Directory.GetFiles(fullFolder, "*.xlsx").FirstOrDefault()
                       ?? throw new FileNotFoundException("No se encontró ningún archivo Excel en la carpeta configurada.");
            return file;
        }

        internal static readonly Dictionary<string, string[]> AliasesCamposClave = new()
        {
            { "email", ["email", "correo", "correo_electronico", "mail"] },
            { "nombrecompleto", ["nombre", "nombre_completo", "nombrecompleto"] },
            { "cedula", ["cedula", "numero_id", "id", "identificacion"] },
            { "empresaid", ["nit_empresa", "nit", "empresa"] },
            { "fechaingreso", ["fecha_ingreso", "fechaingreso", "fecha_de_ingreso"] },
            { "cargo", ["cargo"] },
            { "salariomensual", ["sueldo", "salario", "salario_mensual", "salariomensual"] },
            { "tiposalario", ["tipo_sueldo", "tipo_salario", "tiposalario"] },
            { "tipocontrato", ["tipo_contrato", "tipocontrato"] },
            { "ciudaddetrabajo", ["ciudad_trabajo", "ciudad_de_trabajo", "ciudaddetrabajo"] },
            { "tipoid", ["tipo_id", "tipoid"] }
        };

        internal static bool EsCampoFijoConocido(string clave) =>
            AliasesCamposClave.Values.Any(aliases => aliases.Contains(clave));

        private static string NormalizarClave(string input) =>
            input.ToLowerInvariant()
                 .Normalize(NormalizationForm.FormD)
                 .Where(c => char.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                 .Aggregate("", (a, b) => a + b)
                 .Replace(" ", "_")
                 .Replace(".", "")
                 .Replace("-", "");

        private int BuscarIndice(List<string> headers, string campo)
        {
            if (!AliasesCamposClave.ContainsKey(campo)) return -1;
            var aliases = AliasesCamposClave[campo];
            return headers.FindIndex(h => aliases.Contains(NormalizarClave(h)));
        }

        private async Task<int> ObtenerEmpresaId(List<string?> values, List<string> headers)
        {
            var index = BuscarIndice(headers, "empresaid");
            if (index < 0) return 0;
            var nit = values[index];
            if (string.IsNullOrWhiteSpace(nit)) return 0;
            var empresa = await empresaService.ObtenerEmpresaPorNitAsync(nit);
            return empresa?.Id ?? 0;
        }

        private static string? GetCellValue(WorkbookPart wb, Row row, int index)
        {
            var cell = row.Elements<Cell>().ElementAtOrDefault(index);
            if (cell == null) return null;

            var value = cell.InnerText;

            if (cell.DataType?.Value == CellValues.SharedString)
            {
                return wb.SharedStringTablePart!
                    .SharedStringTable!
                    .Elements<SharedStringItem>()
                    .ElementAt(int.Parse(value))
                    .InnerText
                    .Trim();
            }

            if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var oaDate)
                && oaDate > 30000 && oaDate < 60000)
            {
                try { return DateTime.FromOADate(oaDate).ToString("yyyy-MM-dd"); }
                catch { /* devolver como texto */ }
            }

            return value.Trim();
        }

        private static Dictionary<int, byte[]> ObtenerImagenesPorFila(WorksheetPart ws)
        {
            var dict = new Dictionary<int, byte[]>();
            if (ws.DrawingsPart == null) return dict;

            foreach (var anchor in ws.DrawingsPart.WorksheetDrawing!.Elements<Xdr.TwoCellAnchor>())
            {
                var pic = anchor.Elements<Xdr.Picture>().FirstOrDefault();
                if (pic?.BlipFill?.Blip?.Embed == null) continue;

                var imgPart = ws.DrawingsPart.GetPartById(pic.BlipFill.Blip.Embed.Value!) as ImagePart;
                if (imgPart == null) continue;

                using var s = imgPart.GetStream();
                using var imgMs = new MemoryStream();
                s.CopyTo(imgMs);

                if (int.TryParse(anchor.FromMarker?.RowId?.Text, out var rowIdx))
                    dict[rowIdx + 1] = imgMs.ToArray();
            }

            return dict;
        }

        private static DateOnly? ParseExcelDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var oa))
            {
                try { return DateOnly.FromDateTime(DateTime.FromOADate(oa)); }
                catch { }
            }

            if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var isoDate))
                return DateOnly.FromDateTime(isoDate);

            if (DateTime.TryParse(value, out var parsed))
                return DateOnly.FromDateTime(parsed);

            return null;
        }
    }
}
