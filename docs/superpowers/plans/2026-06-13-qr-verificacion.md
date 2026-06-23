# QR Verificación de Documentos — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Añadir un código QR en el pie de cada PDF generado que apunta a una página pública donde cualquier receptor puede verificar la autenticidad del documento.

**Architecture:** Al generar un PDF se crea un `Guid` único, se genera el QR como PNG base64 con `QRCoder`, se inyecta en el HTML antes de convertir a PDF, y se guarda el código en `Historial`. La página pública `/verificar/{codigo}` carga el `Historial` y muestra nombre del empleado, empresa, tipo de documento y fecha de emisión.

**Tech Stack:** QRCoder (NuGet), EF Core migrations, Blazor Server, DinkToPdf

---

## Archivos afectados

| Acción | Archivo |
|--------|---------|
| Modificar | `Models/Historial.cs` |
| Modificar | `Services/Documents/HtmlPdfService.cs` |
| Modificar | `Services/Documents/CreateDocService.cs` |
| Modificar | `Services/Core/HistorialService.cs` |
| Modificar | `Program.cs` |
| Modificar | `appsettings.json` |
| Crear | `Services/Documents/QrCodeService.cs` |
| Crear | `Components/Pages/Verificar.razor` |
| Crear | Migration EF Core |

---

### Task 1: Instalar QRCoder y agregar BaseUrl

**Files:**
- Modify: `appsettings.json`

- [ ] **Step 1: Instalar paquete NuGet**

```bash
dotnet add package QRCoder
```

Salida esperada: `PackageReference for package 'QRCoder' version X.X.X added to file...`

- [ ] **Step 2: Agregar BaseUrl a appsettings.json**

Reemplazar el contenido completo de `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": ""
  },
  "BaseUrl": "http://localhost:5000",
  "EmailSettings": {
    "SmtpServer": "",
    "Port": 587,
    "From": "",
    "User": "",
    "Password": "",
    "EnableSsl": true
  },
  "ExcelSettings": {
    "UploadFolder": "uploads/excel"
  }
}
```

> Nota: cambiar `BaseUrl` a la URL real en producción.

---

### Task 2: Actualizar modelo Historial

**Files:**
- Modify: `Models/Historial.cs`

- [ ] **Step 1: Agregar los dos campos nuevos**

Reemplazar el contenido completo de `Models/Historial.cs`:

```csharp
namespace CertificadosLaboralesV2.Models
{
    public class Historial
    {
        public int Id { get; set; }
        public int EmpresaId { get; set; }
        public int CreadoPorId { get; set; }
        public int CreadoParaId { get; set; }
        public byte[] Contenido { get; set; } = Array.Empty<byte>();
        public string Hash { get; set; } = string.Empty;
        public Guid CodigoVerificacion { get; set; } = Guid.Empty;
        public string NombreDocumento { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    }
}
```

---

### Task 3: Crear migration EF Core y aplicarla

**Files:**
- Create: `Migrations/` (generado automáticamente)

- [ ] **Step 1: Generar la migration**

Ejecutar desde la raíz del proyecto:

```bash
dotnet ef migrations add AddQrVerification
```

Salida esperada: `Done. To undo this action, use 'ef migrations remove'`

Si falla con "No project was found", agregar `--project CertificadosLaboralesV2.csproj`.

- [ ] **Step 2: Aplicar la migration a la base de datos**

```bash
dotnet ef database update
```

Salida esperada: `Applying migration '..._AddQrVerification'.` seguido de `Done.`

---

### Task 4: Crear QrCodeService

**Files:**
- Create: `Services/Documents/QrCodeService.cs`
- Modify: `Program.cs`

- [ ] **Step 1: Crear el servicio**

Crear `Services/Documents/QrCodeService.cs`:

```csharp
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
```

- [ ] **Step 2: Registrar en Program.cs**

En `Program.cs`, después de la línea `builder.Services.AddScoped<CreateDocService>();`, agregar:

