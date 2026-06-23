# Watermark Position & Size Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add configurable position (X/Y), size, and rotation to text and image watermarks, stored in the database and exposed via sliders in the template editor UI.

**Architecture:** Four new `double`/`int` columns are added to `PlantillaHtml` via an EF Core migration. `HtmlPdfService`'s three watermark builders (`BuildWatermarkCss`, `BuildWatermarkHtml`, `WrapWithWatermarkHtml`) receive the new params. `CreateDocService` passes them through from the entity. `Plantillas.razor` exposes four sliders (tamaño, rotación, X, Y) that appear when the watermark type is not Ninguna.

**Tech Stack:** .NET 10, Blazor Server, Entity Framework Core 10 (SQL Server), wkhtmltopdf via DinkToPdf.

---

## File Map

| File | Action |
|------|--------|
| `Models/PlantillaHtml.cs` | Add 4 new properties |
| `Migrations/20260616000002_WatermarkPositionSize.cs` | New migration (Up/Down) |
| `Migrations/AppDbContextModelSnapshot.cs` | Auto-updated by EF tooling |
| `Services/Documents/HtmlPdfService.cs` | Update 5 methods to accept/use new params |
| `Services/Documents/CreateDocService.cs` | Update 2 call sites to pass new params |
| `Components/Pages/Plantillas.razor` | Add 4 state vars + UI sliders + wire save/load/preview |

---

## Task 1: Add Properties to Model

**Files:**
- Modify: `Models/PlantillaHtml.cs`

- [ ] **Step 1: Add the four new properties**

Open `Models/PlantillaHtml.cs`. The file currently ends at line 31. Replace the class body so it reads:

```csharp
public class PlantillaHtml
{
    [Key]
    public int Id { get; set; }

    [ForeignKey(nameof(Empresa))]
    public int EmpresaId { get; set; }

    [Required]
    [MaxLength(200)]
    public string NombrePlantilla { get; set; } = string.Empty;

    [Required]
    public string HtmlContenido { get; set; } = string.Empty;

    public Empresa Empresa { get; set; } = null!;

    public string? MarcaDeAgua { get; set; }

    public TipoMarcaAgua TipoMarcaAgua { get; set; } = TipoMarcaAgua.Ninguna;

    public double OpacidadMarcaAgua { get; set; } = 0.08;

    public double TamanoMarcaAgua { get; set; } = 80;

    public int RotacionMarcaAgua { get; set; } = -35;

    public double PosicionXMarcaAgua { get; set; } = 50;

    public double PosicionYMarcaAgua { get; set; } = 50;
}

public enum TipoMarcaAgua { Ninguna, Texto, Imagen }
```

- [ ] **Step 2: Build the project to confirm no compile errors**

```
dotnet build CertificadosLaboralesV2.csproj
```

Expected: `Build succeeded. 0 Error(s)`

---

## Task 2: Create EF Core Migration

**Files:**
- Create: `Migrations/20260616000002_WatermarkPositionSize.cs`
- Auto-updated: `Migrations/AppDbContextModelSnapshot.cs`

- [ ] **Step 1: Add the migration via EF tooling**

```
dotnet ef migrations add WatermarkPositionSize --project CertificadosLaboralesV2.csproj
```

Expected output: `Done. To undo this action, use 'ef migrations remove'`

This creates `Migrations/20260616000002_WatermarkPositionSize.cs` automatically.

- [ ] **Step 2: Verify the generated migration adds 4 columns**

Read the generated file and confirm the `Up` method contains `AddColumn` calls for:
- `PosicionXMarcaAgua` (double, default 50)
- `PosicionYMarcaAgua` (double, default 50)
- `RotacionMarcaAgua` (int, default -35)
- `TamanoMarcaAgua` (double, default 80)

If EF did not generate default values, edit the migration manually to add `defaultValue:` to each `AddColumn` call, for example:

```csharp
migrationBuilder.AddColumn<double>(
    name: "TamanoMarcaAgua",
    table: "PlantillaHtml",
    type: "float",
    nullable: false,
    defaultValue: 80.0);

migrationBuilder.AddColumn<int>(
    name: "RotacionMarcaAgua",
    table: "PlantillaHtml",
    type: "int",
    nullable: false,
    defaultValue: -35);

migrationBuilder.AddColumn<double>(
    name: "PosicionXMarcaAgua",
    table: "PlantillaHtml",
    type: "float",
    nullable: false,
    defaultValue: 50.0);

migrationBuilder.AddColumn<double>(
    name: "PosicionYMarcaAgua",
    table: "PlantillaHtml",
    type: "float",
    nullable: false,
    defaultValue: 50.0);
```

