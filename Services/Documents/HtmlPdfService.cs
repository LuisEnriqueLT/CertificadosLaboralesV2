using DinkToPdf;
using DinkToPdf.Contracts;
using CertificadosLaboralesV2.Models;
using CertificadosLaboralesV2.Services.Core;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System.Drawing;
using System.Drawing.Imaging;

namespace CertificadosLaboralesV2.Services.Documents
{
    public class HtmlPdfService(IConverter converter, ILogger<HtmlPdfService> logger, FuenteService fuenteService)
    {
        // wkhtmltopdf renderiza el HTML como un único lienzo continuo y luego lo corta en páginas:
        // un position:fixed con height:100% se mide contra ese lienzo completo, no contra una página
        // física, así que la marca solo aparece UNA vez (en medio del documento) y nunca llega al
        // margen inferior real. Por eso la marca ya no se inyecta en el HTML: se "estampa" después,
        // con PdfSharp, directamente sobre cada página del PDF ya generado.
        static HtmlPdfService()
        {
            GlobalFontSettings.FontResolver ??= new WatermarkFontResolver();
        }

        public async Task<byte[]> HtmlToPdf(
            string htmlContent,
            string? qrBase64 = null,
            string? marcaDeAgua = null,
            TipoMarcaAgua tipoMarca = TipoMarcaAgua.Ninguna,
            double opacidad = 0.08,
            double posX = 50,
            double posY = 50,
            double tamano = 80,
            int rotacion = -35)
        {
            logger.LogDebug("Generando PDF. QR: {HasQr}", qrBase64 != null ? "sí" : "no");

            var fuentes = await fuenteService.ObtenerFuentesActivasAsync();

            string? footerTempPath = null;
            var fontCss = new System.Text.StringBuilder();
            var tempFontPaths = new List<string>();
            try
            {
                var tempDir = Path.Combine(Path.GetTempPath(), "certlab_fonts");
                Directory.CreateDirectory(tempDir);
                foreach (var f in fuentes)
                {
                    var tempPath = Path.Combine(tempDir, $"{f.Slug}{f.Extension}");
                    await File.WriteAllBytesAsync(tempPath, f.Archivo);
                    tempFontPaths.Add(tempPath);
                    var uri = "file:///" + tempPath.Replace("\\", "/");
                    fontCss.AppendLine($"@font-face {{ font-family: '{f.Nombre}'; src: url('{uri}'); }}");
                }

                var objectSettings = new ObjectSettings
                {
                    HtmlContent = BuildHtml(htmlContent, fontCss.ToString()),
                    WebSettings = { DefaultEncoding = "utf-8" }
                };

                if (!string.IsNullOrEmpty(qrBase64))
                {
                    if (!System.Text.RegularExpressions.Regex.IsMatch(qrBase64, @"^[A-Za-z0-9+/=]+$"))
                        throw new ArgumentException("Contenido QR inválido.", nameof(qrBase64));

                    footerTempPath = Path.Combine(Path.GetTempPath(), $"sello_{Guid.NewGuid():N}.html");
                    await File.WriteAllTextAsync(footerTempPath, BuildFooterHtml(qrBase64));

                    objectSettings.FooterSettings = new FooterSettings
                    {
                        HtmUrl = footerTempPath,
                        Spacing = 3
                    };
                }

                var doc = new HtmlToPdfDocument
                {
                    GlobalSettings = new GlobalSettings
                    {
                        PaperSize = PaperKind.Letter,
                        Orientation = Orientation.Portrait,
                        Margins = new MarginSettings { Top = 0, Bottom = 35, Left = 0, Right = 0 }
                    },
                    Objects = { objectSettings }
                };

                var pdfBytes = converter.Convert(doc);

                if (tipoMarca != TipoMarcaAgua.Ninguna && !string.IsNullOrEmpty(marcaDeAgua))
                    pdfBytes = StampWatermarkOnEveryPage(pdfBytes, tipoMarca, marcaDeAgua, opacidad, posX, posY, tamano, rotacion);

                return pdfBytes;
            }
            finally
            {
                if (footerTempPath != null && File.Exists(footerTempPath))
                    File.Delete(footerTempPath);
                foreach (var p in tempFontPaths)
                    if (File.Exists(p)) File.Delete(p);
            }
        }