```csharp
builder.Services.AddScoped<QrCodeService>();
```

---

### Task 5: Actualizar HtmlPdfService

**Files:**
- Modify: `Services/Documents/HtmlPdfService.cs`

El cambio elimina el footer de texto con hash y en su lugar inyecta el QR como bloque HTML al final del documento.

- [ ] **Step 1: Reemplazar el archivo completo**

```csharp
using DinkToPdf;
using DinkToPdf.Contracts;

namespace CertificadosLaboralesV2.Services.Documents
{
    public class HtmlPdfService(IConverter converter, ILogger<HtmlPdfService> logger)
    {
        public async Task<byte[]> HtmlToPdf(string htmlContent, string? qrBase64 = null)
        {
            logger.LogDebug("Generando PDF. QR: {HasQr}", qrBase64 != null ? "sí" : "no");

            var doc = new HtmlToPdfDocument
            {
                GlobalSettings = new GlobalSettings
                {
                    PaperSize = PaperKind.Letter,
                    Orientation = Orientation.Portrait,
                    Margins = new MarginSettings { Top = 25, Bottom = 25, Left = 25, Right = 25 }
                },
                Objects =
                {
                    new ObjectSettings
                    {
                        HtmlContent = await BuildHtml(htmlContent, qrBase64),
                        WebSettings = { DefaultEncoding = "utf-8" }
                    }
                }
            };

            return converter.Convert(doc);
        }

        private static Task<string> BuildHtml(string htmlContent, string? qrBase64 = null)
        {
            var sello = string.IsNullOrEmpty(qrBase64) ? "" :
                $"<div style=\"border-top:1px solid #cbd5e1;margin-top:24px;padding-top:12px;display:flex;align-items:center;gap:14px;page-break-inside:avoid;\">" +
                $"<img src=\"data:image/png;base64,{qrBase64}\" style=\"width:64px;height:64px;flex-shrink:0;\" />" +
                "<div>" +
                "<div style=\"font-weight:700;font-size:9pt;color:#1e293b;letter-spacing:0.3px;\">SELLO DE AUTENTICIDAD</div>" +
                "<div style=\"font-size:8pt;color:#64748b;margin-top:2px;\">Escanea el c&#243;digo QR para verificar este documento</div>" +
                "</div></div>";

            return Task.FromResult(@$"
<html>
<head>
  <meta charset='UTF-8'>
  <style>
    body {{ margin: 10px; font-size: 14px; line-height: 1.6; }}
    .ql-editor {{ white-space: normal; word-break: break-word; }}
    .ql-font-arial {{ font-family: Arial, sans-serif; }}
    .ql-font-calibri {{ font-family: Calibri, sans-serif; }}
    .ql-font-times-new-roman {{ font-family: ""Times New Roman"", serif; }}
    .ql-font-georgia {{ font-family: Georgia, serif; }}
    .ql-font-cambria {{ font-family: Cambria, serif; }}
    .ql-font-courier-new {{ font-family: ""Courier New"", monospace; }}
    .ql-font-consolas {{ font-family: Consolas, monospace; }}
    .ql-size-8 {{font-size: 8px;}} .ql-size-9 {{font-size: 9px;}} .ql-size-10 {{font-size: 10px;}}
    .ql-size-11 {{font-size: 11px;}} .ql-size-12 {{font-size: 12px;}} .ql-size-14 {{font-size: 14px;}}
    .ql-size-16 {{font-size: 16px;}} .ql-size-18 {{font-size: 18px;}} .ql-size-20 {{font-size: 20px;}}
    .ql-size-24 {{font-size: 24px;}} .ql-size-28 {{font-size: 28px;}} .ql-size-32 {{font-size: 32px;}}
    .ql-size-36 {{font-size: 36px;}} .ql-size-40 {{font-size: 40px;}} .ql-size-48 {{font-size: 48px;}}
    .ql-size-56 {{font-size: 56px;}} .ql-size-64 {{font-size: 64px;}} .ql-size-72 {{font-size: 72px;}}
    .ql-align-right {{ text-align: right; }}
    .ql-align-center {{ text-align: center; }}
    .ql-align-justify {{ text-align: justify; }}
    .ql-indent-1 {{ padding-left: 3em; }}
    .ql-indent-2 {{ padding-left: 6em; }}
    .ql-indent-3 {{ padding-left: 9em; }}
    blockquote {{ border-left: 4px solid #ccc; margin: 1em 0; padding-left: 1em; color: #666; }}
    pre {{ background-color: #f4f4f4; padding: 1em; overflow: auto; }}
    code {{ font-family: monospace; background-color: #eee; padding: 2px 4px; }}
    h1 {{ font-size: 2em; font-weight: bold; }} h2 {{ font-size: 1.5em; font-weight: bold; }}
    h3 {{ font-size: 1.17em; font-weight: bold; }}
    ol, ul {{ padding-left: 2em; }} li {{ margin-bottom: 5px; }}
  </style>
</head>
<body>
  <div class='ql-editor'>{htmlContent}</div>
  {sello}
</body>
</html>");
        }
    }
}
```

