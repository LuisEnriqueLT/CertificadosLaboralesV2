# Placeholders para campos fijos del empleado Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Permitir crear, desde Administración → Placeholder, un placeholder personalizado para cualquier campo fijo del empleado (Cargo, Tipo de contrato, Ciudad de trabajo, Tipo ID, Estado laboral, Fecha de ingreso, Tipo de salario, y también Nombre/Email/Cédula), con el mismo flujo que ya existe para los campos dinámicos del catálogo `DatoVariable`.

**Architecture:** `Placeholder.DatoVariableId` se vuelve opcional y se agrega `Placeholder.CampoFijo` (mutuamente excluyentes) — requiere una migración EF Core. Un nuevo catálogo estático `CamposFijosEmpleado` centraliza la lista de campos fijos elegibles y cómo leer su valor desde un `Empleado`. La UI de Administración ofrece un único `<select>` con dos grupos (fijos/dinámicos). La resolución en `CreateDocService` se extiende para manejar ambos tipos de vínculo, y deja de depender de que el empleado tenga `DatosVariables` no vacío.

**Tech Stack:** Blazor Server, EF Core (SQL Server, migraciones), C#.

## Global Constraints

- No existe proyecto de pruebas automatizadas en este repo; verificación = `dotnet build` con 0 errores + verificación manual donde se indique.
- No hay git; no hay commits — cada tarea termina con compilación + verificación manual.
- `Placeholder.DatoVariableId` y `Placeholder.CampoFijo` son mutuamente excluyentes: exactamente uno debe tener valor en cualquier registro válido.
- Se incluyen `nombrecompleto`, `email` y `cedula` en el catálogo de campos fijos aunque ya tengan placeholder hardcodeado (`{{NombreEmpleado}}`, `{{CorreoEmpleado}}`, `{{CedulaEmpleado}}`) — sin casos especiales que los excluyan.
- `GenerarDiccionarioPreviewAsync` (`CreateDocService.cs:278-308`) no se modifica — ya itera genéricamente sobre todos los `Placeholder`.

---

### Task 1: Modelo de datos y migración EF Core

**Files:**
- Modify: `Models/Placeholder.cs`
- Create: migración EF Core (generada por `dotnet ef migrations add`, no se escribe a mano)

**Interfaces:**
- Produces: `Placeholder.DatoVariableId` como `int?`, `Placeholder.DatoVariable` como `DatoVariable?`, y el nuevo campo `Placeholder.CampoFijo` (`string?`). Todas las tareas siguientes consumen estos tipos exactos.

- [ ] **Step 1: Actualizar el modelo `Placeholder`**

Reemplazar el contenido de `Models/Placeholder.cs`:

```csharp
namespace CertificadosLaboralesV2.Models
{
    public class Placeholder
    {
        public int Id { get; set; }
        public string Texto { get; set; } = string.Empty;
        public string PlaceholderTexto { get; set; } = string.Empty;
        public int? DatoVariableId { get; set; }
        public DatoVariable? DatoVariable { get; set; }
        public string? CampoFijo { get; set; }
    }
}
```

- [ ] **Step 2: Generar la migración**

Run: `dotnet ef migrations add MakePlaceholderCampoFijoOpcional`
Expected: se crean dos archivos nuevos en `Migrations/` (ej.
`<timestamp>_MakePlaceholderCampoFijoOpcional.cs` y su `.Designer.cs`), y
`Migrations/AppDbContextModelSnapshot.cs` se actualiza. El contenido de la
migración debe alterar la columna `DatoVariableId` de la tabla
`Placeholders` para aceptar NULL y agregar la columna `CampoFijo`
(`nvarchar`, nullable). Si el comando falla por no encontrar el proyecto de
inicio, ejecutarlo con `dotnet ef migrations add MakePlaceholderCampoFijoOpcional --project .` desde la raíz del repo.

- [ ] **Step 3: Aplicar la migración**

Run: `dotnet ef database update`
Expected: el comando termina sin errores, aplicando la migración a la base
de datos configurada en `ConnectionStrings:DefaultConnection` (user
secrets).

- [ ] **Step 4: Compilar y verificar**

Run: `dotnet build`
Expected: `Compilación correcta. 0 Errores`

- [ ] **Step 5: Verificación manual**