And the `Down` method should drop all four columns:

```csharp
migrationBuilder.DropColumn(name: "TamanoMarcaAgua", table: "PlantillaHtml");
migrationBuilder.DropColumn(name: "RotacionMarcaAgua", table: "PlantillaHtml");
migrationBuilder.DropColumn(name: "PosicionXMarcaAgua", table: "PlantillaHtml");
migrationBuilder.DropColumn(name: "PosicionYMarcaAgua", table: "PlantillaHtml");
```

- [ ] **Step 3: Apply the migration**

```
dotnet ef database update --project CertificadosLaboralesV2.csproj
```

Expected: `Done.` (with migration name applied)

---

## Task 3: Update HtmlPdfService

**Files:**
- Modify: `Services/Documents/HtmlPdfService.cs`

There are 5 methods to update. Make all changes before building.

- [ ] **Step 1: Update `HtmlToPdf` signature (line ~10)**

Current signature:
```csharp
public async Task<byte[]> HtmlToPdf(
    string htmlContent,
    string? qrBase64 = null,
    string? marcaDeAgua = null,
    TipoMarcaAgua tipoMarca = TipoMarcaAgua.Ninguna,
    double opacidad = 0.08)
```

New signature (add 4 params at the end):
```csharp
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
```

- [ ] **Step 2: Update the `BuildHtml` call inside `HtmlToPdf` (~line 39)**

Current:
```csharp
HtmlContent = BuildHtml(htmlContent, fontCss.ToString(), marcaDeAgua, tipoMarca, opacidad),
```

New:
```csharp
HtmlContent = BuildHtml(htmlContent, fontCss.ToString(), marcaDeAgua, tipoMarca, opacidad, posX, posY, tamano, rotacion),
```

- [ ] **Step 3: Update `BuildWatermarkHtml` signature and body (~line 82)**

Current signature:
```csharp
public static string BuildWatermarkHtml(
    TipoMarcaAgua tipoMarca,
    string? marcaDeAgua,
    double opacidad)
```

New signature:
```csharp
public static string BuildWatermarkHtml(
    TipoMarcaAgua tipoMarca,
    string? marcaDeAgua,
    double opacidad,
    double posX = 50,
    double posY = 50,
    double tamano = 80,
    int rotacion = -35)
```

Current body (text branch):
```csharp
TipoMarcaAgua.Texto when !string.IsNullOrEmpty(marcaDeAgua) =>
    $"<div style=\"position:fixed;top:50%;left:50%;" +
    $"-webkit-transform:translate(-50%,-50%) rotate(-35deg);transform:translate(-50%,-50%) rotate(-35deg);" +
    $"font-size:80px;font-weight:700;color:rgba(100,116,139,{op});" +
    $"white-space:nowrap;z-index:9999;pointer-events:none;user-select:none;\">" +
    $"{System.Net.WebUtility.HtmlEncode(marcaDeAgua)}</div>",
```

New body (text branch):
```csharp
TipoMarcaAgua.Texto when !string.IsNullOrEmpty(marcaDeAgua) =>
    $"<div style=\"position:fixed;top:{posY.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}%;left:{posX.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}%;" +
    $"-webkit-transform:translate(-50%,-50%) rotate({rotacion}deg);transform:translate(-50%,-50%) rotate({rotacion}deg);" +
    $"font-size:{tamano.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)}px;font-weight:700;color:rgba(100,116,139,{op});" +
    $"white-space:nowrap;z-index:9999;pointer-events:none;user-select:none;\">" +
    $"{System.Net.WebUtility.HtmlEncode(marcaDeAgua)}</div>",
```

Current body (image branch):
```csharp
TipoMarcaAgua.Imagen when !string.IsNullOrEmpty(marcaDeAgua) =>
    $"<div style=\"position:fixed;top:0;left:0;width:100%;height:100%;" +
    $"display:flex;align-items:center;justify-content:center;" +
    $"opacity:{op};z-index:9999;pointer-events:none;\">" +
    $"<img src=\"data:{GetImageMimeType(marcaDeAgua)};base64,{marcaDeAgua}\" style=\"max-width:60%;max-height:60%;object-fit:contain;\"/></div>",
```

