# Permitir crear Placeholders para campos fijos del empleado

## Contexto

El usuario reportó que no puede crear un Placeholder para campos como Cargo,
Tipo de contrato, Ciudad de trabajo, Tipo ID, Estado laboral, Fecha de
ingreso o Tipo de salario.

Investigando, el sistema de Placeholders hoy tiene dos vías separadas que no
se conectan:

1. **Placeholders fijos hardcodeados** en
   `Services/Documents/CreateDocService.cs:194-252`
   (`GenerarDiccionarioReemplazosAsync`), que cubren solo
   `NombreCompleto` (`{{NombreEmpleado}}`), `Email` (`{{CorreoEmpleado}}`),
   `Cedula` (`{{CedulaEmpleado}}`) y el salario (`{{salario}}`,
   parcialmente). El resto de campos fijos del modelo `Empleado`
   (`Cargo`, `TipoContrato`, `CiudadDeTrabajo`, `TipoId`, `EstadoLaboral`,
   `FechaIngreso`, `tipoSalario`) no tienen ningún placeholder.

2. **Placeholders personalizables** (creados en Administración →
   Placeholder) que solo pueden vincularse a un `DatoVariable` — el
   dropdown del formulario (`Administracion.razor:816-838`) lista
   únicamente el catálogo de campos dinámicos del Excel, nunca los campos
   fijos. Y aunque se pudiera seleccionar uno, el método que resuelve el
   valor (`ObtenerReemplazosDesdePlaceholdersAsync`) solo busca dentro del
   JSON `DatosVariables` de cada empleado, nunca lee una columna fija como
   `Cargo`.

El objetivo es permitir crear, desde Administración → Placeholder, un
placeholder personalizado para **cualquier** campo del empleado — fijo o
dinámico — con el mismo flujo que ya existe para los dinámicos.

## Diseño

### 1. Modelo de datos (`Models/Placeholder.cs`) — requiere migración EF Core

```csharp
public class Placeholder
{
    public int Id { get; set; }
    public string Texto { get; set; } = string.Empty;
    public string PlaceholderTexto { get; set; } = string.Empty;
    public int? DatoVariableId { get; set; }
    public DatoVariable? DatoVariable { get; set; }
    public string? CampoFijo { get; set; }
}
```

Un `Placeholder` se vincula a **uno de los dos**: un `DatoVariable` (campo
dinámico, como hoy) o un campo fijo del empleado vía `CampoFijo` (clave
canónica, ej. `"cargo"`). Son mutuamente excluyentes — exactamente uno debe
tener valor.

Se genera una migración EF Core (`dotnet ef migrations add
MakePlaceholderCampoFijoOpcional`) y se aplica con `dotnet ef database
update`. No se requiere configuración adicional en `AppDbContext` — al
volver `DatoVariableId` un `int?`, EF Core infiere automáticamente la FK
como opcional.

### 2. Catálogo de campos fijos elegibles

Nuevo tipo estático (ubicación sugerida: `Services/Documents/CamposFijosEmpleado.cs`,
junto a `DatosVariablesHelper`, ya que ambos sirven a la resolución de
placeholders) con:

```csharp
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
```

Se incluyen `nombrecompleto`, `email` y `cedula` aunque ya tengan un
placeholder hardcodeado (`{{NombreEmpleado}}`, etc.) — decisión deliberada
para no introducir casos especiales: el usuario puede crear su propio
nombre de placeholder para ellos también si lo prefiere.

### 3. UI en Administración → Placeholder (`Administracion.razor`)

El formulario (`OpenAddPlaceholder`/`OpenEditPlaceholder`, líneas ~799-838)
reemplaza el `<select>` único de `DatoVariable` por un `<select>` con dos
`<optgroup>`: "Campos fijos" (iterando `CamposFijosEmpleado.Campos`) y
"Campos dinámicos" (iterando `_datosVariables`, como hoy). Para
distinguir el origen al guardar, cada `<option>` codifica su `value` con un
prefijo: `F:<clave>` para campos fijos, `D:<id>` para `DatoVariable`. Al
cambiar la selección, se parsea el prefijo y se asigna `_placeholderForm.CampoFijo`
o `_placeholderForm.DatoVariableId` (limpiando el otro a `null`).

La validación de guardado exige que exactamente uno de los dos esté
asignado antes de permitir guardar (mensaje "Selecciona un campo" si
ninguno está seleccionado).

La columna "Campo" de la tabla de Placeholders
(`AllRows`, caso `"Placeholder"`) muestra
`p.DatoVariable?.NombreCampo ?? (p.CampoFijo != null ? CamposFijosEmpleado.Campos.GetValueOrDefault(p.CampoFijo) : "—")`.

### 4. Resolución del valor al generar el certificado (`CreateDocService.cs`)

`ObtenerReemplazosDesdePlaceholdersAsync` cambia su firma de
`(string json)` a `(Empleado? empleado)`, y dentro de su bucle por cada
`Placeholder`:

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

Y en `GenerarDiccionarioReemplazosAsync` (línea 229-234), se elimina el
condicional `if (!string.IsNullOrWhiteSpace(empleado?.DatosVariables))` que
hoy envuelve la llamada — los placeholders de campo fijo deben resolverse
aunque el empleado no tenga ningún dato variable:

```csharp
var dinamicos = await ObtenerReemplazosDesdePlaceholdersAsync(empleado);
foreach (var kv in dinamicos)
    reemplazos[kv.Key] = kv.Value;
```

`GenerarDiccionarioPreviewAsync` (línea 278-308) no necesita cambios — ya
itera genéricamente sobre todos los `Placeholder` mostrando `[Texto]`,
sin importar si están vinculados a un campo fijo o dinámico.

## Fuera de alcance

- No se modifican los placeholders hardcodeados existentes
  (`{{NombreEmpleado}}`, `{{CorreoEmpleado}}`, `{{CedulaEmpleado}}`,
  `{{salario}}`) — siguen funcionando igual, en paralelo a los nuevos
  placeholders personalizables que el usuario cree para esos mismos campos.
- No se agregan campos fijos de `Empresa` o `Firmante` a este sistema —
  el reporte y el alcance acordado son específicamente sobre campos del
  empleado.
- No se valida en la UI que el usuario no cree dos placeholders distintos
  apuntando al mismo campo fijo (igual que hoy no se valida para
  `DatoVariable`) — fuera de alcance de este cambio.

## Verificación

1. Generar y aplicar la migración (`dotnet ef migrations add ...`,
   `dotnet ef database update`); confirmar que la columna `DatoVariableId`
   en la tabla `Placeholders` ahora acepta NULL y existe la nueva columna
   `CampoFijo`.
2. Compilar (`dotnet build`).
3. En Administración → Placeholder → Agregar, confirmar que el dropdown
   muestra dos grupos ("Campos fijos" y "Campos dinámicos") y que se puede
   crear un placeholder para, por ejemplo, "Cargo" con el texto
   `{{ElCargo}}`.
4. Insertar `{{ElCargo}}` en una plantilla y generar un certificado para un
   empleado con `Cargo` poblado; confirmar que el valor se imprime
   correctamente.
5. Confirmar que un empleado SIN ningún dato en `DatosVariables` también
   recibe correctamente el valor de un placeholder de campo fijo (ej.
   `{{ElCargo}}`) en su certificado.
6. Confirmar que los placeholders dinámicos existentes (creados antes de
   este cambio) siguen funcionando sin modificación.
