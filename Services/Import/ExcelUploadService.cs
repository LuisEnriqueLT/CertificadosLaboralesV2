using Microsoft.AspNetCore.Components.Forms;

namespace CertificadosLaboralesV2.Services.Import
{
    public class ExcelUploadService(
        IConfiguration configuration,
        IWebHostEnvironment env,
        ILogger<ExcelUploadService> logger)
    {
        public bool ExisteArchivo()
        {
            var folder = configuration["ExcelSettings:UploadFolder"];
            if (string.IsNullOrWhiteSpace(folder)) return false;

            var fullFolder = Path.Combine(env.ContentRootPath, folder);
            if (!Directory.Exists(fullFolder)) return false;

            return Directory.GetFiles(fullFolder, "*.xlsx").Any();
        }

        public async Task<string> GuardarExcelAsync(IBrowserFile file, bool sobreEscribir)
        {
            if (!file.Name.EndsWith(".xlsx"))
                throw new InvalidOperationException("El archivo debe ser un Excel (.xlsx).");

            var folder = configuration["ExcelSettings:UploadFolder"];
            if (string.IsNullOrWhiteSpace(folder))
                throw new InvalidOperationException("No se configuró ExcelSettings:UploadFolder.");

            var fullFolder = Path.Combine(env.ContentRootPath, folder);
            if (!Directory.Exists(fullFolder))
                Directory.CreateDirectory(fullFolder);

            if (sobreEscribir)
            {
                var existing = Directory.GetFiles(fullFolder).FirstOrDefault();
                if (existing != null)
                    File.Delete(existing);
            }

            var fileName = $"{Guid.NewGuid()}_{file.Name}";
            var fullPath = Path.Combine(fullFolder, fileName);

            using var stream = new FileStream(fullPath, FileMode.Create);
            await file.OpenReadStream(20 * 1024 * 1024).CopyToAsync(stream);

            logger.LogInformation("Excel guardado en {Path}", fullPath);
            return fullPath;
        }
    }
}