New body (image branch — uses absolute positioned inner img for precise placement):
```csharp
TipoMarcaAgua.Imagen when !string.IsNullOrEmpty(marcaDeAgua) =>
    $"<div style=\"position:fixed;top:0;left:0;width:100%;height:100%;" +
    $"opacity:{op};z-index:9999;pointer-events:none;\">" +
    $"<img src=\"data:{GetImageMimeType(marcaDeAgua)};base64,{marcaDeAgua}\" " +
    $"style=\"position:absolute;top:{posY.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}%;left:{posX.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}%;" +
    $"transform:translate(-50%,-50%) rotate({rotacion}deg);-webkit-transform:translate(-50%,-50%) rotate({rotacion}deg);" +
    $"max-width:{tamano.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)}%;max-height:{tamano.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)}%;object-fit:contain;\"/></div>",
```

- [ ] **Step 4: Update `WrapWithWatermarkHtml` signature and body (~line 109)**

Current signature:
```csharp
public static string WrapWithWatermarkHtml(
    TipoMarcaAgua tipoMarca,
    string? marcaDeAgua,
    double opacidad,
    string htmlContent)
```

New signature:
```csharp
public static string WrapWithWatermarkHtml(
    TipoMarcaAgua tipoMarca,
    string? marcaDeAgua,
    double opacidad,
    string htmlContent,
    double posX = 50,
    double posY = 50,
    double tamano = 80,
    int rotacion = -35)
```

Current watermarkDiv text branch:
```csharp
TipoMarcaAgua.Texto =>
    $"<div style=\"position:absolute;top:50%;left:50%;transform:translate(-50%,-50%) rotate(-35deg);" +
    $"font-size:80px;font-weight:700;color:rgba(100,116,139,{op});" +
    $"white-space:nowrap;z-index:9999;pointer-events:none;user-select:none;\">" +
    $"{System.Net.WebUtility.HtmlEncode(marcaDeAgua)}</div>",
```

New text branch:
```csharp
TipoMarcaAgua.Texto =>
    $"<div style=\"position:absolute;top:{posY.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}%;left:{posX.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}%;" +
    $"transform:translate(-50%,-50%) rotate({rotacion}deg);" +
    $"font-size:{tamano.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)}px;font-weight:700;color:rgba(100,116,139,{op});" +
    $"white-space:nowrap;z-index:9999;pointer-events:none;user-select:none;\">" +
    $"{System.Net.WebUtility.HtmlEncode(marcaDeAgua)}</div>",
```

Current image branch:
```csharp
TipoMarcaAgua.Imagen =>
    $"<div style=\"position:absolute;top:0;left:0;width:100%;height:100%;" +
    $"display:flex;align-items:center;justify-content:center;" +
    $"opacity:{op};z-index:9999;pointer-events:none;\">" +
    $"<img src=\"data:{GetImageMimeType(marcaDeAgua)};base64,{marcaDeAgua}\" style=\"max-width:60%;max-height:60%;object-fit:contain;\"/></div>",
```

New image branch:
```csharp
TipoMarcaAgua.Imagen =>
    $"<div style=\"position:absolute;top:0;left:0;width:100%;height:100%;" +
    $"opacity:{op};z-index:9999;pointer-events:none;\">" +
    $"<img src=\"data:{GetImageMimeType(marcaDeAgua)};base64,{marcaDeAgua}\" " +
    $"style=\"position:absolute;top:{posY.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}%;left:{posX.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}%;" +
    $"transform:translate(-50%,-50%) rotate({rotacion}deg);" +
    $"max-width:{tamano.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)}%;max-height:{tamano.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)}%;object-fit:contain;\"/></div>",
```

- [ ] **Step 5: Update private `BuildHtml` signature (~line 146)**

Current:
```csharp
private static string BuildHtml(
    string htmlContent,
    string customFontCss = "",
    string? marcaDeAgua = null,
    TipoMarcaAgua tipoMarca = TipoMarcaAgua.Ninguna,
    double opacidad = 0.08)
```