Conectarse a la base de datos (o usar cualquier herramienta disponible) y
confirmar que la tabla `Placeholders` ahora tiene la columna `DatoVariableId`
como NULLable y existe la nueva columna `CampoFijo`.

---

### Task 2: Catálogo de campos fijos elegibles

**Files:**
- Create: `Services/Documents/CamposFijosEmpleado.cs`

**Interfaces:**
- Consumes: `Models/Empleado.cs` (propiedades `NombreCompleto`, `Email`, `Cedula`, `Cargo`, `SalarioMensual`, `tipoSalario`, `TipoContrato`, `CiudadDeTrabajo`, `TipoId`, `FechaIngreso`, `EstadoLaboral` — todas ya existentes, sin cambios).
- Produces: `CamposFijosEmpleado.Campos` (`Dictionary<string, string>`, clave → etiqueta) y `CamposFijosEmpleado.ObtenerValor(Empleado? empleado, string clave)` (`string?`). Task 3 y Task 4 consumen ambos exactamente con este nombre y firma.

- [ ] **Step 1: Crear el archivo**

```csharp
using CertificadosLaboralesV2.Models;

namespace CertificadosLaboralesV2.Services.Documents
{
    public static class CamposFijosEmpleado
    {
        public static readonly Dictionary<string, string> Campos = new()
        {
            { "nombrecompleto", "Nombre completo" },
            { "email", "Correo electrónico" },
            { "cedula", "Cédula" },
            { "cargo", "Cargo" },
            { "salariomensual", "Salario mensual" },
            { "tiposalario", "Tipo de salario" },
            { "tipocontrato", "Tipo de contrato" },
            { "ciudaddetrabajo", "Ciudad de trabajo" },
            { "tipoid", "Tipo de identificación" },
            { "fechaingreso", "Fecha de ingreso" },
            { "estadolaboral", "Estado laboral" }
        };

        public static string? ObtenerValor(Empleado? empleado, string clave) => clave switch
        {
            "nombrecompleto" => empleado?.NombreCompleto,
            "email" => empleado?.Email,
            "cedula" => empleado?.Cedula,
            "cargo" => empleado?.Cargo,
            "salariomensual" => empleado?.SalarioMensual,
            "tiposalario" => empleado?.tipoSalario,
            "tipocontrato" => empleado?.TipoContrato,
            "ciudaddetrabajo" => empleado?.CiudadDeTrabajo,
            "tipoid" => empleado?.TipoId,
            "fechaingreso" => empleado?.FechaIngreso.ToString("yyyy-MM-dd"),
            "estadolaboral" => empleado == null ? null : (empleado.EstadoLaboral ? "Activo" : "Inactivo"),
            _ => null
        };
    }
}
```

- [ ] **Step 2: Compilar y verificar**

Run: `dotnet build`
Expected: `Compilación correcta. 0 Errores`

---

### Task 3: UI en Administración → Placeholder

**Files:**
- Modify: `Components/Pages/Administracion.razor` (líneas ~793-849: `_placeholderForm`, `OpenAddPlaceholder`, `OpenEditPlaceholder`, `SetPlaceholderModalContent`, `SavePlaceholder`)
- Modify: `Components/Pages/Administracion.razor` (caso `"Placeholder"` de `AllRows`, columna "Campo")

**Interfaces:**
- Consumes: `CamposFijosEmpleado.Campos` y `CamposFijosEmpleado.ObtenerValor` (Task 2), `Placeholder.DatoVariableId`/`DatoVariable`/`CampoFijo` (Task 1).
- No produce interfaces para otras tareas.

- [ ] **Step 1: Agregar el campo de estado para la selección combinada**

Junto a `_placeholderForm`/`_datosVariables` (línea ~793-794), agregar:

```csharp
    private Placeholder _placeholderForm = new();
    private List<DatoVariable> _datosVariables = [];
    private string _placeholderFieldSelection = "";
```

- [ ] **Step 2: Inicializar la selección en `OpenAddPlaceholder` y `OpenEditPlaceholder`**

Reemplazar:

