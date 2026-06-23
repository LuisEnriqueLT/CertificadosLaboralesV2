---
name: qr-verificacion
description: Sello de autenticidad con QR y página pública de verificación para certificados laborales
metadata:
  type: project
---

# Diseño: Verificación de documentos con QR

## Contexto

Los documentos generados por el sistema no tenían mecanismo de verificación de autenticidad. El sistema anterior embebía un hash SHA256 en el pie de página, pero no había forma de verificarlo sin acceso a la base de datos. Se reemplaza por un código QR que apunta a una página pública de verificación.

## Decisiones clave

- **Código único:** UUID (`Guid.NewGuid()`) por documento. Imposible de adivinar, sin colisiones, sin lógica extra.
- **Verificación:** La página muestra los datos originales del sistema. Si el PDF fue alterado, el receptor detecta la discrepancia comparando visualmente.
- **Sin verificación de integridad de bytes:** DinkToPdf puede producir bytes distintos en cada renderizado; el hash SHA256 existente se mantiene en BD pero no se expone en el QR flow por ahora.
- **Sin expiración:** Los links de verificación funcionan indefinidamente.
- **Acceso público:** La página `/verificar/{codigo}` no requiere autenticación.

## Arquitectura

### Flujo de generación

```
Usuario genera documento
  → CreateDocService crea Guid codigoVerificacion
  → QrCodeService.GenerarQr($"{baseUrl}/verificar/{codigo}") → base64 PNG
  → HtmlPdfService.HtmlToPdf(html, qrBase64) → PDF con QR en footer
  → Historial guardado con CodigoVerificacion = guid
  → PDF retornado al usuario
```

### Flujo de verificación

```
Receptor escanea QR → GET /verificar/{codigo}
  → HistorialService.BuscarPorCodigoAsync(Guid.Parse(codigo))
  → Si encontrado: muestra nombre empleado, empresa, tipo doc, fecha emisión + "Auténtico ✓"
  → Si no encontrado: muestra "No se pudo verificar ⚠️"
```

## Componentes

### Nuevo: `Services/Documents/QrCodeService.cs`
- Dependencia: paquete NuGet `QRCoder`
- Método: `string GenerarQr(string url)` → PNG en base64
- Registrado como `AddScoped` en `Program.cs`

### Modificado: `Models/Historial.cs`
- Nuevo campo: `public Guid CodigoVerificacion { get; set; } = Guid.Empty`
- Nuevo campo: `public string NombreDocumento { get; set; } = string.Empty` — nombre de la plantilla al momento de generación (ej. "Certificado Laboral"). Se almacena como string para que sea inmutable aunque la plantilla cambie o se elimine.
- Documentos existentes quedan con `Guid.Empty` y `NombreDocumento` vacío — sin impacto.

### Modificado: `Data/AppDbContext.cs`
- Migration EF Core: columnas `CodigoVerificacion` (uniqueidentifier) y `NombreDocumento` (nvarchar) en tabla `Historial`

### Modificado: `Services/Documents/HtmlPdfService.cs`
- Parámetro `hash` reemplazado por `qrBase64`
- Footer muestra imagen QR + texto "Escanea para verificar este documento"
- Si `qrBase64` es null/vacío, footer sin sello (compatibilidad con flujos sin QR)

### Modificado: `Services/Documents/CreateDocService.cs`
- En métodos de generación: crear UUID, construir URL, llamar `QrCodeService`, pasar resultado a `HtmlPdfService`
- Guardar `CodigoVerificacion` en `Historial`
- `IConfiguration` inyectado para leer `BaseUrl` desde `appsettings.json`

### Modificado: `Services/Core/HistorialService.cs`
- Nuevo método: `Task<Historial?> BuscarPorCodigoAsync(Guid codigo)`

### Nuevo: `Components/Pages/Verificar.razor`
- Ruta: `/verificar/{Codigo}`
- `@attribute [AllowAnonymous]`
- `@attribute [RenderModeInteractiveServer]` — o renderizado estático (no necesita interactividad)
- Muestra estado auténtico o no encontrado según resultado de `HistorialService`
- Campos visibles: nombre empleado, empresa, tipo de documento, fecha de emisión

## Configuración

`appsettings.json` recibe una clave `BaseUrl` con la URL pública de la app (ej. `https://certificados.tuempresa.com`). `CreateDocService` la usa para construir el link del QR.

## Datos mostrados en verificación

| Campo | Fuente |
|---|---|
| Nombre empleado | `Historial` → `Empleado.NombreCompleto` |
| Empresa | `Historial` → `Empresa.Nombre` |
| Tipo de documento | `Historial.NombreDocumento` (grabado al generar, ej. "Certificado Laboral") |
| Fecha de emisión | `Historial.FechaCreacion` |

## Lo que este diseño NO hace

- No verifica integridad de bytes del PDF (posible mejora futura si se confirma que DinkToPdf es determinista)
- No expira los links de verificación
- No requiere login para verificar