New:
```csharp
private static string BuildHtml(
    string htmlContent,
    string customFontCss = "",
    string? marcaDeAgua = null,
    TipoMarcaAgua tipoMarca = TipoMarcaAgua.Ninguna,
    double opacidad = 0.08,
    double posX = 50,
    double posY = 50,
    double tamano = 80,
    int rotacion = -35)
```

- [ ] **Step 6: Update the `BuildWatermarkCss` call inside `BuildHtml` (~line 153)**

Current:
```csharp
var watermarkCss = BuildWatermarkCss(tipoMarca, marcaDeAgua, opacidad);
```

New:
```csharp
var watermarkCss = BuildWatermarkCss(tipoMarca, marcaDeAgua, opacidad, posX, posY, tamano, rotacion);
```

- [ ] **Step 7: Update private `BuildWatermarkCss` signature and body (~line 179)**

Current signature:
```csharp
private static string BuildWatermarkCss(TipoMarcaAgua tipoMarca, string? marcaDeAgua, double opacidad)
```

New signature:
```csharp
private static string BuildWatermarkCss(
    TipoMarcaAgua tipoMarca,
    string? marcaDeAgua,
    double opacidad,
    double posX = 50,
    double posY = 50,
    double tamano = 80,
    int rotacion = -35)
```

Current text CSS block:
```csharp
return $@"body::before {{
    content: '{escaped}';
    position: fixed;
    top: 50%;
    left: 50%;
    -webkit-transform: translate(-50%, -50%) rotate(-35deg);
    transform: translate(-50%, -50%) rotate(-35deg);
    font-size: 80px;
    font-weight: 700;
    color: rgba(100, 116, 139, {op});
    white-space: nowrap;
    z-index: 9999;
    pointer-events: none;
    user-select: none;
}}";
```

New text CSS block (use InvariantCulture for decimal separator):
```csharp
var pxStr = posX.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
var pyStr = posY.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
var szStr = tamano.ToString("F0", System.Globalization.CultureInfo.InvariantCulture);
return $@"body::before {{
    content: '{escaped}';
    position: fixed;
    top: {pyStr}%;
    left: {pxStr}%;
    -webkit-transform: translate(-50%, -50%) rotate({rotacion}deg);
    transform: translate(-50%, -50%) rotate({rotacion}deg);
    font-size: {szStr}px;
    font-weight: 700;
    color: rgba(100, 116, 139, {op});
    white-space: nowrap;
    z-index: 9999;
    pointer-events: none;
    user-select: none;
}}";
```

Current image CSS block:
```csharp
return $@"body::before {{
    content: '';
    position: fixed;
    top: 0;
    left: 0;
    width: 100%;
    height: 100%;
    background-image: url('data:{mime};base64,{marcaDeAgua}');
    background-repeat: no-repeat;
    background-position: center;
    background-size: 60% auto;
    opacity: {op};
    z-index: 9999;
    pointer-events: none;
}}";
```

New image CSS block:
```csharp
var pxStr = posX.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
var pyStr = posY.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
var szStr = tamano.ToString("F0", System.Globalization.CultureInfo.InvariantCulture);
return $@"body::before {{
    content: '';
    position: fixed;
    top: 0;
    left: 0;
    width: 100%;
    height: 100%;
    background-image: url('data:{mime};base64,{marcaDeAgua}');
    background-repeat: no-repeat;
    background-position: {pxStr}% {pyStr}%;
    background-size: {szStr}% auto;
    -webkit-transform: rotate({rotacion}deg);
    transform: rotate({rotacion}deg);
    opacity: {op};
    z-index: 9999;
    pointer-events: none;
}}";
```

- [ ] **Step 8: Build to confirm no compile errors**

```
dotnet build CertificadosLaboralesV2.csproj
```

Expected: `Build succeeded. 0 Error(s)`

---

## Task 4: Update CreateDocService

**Files:**
- Modify: `Services/Documents/CreateDocService.cs`

Two call sites need the new params.

- [ ] **Step 1: Update `GenerarPdfInternoAsync` (~line 175)**

Current:
```csharp
return (await htmlPdfService.HtmlToPdf(
    html,
    qrBase64,
    plantilla!.MarcaDeAgua,
    plantilla.TipoMarcaAgua,
    plantilla.OpacidadMarcaAgua), empleado, plantilla?.NombrePlantilla ?? "");
```

