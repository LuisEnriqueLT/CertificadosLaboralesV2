# Eliminar columnas variables duplicadas y mostrar catálogo completo Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Corregir el bug que hace que columnas fijas (Nombre, Salario, Empresa, etc.) se dupliquen como columnas variables de `DatosVariables`; mostrar siempre todas las columnas dinámicas del catálogo en la tabla de Empleados (no solo las que tengan dato); y limpiar los duplicados ya existentes en la base de datos de desarrollo.

**Architecture:** El bug vive en `Services/Import/ExcelService.cs` (comparación incorrecta contra claves del diccionario de alias en vez de sus valores). La visualización vive en `Components/Pages/Administracion.razor` (filtro de columnas dinámicas a eliminar). La limpieza de datos históricos es un bloque temporal en `Program.cs`, ejecutado una vez vía argumento de línea de comandos y luego eliminado — no es funcionalidad permanente.

**Tech Stack:** Blazor Server, EF Core (SQL Server), C#.

## Global Constraints

- No existe proyecto de pruebas automatizadas en este repo; la verificación de cada tarea es `dotnet build` con 0 errores, más verificación manual donde se indique.
- El repositorio no tiene git; no hay commits ni diffs — cada tarea termina con compilación + verificación manual/script.
- El catálogo `DatoVariable` es global (no por empresa) y así se mantiene; el cambio de visualización solo afecta qué tan filtrado se muestra en la tabla de Empleados.

---

### Task 1: Corregir el bug de alias en `ExcelService.cs`

**Files:**
- Modify: `Services/Import/ExcelService.cs:286-299` (visibilidad de `AliasesCamposClave` + nuevo helper)
- Modify: `Services/Import/ExcelService.cs:126` (detección de columna nueva → `DatoVariable`)
- Modify: `Services/Import/ExcelService.cs:159` (armado de `dataDict` para `DatosVariables`)

**Interfaces:**
- Produces: `internal static bool ExcelService.EsCampoFijoConocido(string clave)` y `internal static readonly Dictionary<string, string[]> ExcelService.AliasesCamposClave` (visibilidad cambiada de `private` a `internal` para que Task 3 los reutilice desde `Program.cs`, mismo ensamblado).

- [ ] **Step 1: Cambiar la visibilidad de `AliasesCamposClave` y agregar el helper de alias**

En `Services/Import/ExcelService.cs`, reemplazar:

```csharp
        private static readonly Dictionary<string, string[]> AliasesCamposClave = new()
        {
            { "email", ["email", "correo", "correo_electronico", "mail"] },
            { "nombrecompleto", ["nombre", "nombre_completo", "nombrecompleto"] },
            { "cedula", ["cedula", "numero_id", "id", "identificacion"] },
            { "empresaid", ["nit_empresa", "nit", "empresa"] },
            { "fechaingreso", ["fecha_ingreso", "fechaingreso", "fecha_de_ingreso"] },
            { "cargo", ["cargo"] },
            { "salariomensual", ["sueldo", "salario", "salario_mensual", "salariomensual"] },
            { "tiposalario", ["tipo_sueldo", "tipo_salario", "tiposalario"] },
            { "tipocontrato", ["tipo_contrato", "tipocontrato"] },
            { "ciudaddetrabajo", ["ciudad_trabajo", "ciudad_de_trabajo", "ciudaddetrabajo"] },
            { "tipoid", ["tipo_id", "tipoid"] }
        };
```

por:

```csharp
        internal static readonly Dictionary<string, string[]> AliasesCamposClave = new()
        {
            { "email", ["email", "correo", "correo_electronico", "mail"] },
            { "nombrecompleto", ["nombre", "nombre_completo", "nombrecompleto"] },
            { "cedula", ["cedula", "numero_id", "id", "identificacion"] },
            { "empresaid", ["nit_empresa", "nit", "empresa"] },
            { "fechaingreso", ["fecha_ingreso", "fechaingreso", "fecha_de_ingreso"] },
            { "cargo", ["cargo"] },
            { "salariomensual", ["sueldo", "salario", "salario_mensual", "salariomensual"] },
            { "tiposalario", ["tipo_sueldo", "tipo_salario", "tiposalario"] },
            { "tipocontrato", ["tipo_contrato", "tipocontrato"] },
            { "ciudaddetrabajo", ["ciudad_trabajo", "ciudad_de_trabajo", "ciudaddetrabajo"] },
            { "tipoid", ["tipo_id", "tipoid"] }
        };

        internal static bool EsCampoFijoConocido(string clave) =>
            AliasesCamposClave.Values.Any(aliases => aliases.Contains(clave));
```

