using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace CertificadosLaboralesV2.Services.Documents
{
    public class HtmlDocxService
    {
        public Task<byte[]> ConvertirHtmlADocxAsync(string html)
        {
            using var stream = new MemoryStream();
            using (var wordDoc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
            {
                var mainPart = wordDoc.AddMainDocumentPart();
                mainPart.Document = new Document(new Body());

                const string altChunkId = "AltChunkId1";
                var chunk = mainPart.AddAlternativeFormatImportPart(
                    AlternativeFormatImportPartType.Html, altChunkId);

                var htmlBytes = System.Text.Encoding.UTF8.GetBytes(
                    $"<html><head><meta charset=\"UTF-8\"></head><body>{html}</body></html>");

                using (var chunkStream = chunk.GetStream(FileMode.Create, FileAccess.Write))
                    chunkStream.Write(htmlBytes, 0, htmlBytes.Length);

                var altChunk = new AltChunk { Id = altChunkId };
                mainPart.Document.Body!.AppendChild(altChunk);
                mainPart.Document.Save();
            }

            return Task.FromResult(stream.ToArray());
        }
    }
}
