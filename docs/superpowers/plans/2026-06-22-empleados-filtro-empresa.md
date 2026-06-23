# Filtro por empresa y columnas completas en Empleados Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Hacer que la pestaña "Empleado" de `Administracion.razor` solo cargue empleados de una empresa seleccionada (en vez de todos los empleados de todas las empresas), y que muestre todos los campos relevantes del empleado, incluidos los campos dinámicos de `DatosVariables`.

**Architecture:** Todo el cambio vive en `Components/Pages/Administracion.razor` (UI + code-behind en el mismo archivo, patrón ya usado en el resto del archivo). Se reutilizan métodos de servicio ya existentes (`EmpleadoService.ObtenerEmpleadosPorEmpresaIdAsync`, `EmpleadoService.ObtenerTodosDatosVariablesAsync`, `DatosVariablesHelper.Parse`) — no se requieren cambios de backend ni de esquema.

**Tech Stack:** Blazor Server (.razor + code-behind inline), EF Core (vía servicios existentes), C#.

## Global Constraints

- No existe proyecto de pruebas automatizadas en este repo (no hay `*.Tests.csproj`). La verificación de cada tarea es: compilar (`dotnet build`) y probar manualmente en la app corriendo, según se describe en cada tarea. No se introduce infraestructura de testing nueva — sería un cambio fuera de alcance del spec.
- El repositorio no es un repositorio git (`git status` no aplica). Los pasos de "commit" de este plan se omiten; en su lugar, cada tarea termina con compilación + verificación manual.
- Reutilizar el patrón visual de `<select>` ya usado en `Components/Pages/GenerarDoc.razor` líneas 36-47 (estilos, manejo de `ChangeEventArgs`).
- `Administracion.razor` ya restringe el acceso a roles `SuperAdmin,Admin` (línea 3: `@attribute [Authorize(Roles = "SuperAdmin,Admin")]`), por lo que **no** se necesita lógica de permisos por usuario para la lista de empresas (a diferencia de `GenerarDoc.razor`); se reutiliza el mismo `EmpresaSvc.ObtenerEmpresasAsync()` sin filtrar, igual que ya hace la pestaña "Empresa".

---

### Task 1: Selector de empresa con carga diferida de empleados

**Files:**
- Modify: `Components/Pages/Administracion.razor:217-227` (declaración de campos de estado)
- Modify: `Components/Pages/Administracion.razor:353-359` (`SelectTab`)
- Modify: `Components/Pages/Administracion.razor:361-402` (`CargarDatosTab`, caso `"Empleado"`)
- Modify: `Components/Pages/Administracion.razor:78-101` (toolbar — agregar selector de empresa)
- Modify: `Components/Pages/Administracion.razor:103-173` (bloque de tabla — mostrar mensaje cuando no hay empresa seleccionada)

**Interfaces:**
- Consumes: `EmpresaService.ObtenerEmpresasAsync()` (ya existe, devuelve `List<Empresa>`), `EmpleadoService.ObtenerEmpleadosPorEmpresaIdAsync(int empresaId)` (ya existe, devuelve `List<Empleado>`).
- Produces: campo `_filtroEmpresaId` (int, 0 = ninguna seleccionada) y método `OnFiltroEmpresaChange(ChangeEventArgs e)`, usados por Task 2 y Task 3 para saber si hay empresa seleccionada antes de construir filas/columnas.

- [ ] **Step 1: Agregar campos de estado**

En `Components/Pages/Administracion.razor`, dentro del bloque `// ── Data ──` (alrededor de la línea 218), agregar:

```csharp
private List<Empresa> _empresas = [];
private List<Empleado> _empleados = [];
private int _filtroEmpresaId = 0;
```

(`_empresas` y `_empleados` ya existen — no duplicar, solo agregar `_filtroEmpresaId` junto a ellos.)

- [ ] **Step 2: Cargar empresas y empleados según filtro en `CargarDatosTab`**

