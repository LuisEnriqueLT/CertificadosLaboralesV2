using CertificadosLaboralesV2.Data;
using CertificadosLaboralesV2.Models;
using CertificadosLaboralesV2.Services.Core;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Cryptography;

namespace CertificadosLaboralesV2.Services.Documents
{
    public class CreateDocService(
        EmpleadoService empleadoService,
        EmpresaService empresaService,
        FirmanteService firmanteService,
        PlantillaService plantillaService,
        PlaceholderService placeholderService,
        ReplaceService replaceService,
        HtmlPdfService htmlPdfService,
        HtmlDocxService htmlDocxService,
        QrCodeService qrCodeService,
        IConfiguration configuration,
        IDbContextFactory<AppDbContext> contextFactory)
    {
        public async Task<(byte[] Contenido, string NombreArchivo)> GenerarDocumentoAsync(
            int plantillaId = 0,
            int empresaId = 0,
            int empleadoId = 0,
            int creadoPorId = 0,
            string htmlContent = "")
        {
            if (empresaId > 0 && plantillaId > 0 && empleadoId > 0)
            {
                var empleado = await empleadoService.ObtenerEmpleadoPorIdAsync(empleadoId);
                var empresa = await empresaService.ObtenerEmpresaPorIdAsync(empresaId);
                if (empresa == null) throw new InvalidOperationException($"Empresa {empresaId} no encontrada.");
                var firmante = await firmanteService.ObtenerFirmantePorIdAsync(empresa.RepresentanteLegalId);
                var plantilla = await plantillaService.ObtenerPlantillaPorIdAsync(plantillaId);
                if (plantilla == null) throw new InvalidOperationException($"Plantilla {plantillaId} no encontrada.");

                var reemplazos = await GenerarDiccionarioReemplazosAsync(empresa, empleado, firmante);
                var html = replaceService.ReplacePlaceholders(plantilla.HtmlContenido, reemplazos);
                var pdf = await htmlPdfService.HtmlToPdf(html);
                return (pdf, $"{plantilla.NombrePlantilla}_{empleado?.NombreCompleto}.pdf");
            }

            if (!string.IsNullOrWhiteSpace(htmlContent))
            {
                var reemplazos = await GenerarDiccionarioReemplazosAsync();
                var html = replaceService.ReplacePlaceholders(htmlContent, reemplazos);
                var pdf = await htmlPdfService.HtmlToPdf(html);
                return (pdf, "PREVIEW.pdf");
            }

            throw new ArgumentException("Datos insuficientes para generar el documento.");
        }

        public async Task<(byte[] Contenido, string NombreArchivo)> GenerarDocumentoSinHashAsync(
            int plantillaId, int empresaId, int empleadoId, int creadoPorId)
        {
            using var context = contextFactory.CreateDbContext();
            var (pdfFinal, empleado, _) = await GenerarPdfInternoAsync(plantillaId, empresaId, empleadoId);

            var hash = CalcularHash(pdfFinal);
            context.Historial.Add(new Historial
            {
                EmpresaId = empresaId,
                CreadoPorId = creadoPorId,
                CreadoParaId = empleadoId,
                Contenido = pdfFinal,
                Hash = hash,
                FechaCreacion = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            return (pdfFinal, $"{empleado?.NombreCompleto}_{DateTime.UtcNow:yyyyMMddHHmm}.pdf");
        }

        public async Task<(byte[] Contenido, string NombreArchivo)> GenerarDocumentoConHashAsync(
            int plantillaId, int empresaId, int empleadoId, int creadoPorId)
            => await GenerarDocumentoPdfAsync(plantillaId, empresaId, empleadoId, creadoPorId);

        public async Task<(byte[] Contenido, string NombreArchivo)> GenerarDocumentoDocxAsync(
            int plantillaId, int empresaId, int empleadoId)
        {
            var plantilla = await plantillaService.ObtenerPlantillaPorIdAsync(plantillaId);
            var empresa = await empresaService.ObtenerEmpresaPorIdAsync(empresaId);
            if (empresa == null) throw new InvalidOperationException($"Empresa {empresaId} no encontrada.");
            var empleado = await empleadoService.ObtenerEmpleadoPorIdAsync(empleadoId);
            var firmante = await firmanteService.ObtenerFirmantePorIdAsync(empresa.RepresentanteLegalId);

            var reemplazos = await GenerarDiccionarioReemplazosAsync(empresa, empleado, firmante);
            var html = replaceService.ReplacePlaceholders(plantilla!.HtmlContenido, reemplazos);
            var docxBytes = await htmlDocxService.ConvertirHtmlADocxAsync(html);

            return (docxBytes, $"{plantilla.NombrePlantilla}_{empleado?.NombreCompleto}.docx");
        }

        public async Task<string> ObtenerHtmlPreviewAsync(
            int plantillaId, int empresaId, int empleadoId, bool sinSello = false)
        {
            var plantilla = await plantillaService.ObtenerPlantillaPorIdAsync(plantillaId);
            if (plantilla == null) return "";
            var empresa = await empresaService.ObtenerEmpresaPorIdAsync(empresaId);
            var empleado = await empleadoService.ObtenerEmpleadoPorIdAsync(empleadoId);
            var firmante = empresa != null
                ? await firmanteService.ObtenerFirmantePorIdAsync(empresa.RepresentanteLegalId)
                : null;

            var reemplazos = await GenerarDiccionarioReemplazosAsync(empresa, empleado, firmante);
            if (sinSello)
            {
                reemplazos["{{LogoEmpresa}}"] = "";
                reemplazos["{{Firma}}"] = "";
            }
            var html = replaceService.ReplacePlaceholders(plantilla.HtmlContenido, reemplazos);
            return HtmlPdfService.WrapWithWatermarkHtml(
                plantilla.TipoMarcaAgua,
                plantilla.MarcaDeAgua,
                plantilla.OpacidadMarcaAgua,
                html,
                plantilla.PosicionXMarcaAgua,
                plantilla.PosicionYMarcaAgua,
                plantilla.TamanoMarcaAgua,
                plantilla.RotacionMarcaAgua);
        }

        public async Task<(byte[] Contenido, string NombreArchivo)> GenerarDocumentoPdfAsync(
            int plantillaId, int empresaId, int empleadoId, int creadoPorId, bool sinSello = false)
        {
            using var context = contextFactory.CreateDbContext();

            var codigo = Guid.NewGuid();
            var baseUrl = configuration["BaseUrl"] ?? "http://localhost:5000";
            var qrBase64 = sinSello ? null : qrCodeService.GenerarQr($"{baseUrl}/verificar/{codigo}");

            var (pdfFinal, empleado, nombrePlantilla) = await GenerarPdfInternoAsync(
                plantillaId, empresaId, empleadoId, sinSello, qrBase64);

            var hash = CalcularHash(pdfFinal);
            context.Historial.Add(new Historial
            {
                EmpresaId = empresaId,
                CreadoPorId = creadoPorId,
                CreadoParaId = empleadoId,
                Contenido = pdfFinal,
                Hash = hash,
                CodigoVerificacion = codigo,
                NombreDocumento = nombrePlantilla,
                FechaCreacion = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            return (pdfFinal, $"{empleado?.NombreCompleto}_{DateTime.UtcNow:yyyyMMddHHmm}.pdf");
        }

        public async Task<byte[]> GenerarPreviewAsync(string html)
        {
            var reemplazos = await GenerarDiccionarioPreviewAsync();
            var htmlFinal = replaceService.ReplacePlaceholders(html, reemplazos);
            return await htmlPdfService.HtmlToPdf(htmlFinal);
        }

        // ============================================================
        // HELPERS PRIVADOS
        // ============================================================

        private async Task<(byte[] Pdf, Empleado? Empleado, string NombrePlantilla)> GenerarPdfInternoAsync(
            int plantillaId, int empresaId, int empleadoId, bool sinSello = false, string? qrBase64 = null)
        {
            var plantilla = await plantillaService.ObtenerPlantillaPorIdAsync(plantillaId);
            var empresa = await empresaService.ObtenerEmpresaPorIdAsync(empresaId);
            if (empresa == null) throw new InvalidOperationException($"Empresa {empresaId} no encontrada.");
            var empleado = await empleadoService.ObtenerEmpleadoPorIdAsync(empleadoId);
            var firmante = await firmanteService.ObtenerFirmantePorIdAsync(empresa.RepresentanteLegalId);

            var reemplazos = await GenerarDiccionarioReemplazosAsync(empresa, empleado, firmante);
            if (sinSello)
            {
                reemplazos["{{LogoEmpresa}}"] = "";
                reemplazos["{{Firma}}"] = "";
            }
            var html = replaceService.ReplacePlaceholders(plantilla!.HtmlContenido, reemplazos);
            return (await htmlPdfService.HtmlToPdf(
                html,
                qrBase64,
                plantilla!.MarcaDeAgua,
                plantilla.TipoMarcaAgua,
                plantilla.OpacidadMarcaAgua,
                plantilla.PosicionXMarcaAgua,
                plantilla.PosicionYMarcaAgua,
                plantilla.TamanoMarcaAgua,
                plantilla.RotacionMarcaAgua), empleado, plantilla?.NombrePlantilla ?? "");
        }

        private async Task<Dictionary<string, string>> GenerarDiccionarioReemplazosAsync(
            Empresa? empresa = null,
            Empleado? empleado = null,
            Firmante? firmante = null)
        {
            var reemplazos = new Dictionary<string, string>();

            var logo = Convert.ToBase64String(empresa?.Logo ?? Array.Empty<byte>());
            var firma = Convert.ToBase64String(firmante?.Firma ?? Array.Empty<byte>());

            // Valores HTML (no se escapan)
            reemplazos["{{LogoEmpresa}}"] = $"<img src=\"data:image/png;base64,{logo}\" style=\"width:200px;height:auto;\"/>";
            reemplazos["{{Firma}}"] = $"<img src=\"data:image/png;base64,{firma}\" style=\"width:200px;height:auto;\"/>";

            // Valores de texto (se escapan en ReplaceService)
            reemplazos["{{FirmaBase64}}"] = firma;
            reemplazos["{{NombreEmpresa}}"] = empresa?.Nombre ?? "";
            reemplazos["{{EmpresaId}}"] = empresa?.Nit ?? "";
            reemplazos["{{DomicilioEmpresa}}"] = empresa?.Domicilio ?? "";
            reemplazos["{{TelefonoEmpresa}}"] = empresa?.Telefono?.ToString() ?? "";
            reemplazos["{{CorreoEmpresa}}"] = empresa?.Email ?? "";
            reemplazos["{{NombreEmpleado}}"] = empleado?.NombreCompleto ?? "";
            reemplazos["{{CorreoEmpleado}}"] = empleado?.Email ?? "";
            reemplazos["{{CedulaEmpleado}}"] = empleado?.Cedula ?? "";
            reemplazos["{{Firmante}}"] = firmante?.NombreCompleto ?? "";
            reemplazos["{{CargoFirmante}}"] = firmante?.Cargo ?? "";
            reemplazos["{{Dia}}"] = DateTime.Now.Day.ToString();
            reemplazos["{{DiaStr}}"] = NumToWords(DateTime.Now.Day);
            reemplazos["{{Mes}}"] = DateTime.Now.ToString("MMMM", new CultureInfo("es-CO"));
            reemplazos["{{Anio}}"] = DateTime.Now.Year.ToString();
            reemplazos["{{MesNum}}"] = DateTime.Now.Month.ToString("D2");
            reemplazos["{{FechaCompleta}}"] = DateTime.Now.ToString("d 'de' MMMM 'de' yyyy", new CultureInfo("es-CO"));
            reemplazos["{{FechaCorta}}"] = DateTime.Now.ToString("dd/MM/yyyy");
            reemplazos["{{FechaISO}}"] = DateTime.Now.ToString("yyyy-MM-dd");

            var dinamicos = await ObtenerReemplazosDesdePlaceholdersAsync(empleado);
            foreach (var kv in dinamicos)
                reemplazos[kv.Key] = kv.Value;

            string? salarioRaw = null;
            if (!string.IsNullOrWhiteSpace(empleado?.DatosVariables))
            {
                var datos = DatosVariablesHelper.Parse(empleado.DatosVariables);
                datos.TryGetValue("sueldo", out salarioRaw);
            }
            salarioRaw ??= empleado?.SalarioMensual;

            var salarioInt = ParseSalarioToInt(salarioRaw);
            if (salarioInt.HasValue)
            {
                reemplazos["{{salario}}"] = salarioInt.Value.ToString("N0", new CultureInfo("es-US"));
                reemplazos["{{salario_str}}"] = NumToWords(salarioInt.Value);
            }

            return reemplazos;
        }

        private async Task<Dictionary<string, string>> ObtenerReemplazosDesdePlaceholdersAsync(Empleado? empleado)
        {
            var resultado = new Dictionary<string, string>();
            var datos = DatosVariablesHelper.Parse(empleado?.DatosVariables);
            var placeholders = await placeholderService.ObtenerTodosLosPlaceholdersAsync();

            foreach (var p in placeholders)
            {
                string? valor;
                if (p.DatoVariableId.HasValue && p.DatoVariable != null)
                {
                    if (!datos.TryGetValue(p.DatoVariable.Clave, out valor)) continue;
                }
                else if (!string.IsNullOrWhiteSpace(p.CampoFijo))
                {
                    valor = CamposFijosEmpleado.ObtenerValor(empleado, p.CampoFijo);
                    if (valor == null) continue;
                }
                else continue;

                resultado[$"{{{{{p.PlaceholderTexto}}}}}"] = valor ?? "";

                if (p.DatoVariableId.HasValue && p.DatoVariable?.Clave == "salario")
                {
                    resultado["{{salario_formato}}"] = SalarioFormato(valor);
                    if (int.TryParse(valor?.Replace(".", "").Replace(",", ""), out var salarioNum))
                        resultado["{{salario_str}}"] = NumToWords(salarioNum);
                }
            }

            return resultado;
        }

        private async Task<Dictionary<string, string>> GenerarDiccionarioPreviewAsync()
        {
            var reemplazos = new Dictionary<string, string>
            {
                ["{{NombreEmpresa}}"] = "EMPRESA DEMO",
                ["{{DomicilioEmpresa}}"] = "Dirección de ejemplo",
                ["{{TelefonoEmpresa}}"] = "000 000 0000",
                ["{{CorreoEmpresa}}"] = "empresa@demo.com",
                ["{{NombreEmpleado}}"] = "EMPLEADO DEMO",
                ["{{CorreoEmpleado}}"] = "empleado@demo.com",
                ["{{CedulaEmpleado}}"] = "000000000",
                ["{{Firmante}}"] = "FIRMANTE DEMO",
                ["{{CargoFirmante}}"] = "CARGO DEMO",
                ["{{Dia}}"] = DateTime.Now.Day.ToString(),
                ["{{DiaStr}}"] = NumToWords(DateTime.Now.Day),
                ["{{Mes}}"] = DateTime.Now.ToString("MMMM", new CultureInfo("es-CO")),
                ["{{Anio}}"] = DateTime.Now.Year.ToString(),
                ["{{MesNum}}"] = DateTime.Now.Month.ToString("D2"),
                ["{{FechaCompleta}}"] = DateTime.Now.ToString("d 'de' MMMM 'de' yyyy", new CultureInfo("es-CO")),
                ["{{FechaCorta}}"] = DateTime.Now.ToString("dd/MM/yyyy"),
                ["{{FechaISO}}"] = DateTime.Now.ToString("yyyy-MM-dd"),
                ["{{salario_formato}}"] = "$0",
                ["{{salario_str}}"] = "CERO PESOS"
            };

            var placeholders = await placeholderService.ObtenerTodosLosPlaceholdersAsync();
            foreach (var p in placeholders)
                reemplazos[$"{{{{{p.PlaceholderTexto}}}}}"] = $"[{p.Texto}]";

            return reemplazos;
        }

        private static string SalarioFormato(string? salario)
        {
            if (decimal.TryParse(salario, out var dec))
                return dec.ToString("c");
            return salario ?? "";
        }

        public string CalcularHash(byte[] pdfBytes)
        {
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(pdfBytes));
        }

        private static int? ParseSalarioToInt(string? salarioRaw)
        {
            if (string.IsNullOrWhiteSpace(salarioRaw)) return null;

            salarioRaw = salarioRaw.Replace("$", "").Trim();

            if (decimal.TryParse(salarioRaw, NumberStyles.Number | NumberStyles.AllowCurrencySymbol,
                new CultureInfo("es-ES"), out var v1))
                return (int)Math.Round(v1, MidpointRounding.AwayFromZero);

            if (decimal.TryParse(salarioRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v2))
                return (int)Math.Round(v2, MidpointRounding.AwayFromZero);

            return null;
        }

        public static string NumToWords(int number)
        {
            if (number == 0) return "CERO";
            if (number < 0) return "MENOS " + NumToWords(Math.Abs(number));

            string words = "";

            if ((number / 1000000) > 0)
            {
                words += (number / 1000000 == 1) ? "UN MILLÓN " : NumToWords(number / 1000000) + " MILLONES ";
                number %= 1000000;
            }

            if ((number / 1000) > 0)
            {
                words += (number / 1000 == 1) ? "MIL " : NumToWords(number / 1000) + " MIL ";
                number %= 1000;
            }

            if ((number / 100) > 0)
            {
                var hundreds = number / 100;
                words += hundreds switch
                {
                    1 when number % 100 == 0 => "CIEN ",
                    1 => "CIENTO ",
                    5 => "QUINIENTOS ",
                    7 => "SETECIENTOS ",
                    9 => "NOVECIENTOS ",
                    _ => NumToWords(hundreds) + "CIENTOS "
                };
                number %= 100;
            }

            if (number > 0)
            {
                string[] units = ["", "UNO", "DOS", "TRES", "CUATRO", "CINCO", "SEIS", "SIETE", "OCHO", "NUEVE",
                                   "DIEZ", "ONCE", "DOCE", "TRECE", "CATORCE", "QUINCE", "DIECISÉIS", "DIECISIETE", "DIECIOCHO", "DIECINUEVE"];
                string[] tens = ["", "", "VEINTE", "TREINTA", "CUARENTA", "CINCUENTA", "SESENTA", "SETENTA", "OCHENTA", "NOVENTA"];

                if (number < 20) words += units[number];
                else
                {
                    words += tens[number / 10];
                    if (number % 10 > 0) words += " Y " + units[number % 10];
                }
            }

            return words.Trim();
        }
    }
}