        // Para previews en el navegador: position:absolute dentro de un wrapper relative con
        // overflow:hidden, así queda contenida en la tarjeta del documento (siempre es una sola
        // "página" visual, así que el problema de repetición/paginación de wkhtmltopdf no aplica aquí).
        public static string WrapWithWatermarkHtml(
            TipoMarcaAgua tipoMarca,
            string? marcaDeAgua,
            double opacidad,
            string htmlContent,
            double posX = 50,
            double posY = 50,
            double tamano = 80,
            int rotacion = -35)
        {
            if (tipoMarca == TipoMarcaAgua.Ninguna || string.IsNullOrEmpty(marcaDeAgua))
                return htmlContent;

            var overlay = BuildWatermarkDiv(tipoMarca, marcaDeAgua, opacidad, posX, posY, tamano, rotacion);
            // Sin wrapper adicional: el contenedor position:relative es el .paper-doc del caller.
            // La marca (position:absolute;inset:0) se estira para cubrir toda el área del papel, incluyendo el padding.
            return $"{overlay}{htmlContent}";
        }

        private static string GetImageMimeType(string base64)
        {
            if (base64.StartsWith("iVBOR")) return "image/png";
            if (base64.StartsWith("/9j/")) return "image/jpeg";
            if (base64.StartsWith("R0lG")) return "image/gif";
            if (base64.StartsWith("UklG")) return "image/webp";
            return "image/png";
        }

