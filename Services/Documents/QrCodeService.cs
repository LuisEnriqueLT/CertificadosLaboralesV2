using QRCoder;

namespace CertificadosLaboralesV2.Services.Documents
{
    public class QrCodeService
    {
        public string GenerarQr(string url)
        {
            using var generator = new QRCodeGenerator();
            var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
            using var qr = new PngByteQRCode(data);
            return Convert.ToBase64String(qr.GetGraphic(5));
        }
    }
}