---

### Task 6: Actualizar CreateDocService

**Files:**
- Modify: `Services/Documents/CreateDocService.cs`

Cambios:
1. Inyectar `QrCodeService` y `IConfiguration`
2. `GenerarPdfInternoAsync` devuelve `NombrePlantilla` además del PDF y empleado, y acepta `qrBase64` opcional
3. `GenerarDocumentoPdfAsync` genera el UUID, construye la URL, genera el QR, y guarda `CodigoVerificacion` + `NombreDocumento` en Historial
4. Actualizar `GenerarDocumentoSinHashAsync` y `GenerarDocumentoConHashAsync` para compilar con la nueva firma

- [ ] **Step 1: Actualizar el constructor**

Reemplazar el bloque del constructor en `Services/Documents/CreateDocService.cs`:

```csharp
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
```

- [ ] **Step 2: Actualizar GenerarPdfInternoAsync**

Reemplazar el método `GenerarPdfInternoAsync` completo:

```csharp
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
    return (await htmlPdfService.HtmlToPdf(html, qrBase64), empleado, plantilla?.NombrePlantilla ?? "");
}
```

- [ ] **Step 3: Actualizar GenerarDocumentoPdfAsync**

Reemplazar el método `GenerarDocumentoPdfAsync` completo:

```csharp
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
```

- [ ] **Step 4: Actualizar GenerarDocumentoSinHashAsync para compilar**

El método ahora recibe 3-tupla en el destructuring. Reemplazar `GenerarDocumentoSinHashAsync`:

```csharp
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
```

- [ ] **Step 5: Actualizar GenerarDocumentoConHashAsync para compilar**

Este método usaba `GenerarPdfConHashAsync` que pasaba el hash al footer de texto — ya no aplica. Reemplazarlo para que delegue a `GenerarDocumentoPdfAsync`:

```csharp
public async Task<(byte[] Contenido, string NombreArchivo)> GenerarDocumentoConHashAsync(
    int plantillaId, int empresaId, int empleadoId, int creadoPorId)
    => await GenerarDocumentoPdfAsync(plantillaId, empresaId, empleadoId, creadoPorId);
```

- [ ] **Step 6: Eliminar GenerarPdfConHashAsync**

Borrar el método privado `GenerarPdfConHashAsync` completo (líneas 183–195 del original) ya que ya no se usa.

- [ ] **Step 7: Verificar que compila**

```bash
dotnet build
```

Salida esperada: `Build succeeded.` sin errores.

---

### Task 7: Agregar BuscarPorCodigoAsync a HistorialService

**Files:**
- Modify: `Services/Core/HistorialService.cs`

- [ ] **Step 1: Agregar el método**