New:
```csharp
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
```

- [ ] **Step 2: Update `ObtenerHtmlPreviewAsync` (~line 115)**

Current:
```csharp
return HtmlPdfService.WrapWithWatermarkHtml(
    plantilla.TipoMarcaAgua, plantilla.MarcaDeAgua, plantilla.OpacidadMarcaAgua, html);
```

New:
```csharp
return HtmlPdfService.WrapWithWatermarkHtml(
    plantilla.TipoMarcaAgua,
    plantilla.MarcaDeAgua,
    plantilla.OpacidadMarcaAgua,
    html,
    plantilla.PosicionXMarcaAgua,
    plantilla.PosicionYMarcaAgua,
    plantilla.TamanoMarcaAgua,
    plantilla.RotacionMarcaAgua);
```

- [ ] **Step 3: Build to confirm no compile errors**

```
dotnet build CertificadosLaboralesV2.csproj
```

Expected: `Build succeeded. 0 Error(s)`

---

## Task 5: Update Plantillas.razor

**Files:**
- Modify: `Components/Pages/Plantillas.razor`

- [ ] **Step 1: Add 4 state variables to the `@code` block (~line 299)**

After the existing `private double _opacidadMarca = 0.15;` line, add:

```csharp
private double _tamanoMarca = 80;
private int _rotacionMarca = -35;
private double _posXMarca = 50;
private double _posYMarca = 50;
```

- [ ] **Step 2: Add sliders to the watermark section in the markup (~line 122)**

The current watermark section ends with the opacity slider block:
```razor
@if (_tipoMarca != TipoMarcaAgua.Ninguna)
{
    <div style="display:flex;align-items:center;gap:10px;">
        <label style="font-size:12px;color:#64748b;white-space:nowrap;">Opacidad: @((_opacidadMarca * 100).ToString("F0"))%</label>
        <input type="range" @bind="_opacidadMarca" @bind:event="oninput"
               min="0.01" max="0.5" step="0.01"
               style="flex:1;accent-color:#F97316;" />
    </div>
}
```

Replace that entire block with:
```razor
@if (_tipoMarca != TipoMarcaAgua.Ninguna)
{
    <div style="display:flex;flex-direction:column;gap:8px;">
        <div style="display:flex;align-items:center;gap:10px;">
            <label style="font-size:12px;color:#64748b;white-space:nowrap;min-width:120px;">Opacidad: @((_opacidadMarca * 100).ToString("F0"))%</label>
            <input type="range" @bind="_opacidadMarca" @bind:event="oninput"
                   min="0.01" max="0.5" step="0.01"
                   style="flex:1;accent-color:#F97316;" />
        </div>
        <div style="display:flex;align-items:center;gap:10px;">
            <label style="font-size:12px;color:#64748b;white-space:nowrap;min-width:120px;">
                @(_tipoMarca == TipoMarcaAgua.Texto ? $"Tamaño: {_tamanoMarca:F0}px" : $"Tamaño: {_tamanoMarca:F0}%")
            </label>
            <input type="range" @bind="_tamanoMarca" @bind:event="oninput"
                   min="@(_tipoMarca == TipoMarcaAgua.Texto ? "20" : "10")"
                   max="@(_tipoMarca == TipoMarcaAgua.Texto ? "200" : "100")"
                   step="1"
                   style="flex:1;accent-color:#F97316;" />
        </div>
        <div style="display:flex;align-items:center;gap:10px;">
            <label style="font-size:12px;color:#64748b;white-space:nowrap;min-width:120px;">Rotación: @_rotacionMarca°</label>
            <input type="range" @bind="_rotacionMarca" @bind:event="oninput"
                   min="-180" max="180" step="1"
                   style="flex:1;accent-color:#F97316;" />
        </div>
        <div style="display:flex;align-items:center;gap:10px;">
            <label style="font-size:12px;color:#64748b;white-space:nowrap;min-width:120px;">Posición X: @_posXMarca.ToString("F0")%</label>
            <input type="range" @bind="_posXMarca" @bind:event="oninput"
                   min="0" max="100" step="1"
                   style="flex:1;accent-color:#F97316;" />
        </div>
        <div style="display:flex;align-items:center;gap:10px;">
            <label style="font-size:12px;color:#64748b;white-space:nowrap;min-width:120px;">Posición Y: @_posYMarca.ToString("F0")%</label>
            <input type="range" @bind="_posYMarca" @bind:event="oninput"
                   min="0" max="100" step="1"
                   style="flex:1;accent-color:#F97316;" />
        </div>
    </div>
}
```