(Solo cambia `private` → `internal` en la primera línea, y se agrega el método nuevo justo después de la llave de cierre `};` del diccionario.)

- [ ] **Step 2: Usar el helper en la detección de columnas nuevas**

Reemplazar (línea ~126):

```csharp
                if (AliasesCamposClave.ContainsKey(clave) || existentesDict.ContainsKey(clave)) continue;
```

por:

```csharp
                if (EsCampoFijoConocido(clave) || existentesDict.ContainsKey(clave)) continue;
```

- [ ] **Step 3: Usar el helper en el armado del JSON `DatosVariables`**

Reemplazar (línea ~159):

```csharp
                    if (AliasesCamposClave.ContainsKey(clave)) continue;
```

por:

```csharp
                    if (EsCampoFijoConocido(clave)) continue;
```

- [ ] **Step 4: Compilar y verificar**

Run: `dotnet build`
Expected: `Compilación correcta. 0 Errores`

- [ ] **Step 5: Verificación manual**

Subir un Excel de prueba con una columna llamada "Nombre" (alias de `nombrecompleto`, no la clave canónica) y confirmar que:
- El valor se extrae correctamente al campo fijo `NombreCompleto` del empleado (esto ya funcionaba antes, vía `BuscarIndice`).
- NO se crea un nuevo `DatoVariable` para "nombre".
- "nombre" NO aparece como clave en el JSON `DatosVariables` del empleado.

---

### Task 2: Mostrar siempre el catálogo completo de columnas dinámicas

**Files:**
- Modify: `Components/Pages/Administracion.razor` (método `CargarDatosTab`, caso `"Empleado"`)

**Interfaces:**
- Consumes: `EmpleadoService.ObtenerEmpleadosPorEmpresaIdAsync(int)`, `EmpleadoService.ObtenerTodosDatosVariablesAsync()` (ambos ya existen, sin cambios de firma).
- No introduce nuevas interfaces para otras tareas.

- [ ] **Step 1: Simplificar el cálculo de `_empleadoColumnasDinamicas`**

En `Components/Pages/Administracion.razor`, dentro de `CargarDatosTab`, reemplazar el caso `"Empleado"` actual:

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

por:

```csharp
                case "Empleado":
                    _empresas = await EmpresaSvc.ObtenerEmpresasAsync();
                    _empresaNames = _empresas.ToDictionary(e => e.Id, e => e.Nombre ?? "");
                    if (_filtroEmpresaId > 0)
                    {
                        _empleados = await EmpleadoSvc.ObtenerEmpleadosPorEmpresaIdAsync(_filtroEmpresaId);
                        _empleadoColumnasDinamicas = (await EmpleadoSvc.ObtenerTodosDatosVariablesAsync())
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

No se modifican `Columns` ni `AllRows` — ya iteran genéricamente sobre `_empleadoColumnasDinamicas` y usan "—" como fallback cuando el empleado no tiene esa clave en su `DatosVariables`.

- [ ] **Step 2: Compilar y verificar**

Run: `dotnet build`
Expected: `Compilación correcta. 0 Errores`

- [ ] **Step 3: Verificación manual**

Abrir Administración → Empleado, elegir una empresa cualquiera, y confirmar que aparecen TODAS las columnas dinámicas del catálogo `DatoVariable` (no solo las que tengan dato para esa empresa), con "—" en las celdas sin valor.

---

### Task 3: Limpiar duplicados existentes en la base de datos (una sola vez)

**Files:**
- Modify (temporalmente): `Program.cs` (bloque de limpieza agregado y luego removido en este mismo task)

**Interfaces:**
- Consumes: `ExcelService.AliasesCamposClave` y `ExcelService.EsCampoFijoConocido` (de Task 1, ya `internal`), `AppDbContext` (vía `IDbContextFactory<AppDbContext>`, ya registrado en DI), `DatosVariablesHelper.Parse`/`ToJson` (ya existen en `Services/Documents/DatosVariablesHelper.cs`).
- No produce interfaces para otras tareas — este código se elimina al final del task.

- [ ] **Step 1: Agregar el bloque de limpieza en `Program.cs`**

En `Program.cs`, justo después de:

```csharp
using (var scope = app.Services.CreateScope())
{
    await SeedData.InitializeAsync(scope.ServiceProvider);
}
```

agregar:

```csharp
if (args.Contains("--cleanup-datosvariables"))
{
    using var cleanupScope = app.Services.CreateScope();
    var factory = cleanupScope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    using var db = await factory.CreateDbContextAsync();

    var aliasConocidos = CertificadosLaboralesV2.Services.Import.ExcelService.AliasesCamposClave
        .Values.SelectMany(a => a).ToHashSet();

    var datoVariablesDuplicados = await db.DatosVariables
        .Where(d => aliasConocidos.Contains(d.Clave))
        .ToListAsync();
    var idsDuplicados = datoVariablesDuplicados.Select(d => d.Id).ToHashSet();

    var placeholdersDuplicados = await db.Placeholders
        .Where(p => idsDuplicados.Contains(p.DatoVariableId))
        .ToListAsync();
    db.Placeholders.RemoveRange(placeholdersDuplicados);
    db.DatosVariables.RemoveRange(datoVariablesDuplicados);

    var empleados = await db.Empleados.ToListAsync();
    var empleadosModificados = 0;
    foreach (var empleado in empleados)
    {
        var datos = DatosVariablesHelper.Parse(empleado.DatosVariables);
        var clavesAEliminar = datos.Keys.Where(k => aliasConocidos.Contains(k)).ToList();
        if (clavesAEliminar.Count == 0) continue;

        foreach (var clave in clavesAEliminar) datos.Remove(clave);
        empleado.DatosVariables = DatosVariablesHelper.ToJson(datos);
        empleadosModificados++;
    }

    await db.SaveChangesAsync();

    Console.WriteLine($"Limpieza completada: {placeholdersDuplicados.Count} Placeholder(s), " +
        $"{datoVariablesDuplicados.Count} DatoVariable(s), {empleadosModificados} Empleado(s) modificado(s).");
    return;
}
```

Este bloque necesita `using CertificadosLaboralesV2.Services.Documents;` ya presente en el `using` global del archivo (confirmar que está en la parte superior; si no, agregarlo junto a los demás `using CertificadosLaboralesV2.Services.*;` de las líneas 4-7).

- [ ] **Step 2: Compilar**

Run: `dotnet build`
Expected: `Compilación correcta. 0 Errores`

- [ ] **Step 3: Ejecutar la limpieza una vez**

Run: `dotnet run -- --cleanup-datosvariables`
Expected: el proceso imprime una línea como
`Limpieza completada: N Placeholder(s), N DatoVariable(s), N Empleado(s) modificado(s).`
y termina sin levantar el servidor web (por el `return;` antes de `app.Run()`).

- [ ] **Step 4: Verificación manual de la limpieza**

Revisar (por ejemplo, abriendo Administración → Placeholder y → Empleado, o consultando la BD) que:
- No quedan `DatoVariable`/`Placeholder` cuya clave sea "nombre", "salario", "empresa", u otro alias de la lista `AliasesCamposClave`.
- Los `Empleado.DatosVariables` que antes tenían esas claves duplicadas ya no las tienen, conservando el resto de sus datos variables intactos.

- [ ] **Step 5: Remover el bloque de limpieza de `Program.cs`**

Eliminar el bloque `if (args.Contains("--cleanup-datosvariables")) { ... }` agregado en el Step 1 — es una corrección de datos histórica puntual, no debe quedar código de mantenimiento permanente en el arranque de la app.

- [ ] **Step 6: Compilar y verificar que la app sigue arrancando normalmente**

Run: `dotnet build`
Expected: `Compilación correcta. 0 Errores`

Run manual: `dotnet run` (sin argumentos) y confirmar que la app levanta el servidor web normalmente (sin el bloque de limpieza ya removido).

---

## Self-Review

- **Cobertura del spec:** Fix de alias en ExcelService → Task 1. Columnas dinámicas siempre visibles → Task 2. Limpieza de datos existentes → Task 3. Todo lo descrito en el spec tiene tarea correspondiente.
- **Placeholders:** ninguno; todo el código está completo y es ejecutable tal cual.
- **Consistencia de tipos:** `ExcelService.AliasesCamposClave` e `ExcelService.EsCampoFijoConocido` se definen en Task 1 con visibilidad `internal` y se consumen exactamente con esos nombres en Task 3. `_empleadoColumnasDinamicas` (tipo `List<DatoVariable>`, ya existente) no cambia de tipo en Task 2, solo cambia cómo se llena.