Reemplazar el caso `"Empleado"` actual (líneas 372-375):

```csharp
case "Empleado":
    _empleados = await EmpleadoSvc.ObtenerTodosEmpleadosAsync();
    _empresaNames = (await EmpresaSvc.ObtenerEmpresasAsync()).ToDictionary(e => e.Id, e => e.Nombre ?? "");
    break;
```

por:

```csharp
case "Empleado":
    _empresas = await EmpresaSvc.ObtenerEmpresasAsync();
    _empresaNames = _empresas.ToDictionary(e => e.Id, e => e.Nombre ?? "");
    _empleados = _filtroEmpresaId > 0
        ? await EmpleadoSvc.ObtenerEmpleadosPorEmpresaIdAsync(_filtroEmpresaId)
        : [];
    break;
```

- [ ] **Step 3: Resetear el filtro al cambiar de pestaña**

En `SelectTab` (línea 353-359), resetear `_filtroEmpresaId` junto con `_search` y `_page` para que al volver a la pestaña Empleado siempre empiece sin empresa seleccionada:

```csharp
private async Task SelectTab(string tab)
{
    _activeTab = tab;
    _search = "";
    _page = 0;
    _filtroEmpresaId = 0;
    await CargarDatosTab();
}
```

- [ ] **Step 4: Agregar el manejador de cambio de empresa**

Cerca de `RecargarDatos` (línea ~404-407), agregar:

```csharp
private async Task OnFiltroEmpresaChange(ChangeEventArgs e)
{
    _filtroEmpresaId = int.TryParse(e.Value?.ToString(), out var id) ? id : 0;
    _page = 0;
    await CargarDatosTab();
}
```

- [ ] **Step 5: Agregar el `<select>` de empresa en la toolbar, solo visible en la pestaña Empleado**

En el bloque "Toolbar" (líneas 82-101), insertar el selector antes del buscador, condicionado a `_activeTab == "Empleado"`:

```razor
<div style="padding:16px 24px;display:flex;align-items:center;gap:16px;border-bottom:1px solid #e2e8f0;">
    @if (_activeTab == "Empleado")
    {
        <div style="position:relative;width:260px;">
            <select @onchange="OnFiltroEmpresaChange"
                    style="appearance:none;-webkit-appearance:none;width:100%;height:40px;border:1px solid #cbd5e1;border-radius:4px;padding:0 34px 0 12px;font-size:13.5px;font-family:Roboto,sans-serif;color:#1e293b;background:#ffffff;cursor:pointer;outline:none;"
                    onfocus="this.style.borderColor='#F97316';this.style.boxShadow='0 0 0 3px rgba(249,115,22,0.12)';"
                    onblur="this.style.borderColor='#cbd5e1';this.style.boxShadow='none';">
                <option value="0">— Seleccione una empresa —</option>
                @foreach (var e in _empresas)
                {
                    <option value="@e.Id" selected="@(e.Id == _filtroEmpresaId)">@e.Nombre</option>
                }
            </select>
            <span class="icon" style="position:absolute;right:10px;top:50%;transform:translateY(-50%);color:#64748b;pointer-events:none;font-size:18px;">expand_more</span>
        </div>
    }
    <div style="position:relative;width:320px;">
        <span class="icon" style="position:absolute;left:12px;top:50%;transform:translateY(-50%);color:#94a3b8;font-size:19px;pointer-events:none;">search</span>
        <input @bind="_search" @bind:event="oninput" type="text" placeholder="@SearchPlaceholder"
               style="width:100%;height:40px;border:1px solid #cbd5e1;border-radius:4px;padding:0 12px 0 38px;font-size:13.5px;font-family:Roboto,sans-serif;color:#1e293b;outline:none;"
               onfocus="this.style.borderColor='#F97316';this.style.boxShadow='0 0 0 3px rgba(249,115,22,0.12)';"
               onblur="this.style.borderColor='#cbd5e1';this.style.boxShadow='none';" />
    </div>
    <div style="flex:1;"></div>
    @if (ShowAddBtn)
    {
        <button @onclick="OpenAddModal"
                style="height:40px;padding:0 18px;border:none;border-radius:4px;background:#F97316;color:#ffffff;font-family:Roboto,sans-serif;font-size:13px;font-weight:500;letter-spacing:0.5px;text-transform:uppercase;cursor:pointer;box-shadow:0 2px 6px rgba(249,115,22,0.35);display:flex;align-items:center;gap:7px;"
                onmouseover="this.style.background='#ea580c';"
                onmouseout="this.style.background='#F97316';">
            <span class="icon" style="font-size:19px;">add</span>
            @AddLabel
        </button>
    }
</div>
```