```csharp
    private async Task OpenAddPlaceholder()
    {
        _placeholderForm = new();
        _datosVariables = await EmpleadoSvc.ObtenerTodosDatosVariablesAsync();
        _modalTitle = "Agregar placeholder";
        _modalSaveAction = SavePlaceholder;
        SetPlaceholderModalContent();
        _showModal = true;
    }

    private async Task OpenEditPlaceholder(Placeholder p)
    {
        _placeholderForm = new() { Id = p.Id, PlaceholderTexto = p.PlaceholderTexto, Texto = p.Texto, DatoVariableId = p.DatoVariableId };
        _datosVariables = await EmpleadoSvc.ObtenerTodosDatosVariablesAsync();
        _modalTitle = "Editar placeholder";
        _modalSaveAction = SavePlaceholder;
        SetPlaceholderModalContent();
        _showModal = true;
    }
```

por:

```csharp
    private async Task OpenAddPlaceholder()
    {
        _placeholderForm = new();
        _placeholderFieldSelection = "";
        _datosVariables = await EmpleadoSvc.ObtenerTodosDatosVariablesAsync();
        _modalTitle = "Agregar placeholder";
        _modalSaveAction = SavePlaceholder;
        SetPlaceholderModalContent();
        _showModal = true;
    }

    private async Task OpenEditPlaceholder(Placeholder p)
    {
        _placeholderForm = new() { Id = p.Id, PlaceholderTexto = p.PlaceholderTexto, Texto = p.Texto, DatoVariableId = p.DatoVariableId, CampoFijo = p.CampoFijo };
        _placeholderFieldSelection = p.DatoVariableId.HasValue ? $"D:{p.DatoVariableId}" : (p.CampoFijo != null ? $"F:{p.CampoFijo}" : "");
        _datosVariables = await EmpleadoSvc.ObtenerTodosDatosVariablesAsync();
        _modalTitle = "Editar placeholder";
        _modalSaveAction = SavePlaceholder;
        SetPlaceholderModalContent();
        _showModal = true;
    }

    private void OnPlaceholderFieldChange(ChangeEventArgs e)
    {
        _placeholderFieldSelection = e.Value?.ToString() ?? "";
        if (_placeholderFieldSelection.StartsWith("F:"))
        {
            _placeholderForm.CampoFijo = _placeholderFieldSelection[2..];
            _placeholderForm.DatoVariableId = null;
        }
        else if (_placeholderFieldSelection.StartsWith("D:"))
        {
            _placeholderForm.CampoFijo = null;
            _placeholderForm.DatoVariableId = int.TryParse(_placeholderFieldSelection[2..], out var id) ? id : null;
        }
        else
        {
            _placeholderForm.CampoFijo = null;
            _placeholderForm.DatoVariableId = null;
        }
    }
```

- [ ] **Step 3: Reemplazar el `<select>` del modal por el dropdown combinado**

Reemplazar:

```csharp
    private void SetPlaceholderModalContent()
    {
        _modalContent = @<div style="display:grid;grid-template-columns:1fr 1fr;gap:16px;">
            <div style="display:flex;flex-direction:column;gap:6px;">
                <label style="font-size:12.5px;font-weight:500;color:#475569;">Placeholder</label>
                <input @bind="_placeholderForm.PlaceholderTexto" placeholder="Ej. NombreEmpleado" style="@InputStyle" />
            </div>
            <div style="display:flex;flex-direction:column;gap:6px;">
                <label style="font-size:12.5px;font-weight:500;color:#475569;">Campo de datos</label>
                <select @bind="_placeholderForm.DatoVariableId" style="@SelectStyle">
                    <option value="0">— Seleccione —</option>
                    @foreach (var d in _datosVariables)
                    {
                        <option value="@d.Id">@d.NombreCampo</option>
                    }
                </select>
            </div>
            <div style="grid-column:1/-1;display:flex;flex-direction:column;gap:6px;">
                <label style="font-size:12.5px;font-weight:500;color:#475569;">Descripción</label>
                <input @bind="_placeholderForm.Texto" placeholder="Qué dato reemplaza" style="@InputStyle" />
            </div>
        </div>;
    }
```

por:

