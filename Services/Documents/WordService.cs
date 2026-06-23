using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Components.Forms;

namespace CertificadosLaboralesV2.Services.Documents
{
    public class WordService(ILogger<WordService> logger)
    {
        public async Task<string> ExtraerTextoWordAsync(IBrowserFile archivoSeleccionado)
        {
            try
            {
                using var stream = new MemoryStream();
                await archivoSeleccionado.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024).CopyToAsync(stream);
                stream.Position = 0;

                using var wordDoc = WordprocessingDocument.Open(stream, false);
                var body = wordDoc.MainDocumentPart?.Document?.Body;

                if (body == null)
                    return string.Empty;

                var sb = new System.Text.StringBuilder();
                foreach (var p in body.Elements<Paragraph>())
                {
                    var texto = p.InnerText?.Trim();
                    if (!string.IsNullOrWhiteSpace(texto))
                        sb.AppendLine($"<p>{texto}</p>");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error al extraer texto del archivo Word");
                return string.Empty;
            }
        }
    }
}