- [ ] **Step 6: Mostrar mensaje cuando no hay empresa seleccionada, en vez de la tabla vacía**

En el bloque que renderiza la tabla (línea ~103, justo después de `else` del `_loading`), envolver la tabla existente con una condición adicional. Reemplazar la apertura:

```razor
else
{
    <div style="overflow-x:auto;">
        <table class="admin-table">
```

por:

```razor
else if (_activeTab == "Empleado" && _filtroEmpresaId == 0)
{
    <div style="padding:60px;text-align:center;color:#94a3b8;font-size:13.5px;">
        Selecciona una empresa para ver sus empleados.
    </div>
}
else
{
    <div style="overflow-x:auto;">
        <table class="admin-table">
```

(El resto de la tabla — `</table></div>` y el bloque de paginación que le sigue — no cambia; el `}` de cierre de este `else` ya existente sigue cerrando el bloque de la tabla.)

- [ ] **Step 7: Compilar y verificar**

Run: `dotnet build`
Expected: `Compilación correcta. 0 Errores`

- [ ] **Step 8: Verificación manual**

Ejecutar la app (`dotnet run`), ir a Administración → pestaña Empleado:
- Debe aparecer el mensaje "Selecciona una empresa para ver sus empleados." y la tabla NO debe mostrar filas.
- Seleccionar una empresa: debe aparecer la tabla con solo los empleados de esa empresa.
- Cambiar a otra pestaña y volver a "Empleado": el selector debe volver a "— Seleccione una empresa —" y la tabla debe estar vacía de nuevo (confirma el reset del Step 3).

---

### Task 2: Columnas fijas adicionales

**Files:**
- Modify: `Components/Pages/Administracion.razor:266` (`Columns`, caso `"Empleado"`)
- Modify: `Components/Pages/Administracion.razor:293-296` (`AllRows`, caso `"Empleado"`)

**Interfaces:**
- Consumes: `_filtroEmpresaId` y `_empleados` de Task 1 (sin cambios de tipo).
- Produces: ninguna interfaz nueva para otras tareas; Task 3 modifica el mismo `Columns`/`AllRows` para el caso `"Empleado"` añadiendo columnas dinámicas al final de las que esta tarea define.

- [ ] **Step 1: Ampliar las columnas fijas de "Empleado"**

En `Columns` (línea 266), reemplazar:

```csharp
"Empleado" => ["Nombre completo", "Cédula", "Empresa", "Cargo", "Salario", "Fecha ingreso"],
```

por:

```csharp
"Empleado" => ["Nombre completo", "Cédula", "Empresa", "Cargo", "Salario", "Fecha ingreso",
               "Email", "Tipo contrato", "Ciudad de trabajo", "Tipo ID", "Estado laboral", "Tipo salario"],
```

- [ ] **Step 2: Ampliar las celdas de cada fila de "Empleado"**

En `AllRows`, caso `"Empleado"` (líneas 293-296), reemplazar:

```csharp
.Select(e => new TableRow(
    [e.NombreCompleto ?? "—", e.Cedula ?? "—",
     _empresaNames.GetValueOrDefault(e.EmpresaId, "—"),
     e.Cargo ?? "—", e.SalarioMensual ?? "—", e.FechaIngreso.ToString("yyyy-MM-dd")],
```