- [ ] **Step 3: Update `HandleGuardar` to save the new fields (~line 461)**

In the edit branch, after `plantilla.OpacidadMarcaAgua = _opacidadMarca;` add:
```csharp
plantilla.TamanoMarcaAgua = _tamanoMarca;
plantilla.RotacionMarcaAgua = _rotacionMarca;
plantilla.PosicionXMarcaAgua = _posXMarca;
plantilla.PosicionYMarcaAgua = _posYMarca;
```

In the new branch (the `AgregarPlantillaAsync` object initializer), after `OpacidadMarcaAgua = _opacidadMarca` add:
```csharp
TamanoMarcaAgua = _tamanoMarca,
RotacionMarcaAgua = _rotacionMarca,
PosicionXMarcaAgua = _posXMarca,
PosicionYMarcaAgua = _posYMarca,
```

- [ ] **Step 4: Update `HandlePrevisualizar` to pass new params (~line 523)**

Current:
```csharp
_previewContent = HtmlPdfService.WrapWithWatermarkHtml(_tipoMarca, marcaValor, _opacidadMarca, html);
```

New:
```csharp
_previewContent = HtmlPdfService.WrapWithWatermarkHtml(
    _tipoMarca, marcaValor, _opacidadMarca, html,
    _posXMarca, _posYMarca, _tamanoMarca, _rotacionMarca);
```

- [ ] **Step 5: Update `EditarPlantilla` to load the new fields (~line 542)**

After the existing `_opacidadMarca = p.OpacidadMarcaAgua;` line, add:
```csharp
_tamanoMarca = p.TamanoMarcaAgua;
_rotacionMarca = p.RotacionMarcaAgua;
_posXMarca = p.PosicionXMarcaAgua;
_posYMarca = p.PosicionYMarcaAgua;
```

- [ ] **Step 6: Update `NuevaPlantilla` to reset the new fields (~line 566)**

After the existing `_opacidadMarca = 0.15;` line, add:
```csharp
_tamanoMarca = 80;
_rotacionMarca = -35;
_posXMarca = 50;
_posYMarca = 50;
```

- [ ] **Step 7: Build and verify**

```
dotnet build CertificadosLaboralesV2.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 8: Run the app and verify**

```
dotnet run --project CertificadosLaboralesV2.csproj
```

1. Navigate to `/plantillas`
2. Select watermark type `Texto`, enter text
3. Verify 5 sliders appear: Opacidad, Tamaño, Rotación, Posición X, Posición Y
4. Move the sliders and click **Previsualizar** — confirm the preview reflects the changes
5. Save the template, then re-edit it — confirm the slider values are restored
6. Select watermark type `Imagen`, upload an image
7. Verify the same 5 sliders appear with Tamaño showing `%` instead of `px`
8. Move sliders and preview — confirm image position/size/rotation changes

---

## Self-Review

**Spec coverage:**
- ✅ Add configurable size → `TamanoMarcaAgua` + slider
- ✅ Add configurable X/Y position → `PosicionXMarcaAgua` / `PosicionYMarcaAgua` + sliders
- ✅ Add configurable rotation → `RotacionMarcaAgua` + slider
- ✅ Both text and image types supported
- ✅ PDF (wkhtmltopdf CSS) and browser preview both updated
- ✅ Save + load round-trip covered in UI
- ✅ Migration with defaults so existing rows are unaffected

**Placeholder scan:** No TBDs or incomplete steps found.

**Type consistency:**
- `TamanoMarcaAgua` is `double` in model → `double tamano` in all service methods ✅
- `RotacionMarcaAgua` is `int` in model → `int rotacion` in all service methods ✅
- `PosicionXMarcaAgua` / `PosicionYMarcaAgua` are `double` in model → `double posX`/`posY` in service ✅
- `WrapWithWatermarkHtml` new signature param order: `(tipoMarca, marcaDeAgua, opacidad, htmlContent, posX, posY, tamano, rotacion)` — matches all call sites ✅