        private static string BuildHtml(string htmlContent, string customFontCss = "")
        {
            return @$"
<html>
<head>
  <meta charset='UTF-8'>
  <style>
    {customFontCss}
    body {{ margin: 0; padding: 25mm 25mm 0 25mm; font-size: 14px; line-height: 1.6; }}
    blockquote {{ border-left: 4px solid #ccc; margin: 1em 0; padding-left: 1em; color: #666; }}
    pre {{ background-color: #f4f4f4; padding: 1em; overflow: auto; }}
    code {{ font-family: monospace; background-color: #eee; padding: 2px 4px; }}
    h1 {{ font-size: 2em; font-weight: bold; }} h2 {{ font-size: 1.5em; font-weight: bold; }}
    h3 {{ font-size: 1.17em; font-weight: bold; }}
    ol, ul {{ padding-left: 2em; }} li {{ margin-bottom: 5px; }}
  </style>
</head>
<body>
  <div>{htmlContent}</div>
</body>
</html>";
        }

        // Genera el div de marca para el preview en navegador (position:absolute dentro de un
        // contenedor relative con overflow:hidden, contenido en la tarjeta del documento).
        private static string BuildWatermarkDiv(
            TipoMarcaAgua tipoMarca,
            string? marcaDeAgua,
            double opacidad,
            double posX,
            double posY,
            double tamano,
            int rotacion)
        {
            if (tipoMarca == TipoMarcaAgua.Ninguna || string.IsNullOrEmpty(marcaDeAgua))
                return "";

            var op = opacidad.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            var pxStr = posX.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
            var pyStr = posY.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
            var szStr = tamano.ToString("F0", System.Globalization.CultureInfo.InvariantCulture);
            var xform = $"-webkit-transform:translate(-50%,-50%) rotate({rotacion}deg);transform:translate(-50%,-50%) rotate({rotacion}deg);";
            var outer = "position:absolute;top:0;left:0;right:0;bottom:0;overflow:hidden;z-index:9999;pointer-events:none;";
            var innerPos = $"position:absolute;top:{pyStr}%;left:{pxStr}%;{xform}";

            if (tipoMarca == TipoMarcaAgua.Texto)
            {
                var escaped = System.Net.WebUtility.HtmlEncode(marcaDeAgua);
                return $"<div style=\"{outer}\">" +
                       $"<div style=\"{innerPos}font-size:{szStr}px;font-weight:700;color:rgba(100,116,139,{op});white-space:nowrap;user-select:none;\">{escaped}</div>" +
                       $"</div>";
            }

            var mime = GetImageMimeType(marcaDeAgua);
            return $"<div style=\"{outer}\">" +
                   $"<img src=\"data:{mime};base64,{marcaDeAgua}\" " +
                   $"style=\"{innerPos}max-width:{szStr}%;max-height:{szStr}%;object-fit:contain;opacity:{op};\" />" +
                   $"</div>";
        }

        // Estampa la marca de agua directamente sobre cada página del PDF ya generado, en
        // coordenadas reales de página (0..ancho, 0..alto). Esto garantiza que se repita en
        // todas las hojas y que pueda llegar hasta el borde inferior real de la página.
        private static byte[] StampWatermarkOnEveryPage(
            byte[] pdfBytes,
            TipoMarcaAgua tipoMarca,
            string marcaDeAgua,
            double opacidad,
            double posX,
            double posY,
            double tamano,
            int rotacion)
        {
            using var input = new MemoryStream(pdfBytes);
            using var doc = PdfReader.Open(input, PdfDocumentOpenMode.Modify);

            byte[]? watermarkPngBytes = null;
            double imageAspect = 1;
            if (tipoMarca == TipoMarcaAgua.Imagen)
            {
                watermarkPngBytes = BuildTranslucentPng(marcaDeAgua, opacidad, out imageAspect);
            }

            var font = tipoMarca == TipoMarcaAgua.Texto ? new XFont("WatermarkFont", tamano * 0.75, XFontStyleEx.Bold) : null;
            var brush = new XSolidBrush(XColor.FromArgb((int)Math.Clamp(opacidad * 255, 0, 255), 100, 116, 139));

            foreach (var page in doc.Pages.Cast<PdfPage>())
            {
                using var gfx = XGraphics.FromPdfPage(page);
                var pageWidth = page.Width.Point;
                var pageHeight = page.Height.Point;

                // Recorta al área real de la página: si la marca queda cerca de un borde, se ve
                // cortada por el límite de la hoja (igual que el overflow:hidden del HTML), en vez
                // de desplazarse hacia adentro para no salirse.
                gfx.IntersectClip(new XRect(0, 0, pageWidth, pageHeight));

                var cx = pageWidth * (posX / 100.0);
                var cy = pageHeight * (posY / 100.0);

                if (tipoMarca == TipoMarcaAgua.Texto && font != null)
                {
                    var size = gfx.MeasureString(marcaDeAgua, font);
                    gfx.TranslateTransform(cx, cy);
                    gfx.RotateTransform(rotacion);
                    gfx.DrawString(marcaDeAgua, font, brush, new XPoint(-size.Width / 2, size.Height / 2));
                }
                else if (watermarkPngBytes != null)
                {
                    using var imgStream = new MemoryStream(watermarkPngBytes);
                    using var ximg = XImage.FromStream(imgStream);
                    var boxW = pageWidth * (tamano / 100.0);
                    var boxH = pageHeight * (tamano / 100.0);
                    double drawW = boxW, drawH = boxW / imageAspect;
                    if (drawH > boxH) { drawH = boxH; drawW = boxH * imageAspect; }

                    gfx.TranslateTransform(cx, cy);
                    gfx.RotateTransform(rotacion);
                    gfx.DrawImage(ximg, -drawW / 2, -drawH / 2, drawW, drawH);
                }
            }

            using var output = new MemoryStream();
            doc.Save(output);
            return output.ToArray();
        }

        // Decodifica la imagen base64 y multiplica su canal alfa por la opacidad configurada,
        // horneando la transparencia en los píxeles (PdfSharp no expone opacidad para DrawImage).
#pragma warning disable CA1416 // System.Drawing.Common: esta app solo se despliega en Windows (ya depende de libwkhtmltox.dll nativo)
        private static byte[] BuildTranslucentPng(string base64, double opacidad, out double aspectRatio)
        {
            var raw = Convert.FromBase64String(base64);
            using var srcStream = new MemoryStream(raw);
            using var src = System.Drawing.Image.FromStream(srcStream);
            aspectRatio = (double)src.Width / src.Height;

            using var dest = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
            using var g = System.Drawing.Graphics.FromImage(dest);
            var alpha = (float)Math.Clamp(opacidad, 0, 1);
            var matrix = new ColorMatrix { Matrix33 = alpha };
            using var attributes = new ImageAttributes();
            attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
            g.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, attributes);

            using var outStream = new MemoryStream();
            dest.Save(outStream, ImageFormat.Png);
            return outStream.ToArray();
        }
#pragma warning restore CA1416

        private static string BuildFooterHtml(string qrBase64)
        {
            return $@"<html>
<head>
  <meta charset='UTF-8'>
  <style>
    body {{ margin: 0; padding: 4px 25mm; font-family: Arial, sans-serif; }}
    .sello {{ border-top: 1px solid #cbd5e1; padding-top: 6px; display: flex; align-items: center; gap: 14px; max-height: 76px; box-sizing: border-box; overflow: hidden; }}
    .sello img {{ width: 56px; height: 56px; flex-shrink: 0; }}
    .sello-title {{ font-weight: 700; font-size: 8pt; color: #1e293b; letter-spacing: 0.3px; }}
    .sello-sub {{ font-size: 7pt; color: #64748b; margin-top: 2px; }}
  </style>
</head>
<body>
  <div class='sello'>
    <img src='data:image/png;base64,{qrBase64}' />
    <div>
      <div class='sello-title'>SELLO DE AUTENTICIDAD</div>
      <div class='sello-sub'>Escanea el c&#243;digo QR para verificar este documento</div>
    </div>
  </div>
</body>
</html>";
        }

        private sealed class WatermarkFontResolver : IFontResolver
        {
            public byte[]? GetFont(string faceName)
            {
                var fontsDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
                var path = Path.Combine(fontsDir, "arialbd.ttf");
                return File.Exists(path) ? File.ReadAllBytes(path) : File.ReadAllBytes(Path.Combine(fontsDir, "arial.ttf"));
            }

            public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) => new("WatermarkFont");
        }
    }
}