```csharp
    private void SetPlaceholderModalContent()
    {
        _modalContent = @<div style="display:grid;grid-template-columns:1fr 1fr;gap:16px;">
            <div style="display:flex;flex-direction:column;gap:6px;">
                <label style="font-size:12.5px;font-weight:500;color:#475569;">Placeholder</label>
                <input @bind="_placeholderForm.PlaceholderTexto" placeholder="Ej. NombreEmpleado" style="@InputStyle" />
            </div>
            <div style="display:flex;flex-direction:column;gap:6px;">
                <label style="font-size:12.5px;font-weight:500;color:#475569;">Campo de datos</label>
                <select value="@_placeholderFieldSelection" @onchange="OnPlaceholderFieldChange" style="@SelectStyle">
                    <option value="">— Seleccione —</option>
                    <optgroup label="Campos fijos">
                        @foreach (var kv in CamposFijosEmpleado.Campos)
                        {
                            <option value="@($"F:{kv.Key}")">@kv.Value</option>
                        }
                    </optgroup>
                    <optgroup label="Campos dinámicos">
                        @foreach (var d in _datosVariables)
                        {
                            <option value="@($"D:{d.Id}")">@d.NombreCampo</option>
                        }
                    </optgroup>
                </select>
            </div>
            <div style="grid-column:1/-1;display:flex;flex-direction:column;gap:6px;">
                <label style="font-size:12.5px;font-weight:500;color:#475569;">Descripción</label>
                <input @bind="_placeholderForm.Texto" placeholder="Qué dato reemplaza" style="@InputStyle" />
            </div>
        </div>;
    }
```

- [ ] **Step 4: Validar antes de guardar**

Reemplazar:

```csharp
    private async Task SavePlaceholder()
    {
        if (_placeholderForm.Id == 0)
            await PlaceholderSvc.AgregarPlaceholderAsync(_placeholderForm);
        else
            await PlaceholderSvc.ActualizarPlaceholderAsync(_placeholderForm);
        _placeholders = await PlaceholderSvc.ObtenerTodosLosPlaceholdersAsync();
        Snackbar.Add("Placeholder guardado.", Severity.Success);
        _showModal = false;
    }
```

por:

```csharp
    private async Task SavePlaceholder()
    {
        if (_placeholderForm.DatoVariableId == null && string.IsNullOrWhiteSpace(_placeholderForm.CampoFijo))
        {
            Snackbar.Add("Selecciona un campo.", Severity.Warning);
            return;
        }

        if (_placeholderForm.Id == 0)
            await PlaceholderSvc.AgregarPlaceholderAsync(_placeholderForm);
        else
            await PlaceholderSvc.ActualizarPlaceholderAsync(_placeholderForm);
        _placeholders = await PlaceholderSvc.ObtenerTodosLosPlaceholdersAsync();
        Snackbar.Add("Placeholder guardado.", Severity.Success);
        _showModal = false;
    }
```

- [ ] **Step 5: Mostrar la etiqueta del campo fijo en la columna "Campo" de la tabla**

En `AllRows`, caso `"Placeholder"`, reemplazar:

```csharp
        "Placeholder" => _placeholders
            .Where(p => string.IsNullOrEmpty(_search)
                || p.PlaceholderTexto.Contains(_search, StringComparison.OrdinalIgnoreCase)
                || p.Texto.Contains(_search, StringComparison.OrdinalIgnoreCase))
            .Select(p => new TableRow(
                [$"{{{{{p.PlaceholderTexto}}}}}", p.Texto, p.DatoVariable?.NombreCampo ?? "—"],
```

por:

```csharp
        "Placeholder" => _placeholders
            .Where(p => string.IsNullOrEmpty(_search)
                || p.PlaceholderTexto.Contains(_search, StringComparison.OrdinalIgnoreCase)
                || p.Texto.Contains(_search, StringComparison.OrdinalIgnoreCase))
            .Select(p => new TableRow(
                [$"{{{{{p.PlaceholderTexto}}}}}", p.Texto,
                 p.DatoVariable?.NombreCampo
                 ?? (p.CampoFijo != null ? CamposFijosEmpleado.Campos.GetValueOrDefault(p.CampoFijo) : null)
                 ?? "—"],
```

(El resto de la tupla — `Actions` con Editar/Eliminar — no cambia.)

- [ ] **Step 6: Compilar y verificar**

Run: `dotnet build`
Expected: `Compilación correcta. 0 Errores`

- [ ] **Step 7: Verificación manual**

En Administración → Placeholder → Agregar: confirmar que el dropdown
muestra los dos grupos ("Campos fijos" y "Campos dinámicos"), que se puede
crear un placeholder seleccionando "Cargo" con texto de placeholder
`ElCargo`, y que al guardar aparece en la tabla con "Cargo" en la columna
"Campo". Editar ese mismo placeholder y confirmar que el dropdown
pre-selecciona "Cargo" correctamente.

---

### Task 4: Resolución del valor al generar el certificado