por:

```csharp
.Select(e => new TableRow(
    [e.NombreCompleto ?? "—", e.Cedula ?? "—",
     _empresaNames.GetValueOrDefault(e.EmpresaId, "—"),
     e.Cargo ?? "—", e.SalarioMensual ?? "—", e.FechaIngreso.ToString("yyyy-MM-dd"),
     e.Email ?? "—", e.TipoContrato ?? "—", e.CiudadDeTrabajo ?? "—", e.TipoId ?? "—",
     e.EstadoLaboral ? "Activo" : "Inactivo", e.tipoSalario ?? "—"],
```

(La parte de `Actions` que sigue, líneas 297-299, no cambia.)

- [ ] **Step 3: Compilar y verificar**

Run: `dotnet build`
Expected: `Compilación correcta. 0 Errores`

- [ ] **Step 4: Verificación manual**

Con una empresa seleccionada (requiere Task 1 ya implementado), confirmar que la tabla muestra las 12 columnas nuevas con datos correctos para empleados que tengan esos campos poblados, y "—" para los que no.

---

### Task 3: Columnas dinámicas de `DatosVariables`

**Files:**
- Modify: `Components/Pages/Administracion.razor:217-227` (nuevo campo de estado)
- Modify: `Components/Pages/Administracion.razor:361-402` (`CargarDatosTab`, caso `"Empleado"` — calcular columnas dinámicas tras cargar empleados)
- Modify: `Components/Pages/Administracion.razor:263-272` (`Columns`, caso `"Empleado"` — anexar columnas dinámicas)
- Modify: `Components/Pages/Administracion.razor:289-300` (`AllRows`, caso `"Empleado"` — anexar celdas dinámicas)

**Interfaces:**
- Consumes: `EmpleadoService.ObtenerTodosDatosVariablesAsync()` (ya existe, devuelve `List<DatoVariable>` con propiedades `Clave` y `NombreCampo`), `DatosVariablesHelper.Parse(string? json)` (ya existe en `CertificadosLaboralesV2.Services.Documents`, devuelve `Dictionary<string, string>`).
- Produces: campo `_empleadoColumnasDinamicas` (`List<DatoVariable>`), consumido únicamente dentro de esta misma vista (no hay tareas posteriores).

- [ ] **Step 1: Agregar el campo de estado para las columnas dinámicas**

Junto a `_filtroEmpresaId` (agregado en Task 1, Step 1), agregar:

```csharp
private List<DatoVariable> _empleadoColumnasDinamicas = [];
```

- [ ] **Step 2: Calcular las columnas dinámicas tras cargar empleados de la empresa seleccionada**

En `CargarDatosTab`, caso `"Empleado"` (modificado en Task 1, Step 2), reemplazar:

```csharp
case "Empleado":
    _empresas = await EmpresaSvc.ObtenerEmpresasAsync();
    _empresaNames = _empresas.ToDictionary(e => e.Id, e => e.Nombre ?? "");
    _empleados = _filtroEmpresaId > 0
        ? await EmpleadoSvc.ObtenerEmpleadosPorEmpresaIdAsync(_filtroEmpresaId)
        : [];
    break;
```

por:

```csharp
case "Empleado":
    _empresas = await EmpresaSvc.ObtenerEmpresasAsync();
    _empresaNames = _empresas.ToDictionary(e => e.Id, e => e.Nombre ?? "");
    if (_filtroEmpresaId > 0)
    {
        _empleados = await EmpleadoSvc.ObtenerEmpleadosPorEmpresaIdAsync(_filtroEmpresaId);
        var catalogo = await EmpleadoSvc.ObtenerTodosDatosVariablesAsync();
        var clavesConDato = _empleados
            .SelectMany(e => DatosVariablesHelper.Parse(e.DatosVariables))
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
            .Select(kv => kv.Key)
            .ToHashSet();
        _empleadoColumnasDinamicas = catalogo
            .Where(d => clavesConDato.Contains(d.Clave))
            .OrderBy(d => d.NombreCampo)
            .ToList();
    }
    else
    {
        _empleados = [];
        _empleadoColumnasDinamicas = [];
    }
    break;
```

