# Eliminar columnas variables duplicadas y mostrar siempre el catálogo dinámico

## Contexto

Al revisar la tabla de Empleados (con el filtro por empresa y las columnas
dinámicas ya implementadas), se detectó que columnas como "Nombre", "Salario"
o "Empresa" aparecen duplicadas: una vez como campo fijo y otra vez como
columna dinámica de `DatosVariables`, con el mismo valor.

La causa raíz está en `Services/Import/ExcelService.cs`. El diccionario
`AliasesCamposClave` (líneas 286-299) mapea cada campo fijo canónico (ej.
`"nombrecompleto"`) a un arreglo de alias aceptados (ej.
`["nombre", "nombre_completo", "nombrecompleto"]`). La función `BuscarIndice`
(líneas 310-314), usada para extraer el valor de los campos fijos, sí revisa
correctamente esos arreglos de alias. Pero los dos bloques que decretan si una
columna del Excel es "nueva" (y por tanto debe registrarse como `DatoVariable`
y guardarse en el JSON `DatosVariables`) — líneas ~122-131 y ~147-153 — solo
comparan la clave normalizada contra las **claves** del diccionario
(`AliasesCamposClave.ContainsKey(clave)`), no contra los alias dentro de los
arreglos. Por eso, si el Excel trae una columna llamada "Nombre" (alias válido
de `nombrecompleto`, pero no igual a la clave canónica), su valor se extrae
correctamente al campo fijo `NombreCompleto` **y además** se registra como
variable dinámica nueva, duplicando la información.

Esto ya generó registros duplicados en la base de datos de desarrollo
(`DatoVariable`, `Placeholder`, y entradas dentro del JSON `DatosVariables` de
empleados existentes) que deben limpiarse, además de corregir el bug para que
no se repita en futuras cargas.

Por separado, la tabla de Empleados (`Components/Pages/Administracion.razor`)
actualmente solo muestra una columna dinámica si al menos un empleado de la
empresa seleccionada tiene un valor no vacío para esa clave. Se decidió
cambiar este comportamiento: mostrar siempre todas las columnas del catálogo
`DatoVariable`, con "—" cuando un empleado no tenga valor, para que una
columna no "desaparezca" simplemente porque aún no se ha cargado ese dato.

## Diseño

### 1. Fix del bug de alias en `ExcelService.cs`

Agregar un helper que determine si una clave normalizada corresponde a
**cualquier** alias de un campo fijo conocido:

```csharp
private static bool EsCampoFijoConocido(string clave) =>
    AliasesCamposClave.Values.Any(aliases => aliases.Contains(clave));
```

Y usarlo en los dos puntos que hoy comparan solo contra
`AliasesCamposClave.ContainsKey(clave)`:

- Bloque de detección de columnas nuevas (creación de `DatoVariable`, líneas
  ~122-131): cambiar `AliasesCamposClave.ContainsKey(clave)` por
  `EsCampoFijoConocido(clave)`.
- Bloque de armado del diccionario `dataDict` que se serializa en
  `DatosVariables` (líneas ~147-153): mismo cambio.

`BuscarIndice` no cambia — ya usa los alias correctamente.

### 2. Columnas dinámicas siempre visibles (`Administracion.razor`)

En `CargarDatosTab`, caso `"Empleado"`, eliminar el cálculo de
`clavesConDato` (que dependía de parsear `DatosVariables` de cada empleado
cargado). En su lugar, cuando `_filtroEmpresaId > 0`:

```csharp
_empleados = await EmpleadoSvc.ObtenerEmpleadosPorEmpresaIdAsync(_filtroEmpresaId);
_empleadoColumnasDinamicas = (await EmpleadoSvc.ObtenerTodosDatosVariablesAsync())
    .OrderBy(d => d.NombreCampo)
    .ToList();
```

Cuando `_filtroEmpresaId == 0`, se mantiene el reset de ambas listas a `[]`.

`Columns` y `AllRows` no cambian su lógica de uso de
`_empleadoColumnasDinamicas` (ya iteran sobre la lista y usan "—" como
fallback) — el cambio es solo en cómo se llena esa lista.

### 3. Limpieza única de datos existentes

Mecanismo: un bloque temporal en `Program.cs`, ejecutado solo si el proceso
se invoca con el argumento `--cleanup-datosvariables`, que:

1. Calcula el conjunto de alias conocidos:
   `var aliasConocidos = AliasesCamposClave.Values.SelectMany(a => a).ToHashSet();`
   (reutilizando el mismo diccionario de `ExcelService`, expuesto como
   `internal static` para este propósito puntual).
2. Borra los `Placeholder` cuyo `DatoVariable.Clave` esté en `aliasConocidos`.
3. Borra los `DatoVariable` cuya `Clave` esté en `aliasConocidos`.
4. Recorre todos los `Empleado`, parsea `DatosVariables` con
   `DatosVariablesHelper.Parse`, remueve las entradas cuya clave esté en
   `aliasConocidos`, y vuelve a serializar con `DatosVariablesHelper.ToJson`
   y guardar — solo si hubo cambios.
5. Imprime en consola un resumen (cuántos `Placeholder`, `DatoVariable` y
   `Empleado` se modificaron) y termina el proceso sin levantar el servidor
   web.

Este bloque se ejecuta una vez en esta sesión contra la base de datos de
desarrollo, y se elimina de `Program.cs` inmediatamente después de
confirmarse el resultado — no queda código de mantenimiento permanente.

## Fuera de alcance

- No se modifica el comportamiento de `BuscarIndice` ni la extracción de
  campos fijos — ya funciona correctamente.
- No se agrega una funcionalidad de limpieza reutilizable en la UI de
  Administración; es una corrección de datos histórica puntual.
- No se cambia el catálogo `DatoVariable` para que sea por-empresa; sigue
  siendo global, y ahora se muestra completo en cualquier empresa
  seleccionada (decisión explícita de este spec).

## Verificación

1. Compilar (`dotnet build`).
2. Ejecutar `dotnet run -- --cleanup-datosvariables` una vez; confirmar en el
   resumen impreso que se eliminaron los `DatoVariable`/`Placeholder`
   duplicados esperados (nombre, salario, empresa, etc.) y que los
   `Empleado.DatosVariables` afectados ya no contienen esas claves.
3. Quitar el bloque de limpieza de `Program.cs`.
4. Subir un Excel de prueba con una columna como "Nombre" (alias de
   `nombrecompleto`) y confirmar que ya NO se crea un `DatoVariable` nuevo
   para ella ni aparece en `DatosVariables`.
5. Abrir Administración → Empleado, elegir una empresa, y confirmar que las
   columnas dinámicas mostradas son todas las del catálogo (no solo las que
   tengan dato), con "—" donde falte el valor, y sin columnas duplicadas con
   los campos fijos.