**Files:**
- Modify: `Services/Documents/CreateDocService.cs` (método `ObtenerReemplazosDesdePlaceholdersAsync`, líneas ~254-276)
- Modify: `Services/Documents/CreateDocService.cs` (método `GenerarDiccionarioReemplazosAsync`, líneas ~229-234)

**Interfaces:**
- Consumes: `CamposFijosEmpleado.ObtenerValor(Empleado?, string)` (Task 2), `Placeholder.DatoVariableId`/`DatoVariable`/`CampoFijo` (Task 1).
- No produce interfaces para otras tareas (es el consumidor final de la cadena).

- [ ] **Step 1: Cambiar la firma y lógica de `ObtenerReemplazosDesdePlaceholdersAsync`**

Reemplazar:

```csharp
        private async Task<Dictionary<string, string>> ObtenerReemplazosDesdePlaceholdersAsync(string json)
        {
            var resultado = new Dictionary<string, string>();
            var datos = DatosVariablesHelper.Parse(json);
            var placeholders = await placeholderService.ObtenerTodosLosPlaceholdersAsync();

            foreach (var p in placeholders)
            {
                var claveDato = p.DatoVariable.Clave;
                if (!datos.TryGetValue(claveDato, out var valor)) continue;

                resultado[$"{{{{{p.PlaceholderTexto}}}}}"] = valor ?? "";

                if (claveDato == "salario")
                {
                    resultado["{{salario_formato}}"] = SalarioFormato(valor);
                    if (int.TryParse(valor?.Replace(".", "").Replace(",", ""), out var salarioNum))
                        resultado["{{salario_str}}"] = NumToWords(salarioNum);
                }
            }

            return resultado;
        }
```

por:

```csharp
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
```

- [ ] **Step 2: Llamar siempre a la función, sin depender de `DatosVariables` no vacío**

En `GenerarDiccionarioReemplazosAsync`, reemplazar:

```csharp
            if (!string.IsNullOrWhiteSpace(empleado?.DatosVariables))
            {
                var dinamicos = await ObtenerReemplazosDesdePlaceholdersAsync(empleado.DatosVariables);
                foreach (var kv in dinamicos)
                    reemplazos[kv.Key] = kv.Value;
            }
```

por:

```csharp
            var dinamicos = await ObtenerReemplazosDesdePlaceholdersAsync(empleado);
            foreach (var kv in dinamicos)
                reemplazos[kv.Key] = kv.Value;
```

- [ ] **Step 3: Compilar y verificar**

Run: `dotnet build`
Expected: `Compilación correcta. 0 Errores`

- [ ] **Step 4: Verificación manual**

1. Con el placeholder `{{ElCargo}}` creado en Task 3 (vinculado a "Cargo"),
   insertarlo en una plantilla de prueba.
2. Generar un certificado para un empleado con `Cargo` poblado y confirmar
   que `{{ElCargo}}` se reemplaza correctamente por el valor de `Cargo`.
3. Generar un certificado para un empleado SIN ningún dato en
   `DatosVariables` (JSON vacío o null) y confirmar que `{{ElCargo}}`
   igual se reemplaza correctamente (antes de este cambio, esto no
   ocurría porque la resolución estaba condicionada a tener
   `DatosVariables` no vacío).
4. Confirmar que un placeholder dinámico ya existente (creado antes de
   este cambio, vinculado a un `DatoVariable`) sigue funcionando sin
   modificación.

## Self-Review

- **Cobertura del spec:** Modelo + migración → Task 1. Catálogo de campos
  fijos → Task 2. UI combinada en Administración → Task 3. Resolución en
  generación de certificados (incluyendo que ya no dependa de
  `DatosVariables` no vacío) → Task 4. Todo lo del spec tiene tarea
  correspondiente.
- **Placeholders:** ninguno; todo el código está completo.
- **Consistencia de tipos:** `Placeholder.CampoFijo` (`string?`) y
  `Placeholder.DatoVariableId` (`int?`) se definen en Task 1 y se usan con
  esos tipos exactos en Task 3 y Task 4. `CamposFijosEmpleado.Campos`
  (`Dictionary<string, string>`) y `CamposFijosEmpleado.ObtenerValor`
  (`string? ObtenerValor(Empleado?, string)`) se definen en Task 2 y se
  consumen con esa firma exacta en Task 3 (UI) y Task 4 (resolución).