- [ ] **Step 3: Anexar las columnas dinámicas a `Columns`**

Reemplazar el caso `"Empleado"` de `Columns` (ya con las 12 columnas fijas de Task 2) por una versión que anexe las dinámicas. Cambiar la firma de `Columns` de expresión `switch` a método si es necesario; más simple: cambiar la línea del caso `"Empleado"` así:

```csharp
"Empleado" => [.. new[] { "Nombre completo", "Cédula", "Empresa", "Cargo", "Salario", "Fecha ingreso",
                          "Email", "Tipo contrato", "Ciudad de trabajo", "Tipo ID", "Estado laboral", "Tipo salario" },
               .. _empleadoColumnasDinamicas.Select(d => d.NombreCampo)],
```

- [ ] **Step 4: Anexar las celdas dinámicas a cada fila en `AllRows`**

Reemplazar el `Select` del caso `"Empleado"` (de Task 2) por:

```csharp
.Select(e =>
{
    var datos = DatosVariablesHelper.Parse(e.DatosVariables);
    var celdasFijas = new List<string>
    {
        e.NombreCompleto ?? "—", e.Cedula ?? "—",
        _empresaNames.GetValueOrDefault(e.EmpresaId, "—"),
        e.Cargo ?? "—", e.SalarioMensual ?? "—", e.FechaIngreso.ToString("yyyy-MM-dd"),
        e.Email ?? "—", e.TipoContrato ?? "—", e.CiudadDeTrabajo ?? "—", e.TipoId ?? "—",
        e.EstadoLaboral ? "Activo" : "Inactivo", e.tipoSalario ?? "—"
    };
    var celdasDinamicas = _empleadoColumnasDinamicas
        .Select(d => datos.TryGetValue(d.Clave, out var v) && !string.IsNullOrWhiteSpace(v) ? v : "—");
    return new TableRow(
        [.. celdasFijas, .. celdasDinamicas],
        [
            new("edit", "Editar", "#475569", EventCallback.Factory.Create(this, () => OpenEditEmpleado(e))),
            new("delete", "Eliminar", "#dc2626", EventCallback.Factory.Create(this, () => DeleteEmpleado(e.Id)))
        ]);
}).ToList(),
```

- [ ] **Step 5: Compilar y verificar**

Run: `dotnet build`
Expected: `Compilación correcta. 0 Errores`

- [ ] **Step 6: Verificación manual**

1. Elegir una empresa cuyos empleados tengan columnas variables distintas a otra empresa (por ejemplo, una con "Departamento" y otra con "Turno", cargadas previamente vía Excel según el flujo de la sesión anterior).
2. Confirmar que solo aparecen las columnas dinámicas con datos para la empresa seleccionada (no las de otras empresas, ni columnas vacías para todos los empleados).
3. Cambiar de empresa y confirmar que las columnas dinámicas cambian según corresponda.
4. Confirmar que un empleado sin valor para una columna dinámica muestra "—" en esa celda.

---

## Self-Review

- **Cobertura del spec:** Selector de empresa + carga diferida → Task 1. Columnas fijas adicionales → Task 2. Columnas dinámicas → Task 3. Mensaje de "selecciona una empresa" → Task 1 Step 6. Reset de paginación al cambiar empresa → Task 1 Step 4. Todo lo cubierto en el spec tiene tarea correspondiente.
- **Placeholders:** ninguno; todos los pasos incluyen código completo y comandos exactos.
- **Consistencia de tipos:** `_filtroEmpresaId` (int) y `_empleadoColumnasDinamicas` (`List<DatoVariable>`) se usan con el mismo nombre y tipo en las tres tareas. `OnFiltroEmpresaChange` se define una sola vez en Task 1 y no se redefine en tareas posteriores.
