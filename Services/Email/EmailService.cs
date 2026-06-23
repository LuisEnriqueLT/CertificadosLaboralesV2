using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using Microsoft.Extensions.Options;

namespace CertificadosLaboralesV2.Services.Email
{
    public class EmailSettings
    {
        public string SmtpServer { get; set; } = string.Empty;
        public int Port { get; set; }
        public bool EnableSsl { get; set; }
        public string User { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string From { get; set; } = string.Empty;
    }

    public class EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger)
    {
        private readonly EmailSettings _settings = emailSettings.Value;

        public async Task<bool> EnviarCorreoAsync(
            string destinatario,
            string asunto,
            string cuerpo,
            byte[]? pdfBytes = null,
            string nombreArchivo = "")
        {
            if (string.IsNullOrWhiteSpace(destinatario))
            {
                logger.LogWarning("Se intentó enviar un correo sin destinatario.");
                return false;
            }

            var mensaje = new MailMessage();
            mensaje.To.Add(destinatario);
            mensaje.Subject = asunto;
            mensaje.Body = cuerpo;
            mensaje.IsBodyHtml = true;
            mensaje.From = new MailAddress(_settings.From);

            if (pdfBytes != null)
            {
                var attachment = new Attachment(
                    new MemoryStream(pdfBytes), nombreArchivo, MediaTypeNames.Application.Pdf);
                mensaje.Attachments.Add(attachment);
            }

            try
            {
                using var clienteSmtp = new SmtpClient(_settings.SmtpServer, _settings.Port)
                {
                    Credentials = new NetworkCredential(_settings.User, _settings.Password),
                    EnableSsl = _settings.EnableSsl
                };
                await clienteSmtp.SendMailAsync(mensaje);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error enviando email a {Destinatario}", destinatario);
                return false;
            }
        }
    }
}