Al final de la clase `HistorialService`, antes del cierre `}`, agregar:

```csharp
public async Task<Historial?> BuscarPorCodigoAsync(Guid codigo)
{
    using var context = contextFactory.CreateDbContext();
    return await context.Historial
        .FirstOrDefaultAsync(h => h.CodigoVerificacion == codigo);
}
```

---

### Task 8: Crear página pública Verificar.razor

**Files:**
- Create: `Components/Pages/Verificar.razor`

- [ ] **Step 1: Crear el archivo**

Crear `Components/Pages/Verificar.razor`:

```razor
@page "/verificar/{Codigo}"
@layout Layout.LoginLayout
@attribute [AllowAnonymous]
@inject HistorialService HistorialSvc
@inject EmpleadoService EmpleadoSvc
@inject EmpresaService EmpresaSvc

<PageTitle>Verificar documento — Uiglobal</PageTitle>

<div style="min-height:100vh;background:#f1f5f9;display:flex;align-items:center;justify-content:center;padding:32px 16px;">
    <div style="width:100%;max-width:480px;">

        <!-- Logo -->
        <div style="text-align:center;margin-bottom:28px;">
            <span style="font-size:26px;font-weight:700;color:#F97316;">Ui</span>
            <span style="font-size:26px;font-weight:500;color:#334155;">global</span>
            <div style="font-size:13px;color:#94a3b8;margin-top:4px;">Verificación de documentos</div>
        </div>

        @if (_loading)
        {
            <div style="background:#fff;border:1px solid #e2e8f0;border-radius:10px;padding:40px;text-align:center;">
                <MudProgressCircular Color="Color.Primary" Indeterminate="true" />
                <div style="margin-top:12px;font-size:13px;color:#64748b;">Verificando documento...</div>
            </div>
        }
        else if (_notFound)
        {
            <div style="background:#fff;border:1px solid #fecaca;border-radius:10px;padding:36px;text-align:center;">
                <div style="width:56px;height:56px;border-radius:50%;background:#fef2f2;display:flex;align-items:center;justify-content:center;margin:0 auto 12px;">
                    <span class="icon" style="font-size:28px;color:#dc2626;">close</span>
                </div>
                <div style="font-size:18px;font-weight:700;color:#dc2626;margin-bottom:8px;">No se pudo verificar</div>
                <div style="font-size:13px;color:#64748b;margin-bottom:20px;">Este código no corresponde a ningún documento en el sistema.</div>
                <div style="background:#fef2f2;border:1px solid #fecaca;border-radius:6px;padding:12px;text-align:left;font-size:12px;color:#991b1b;">
                    <strong>Posibles causas:</strong>
                    <ul style="margin:6px 0 0 16px;padding:0;line-height:1.9;">
                        <li>El documento fue alterado o es falso</li>
                        <li>El código QR está dañado o mal escaneado</li>
                        <li>El documento no fue generado por este sistema</li>
                    </ul>
                </div>
            </div>
        }
        else
        {
            <div style="background:#fff;border:1px solid #bbf7d0;border-radius:10px;padding:36px;">
                <div style="text-align:center;margin-bottom:20px;">
                    <div style="width:56px;height:56px;border-radius:50%;background:#dcfce7;display:flex;align-items:center;justify-content:center;margin:0 auto 12px;">
                        <span class="icon" style="font-size:28px;color:#16a34a;">check</span>
                    </div>
                    <div style="font-size:18px;font-weight:700;color:#16a34a;">Documento Auténtico</div>
                    <div style="font-size:13px;color:#64748b;margin-top:4px;">Este certificado fue emitido por el sistema</div>
                </div>

                <div style="background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;padding:16px;display:grid;grid-template-columns:1fr 1fr;gap:14px;">
                    <div>
                        <div style="font-size:10px;color:#94a3b8;text-transform:uppercase;letter-spacing:0.5px;margin-bottom:3px;">Empleado</div>
                        <div style="font-size:13.5px;font-weight:600;color:#1e293b;">@_nombreEmpleado</div>
                    </div>
                    <div>
                        <div style="font-size:10px;color:#94a3b8;text-transform:uppercase;letter-spacing:0.5px;margin-bottom:3px;">Empresa</div>
                        <div style="font-size:13.5px;font-weight:600;color:#1e293b;">@_nombreEmpresa</div>
                    </div>
                    <div>
                        <div style="font-size:10px;color:#94a3b8;text-transform:uppercase;letter-spacing:0.5px;margin-bottom:3px;">Tipo de documento</div>
                        <div style="font-size:13.5px;font-weight:600;color:#1e293b;">@_tipoDocumento</div>
                    </div>
                    <div>
                        <div style="font-size:10px;color:#94a3b8;text-transform:uppercase;letter-spacing:0.5px;margin-bottom:3px;">Fecha de emisión</div>
                        <div style="font-size:13.5px;font-weight:600;color:#1e293b;">@_fechaEmision</div>
                    </div>
                </div>

                <div style="margin-top:14px;padding:10px 14px;background:#f0fdf4;border:1px solid #bbf7d0;border-radius:6px;font-size:11px;color:#15803d;word-break:break-all;">
                    Código: <strong>@Codigo</strong>
                </div>
            </div>
        }
    </div>
</div>

@code {
    [Parameter] public string Codigo { get; set; } = "";

    private bool _loading = true;
    private bool _notFound = false;
    private string _nombreEmpleado = "";
    private string _nombreEmpresa = "";
    private string _tipoDocumento = "";
    private string _fechaEmision = "";

    protected override async Task OnInitializedAsync()
    {
        if (!Guid.TryParse(Codigo, out var guid))
        {
            _notFound = true;
            _loading = false;
            return;
        }

        var historial = await HistorialSvc.BuscarPorCodigoAsync(guid);
        if (historial == null)
        {
            _notFound = true;
            _loading = false;
            return;
        }

        var empleado = await EmpleadoSvc.ObtenerEmpleadoPorIdAsync(historial.CreadoParaId);
        var empresa = await EmpresaSvc.ObtenerEmpresaPorIdAsync(historial.EmpresaId);

        _nombreEmpleado = empleado?.NombreCompleto ?? "—";
        _nombreEmpresa = empresa?.Nombre ?? "—";
        _tipoDocumento = string.IsNullOrEmpty(historial.NombreDocumento) ? "Certificado Laboral" : historial.NombreDocumento;
        _fechaEmision = historial.FechaCreacion.ToLocalTime().ToString("dd 'de' MMMM 'de' yyyy",
            new System.Globalization.CultureInfo("es-CO"));

        _loading = false;
    }
}
```

- [ ] **Step 2: Verificar build final**

```bash
dotnet build
```

Salida esperada: `Build succeeded.` sin advertencias sobre tipos faltantes.

---

### Task 9: Prueba manual

- [ ] **Step 1: Arrancar la app**

```bash
dotnet run
```

- [ ] **Step 2: Generar un PDF**

Navegar a `/generar-doc`, seleccionar empresa, empleado y plantilla, y hacer click en **Descargar PDF**.

- [ ] **Step 3: Verificar el sello en el PDF**

Abrir el PDF descargado. Al final del documento debe aparecer el bloque "SELLO DE AUTENTICIDAD" con el código QR.

- [ ] **Step 4: Verificar la página pública**

Escanear el QR con el teléfono (o copiar la URL del QR y abrirla en el navegador). Debe mostrar la página verde con los datos del documento.

- [ ] **Step 5: Probar código inválido**

Navegar a `/verificar/00000000-0000-0000-0000-000000000000`. Debe mostrar el estado rojo "No se pudo verificar".

- [ ] **Step 6: Probar sin sello**

Generar un PDF con el checkbox "Generar sin sello" marcado. El PDF no debe tener el bloque QR.
