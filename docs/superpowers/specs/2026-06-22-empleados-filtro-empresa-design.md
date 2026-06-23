# Filtro por empresa y columnas completas en la vista de Empleados

## Contexto

La pestaña "Empleado" dentro de `Components/Pages/Administracion.razor` carga
la lista de empleados con `EmpleadoService.ObtenerTodosEmpleadosAsync()`
(`Services/Core/EmpleadoService.cs`, líneas 9-13), que trae **todos los
empleados de todas las empresas sin filtro ni paginación en BD**. Esto causa
que la carga sea lenta a medida que crece el número de empleados.

Además, la tabla solo muestra 6 columnas (Nombre completo, Cédula, Empresa,
Cargo, Salario, Fecha ingreso) de las 13 disponibles en el modelo `Empleado`,
y nunca muestra los campos dinámicos capturados en `DatosVariables` (JSON),
que es precisamente la razón por la que ese campo existe.

El objetivo es: (1) que la tabla solo cargue empleados de una empresa
seleccionada, eliminando la consulta global; y (2) que muestre todos los
campos relevantes del empleado, incluidos los dinámicos.

## Diseño

### 1. Selector de empresa con carga diferida

- Se agrega un `<select>` de Empresa en la pestaña "Empleado", siguiendo el
  patrón ya usado en `Components/Pages/GenerarDoc.razor` (líneas 33-48):
  opción inicial "— Seleccione una empresa —", lista de empresas según
  permisos del usuario (SuperAdmin/Admin ven todas; usuario normal solo las
  asignadas, mismo criterio que `GenerarDoc.razor` líneas 221-223).
- Mientras no se haya seleccionado una empresa, la tabla no hace ninguna
  consulta de empleados y muestra un mensaje ("Selecciona una empresa para
  ver sus empleados").
- Al seleccionar una empresa, se llama a
  `EmpleadoService.ObtenerEmpleadosPorEmpresaIdAsync(empresaId)` (ya existe,
  líneas 27-34), reemplazando el uso de `ObtenerTodosEmpleadosAsync()` en
  esta pestaña.
- Cambiar de empresa recarga la lista de empleados y resetea `_page = 0`.
- El resto del flujo de búsqueda/paginación en memoria
  (`AllRows`/`PagedRows`, líneas ~344) se mantiene igual, pero ahora opera
  sobre un conjunto ya acotado a una sola empresa, por lo que deja de ser un
  problema de rendimiento.

### 2. Columnas fijas adicionales

Se agregan a la definición de columnas de "Empleado" (línea ~265-266) los
campos del modelo que hoy no se muestran:

```
["Nombre completo", "Cédula", "Empresa", "Cargo", "Salario", "Fecha ingreso",
 "Email", "Tipo contrato", "Ciudad de trabajo", "Tipo ID", "Estado laboral",
 "Tipo salario"]
```

Se mantiene la columna "Empresa" aunque sea constante una vez filtrado (por
si en el futuro se permite ver todas), evitando cambios estructurales
adicionales al layout.

### 3. Columnas dinámicas (DatosVariables)

- Tras cargar la lista de empleados de la empresa seleccionada, se parsea el
  campo `DatosVariables` de cada uno con
  `Services/Documents/DatosVariablesHelper.Parse` (ya existe).
- Se calcula el conjunto de claves que tengan al menos un valor no vacío
  entre esos empleados (unión de claves presentes, ignorando vacías).
- Se obtiene el catálogo `DatoVariable` (`EmpleadoService.ObtenerTodosDatosVariablesAsync`)
  para mapear cada clave a su `NombreCampo` (nombre amigable) como encabezado
  de columna.
- Se agrega una columna por cada clave encontrada, al final de las columnas
  fijas, mostrando el valor correspondiente de cada empleado (o "—" si no
  tiene esa clave).
- Si la empresa seleccionada no tiene empleados con datos dinámicos, no se
  agrega ninguna columna extra.

## Fuera de alcance

- No se modifica `ObtenerTodosEmpleadosAsync()` ni su uso en otras vistas/flujos
  (ej. reportes globales), solo el origen de datos de esta pestaña.
- No se agrega paginación a nivel de base de datos (`Skip`/`Take` en SQL):
  una vez acotado por empresa, el volumen esperado por empresa no lo justifica.
  Si una empresa puntual llega a tener miles de empleados, se evaluaría aparte.
- No se cambia el comportamiento de búsqueda/orden existente en la tabla,
  solo se le agrega más columnas y se acota el origen de datos.
- No se modifican otras pestañas de `Administracion.razor`.

## Verificación

1. Abrir Administración → pestaña Empleado: la tabla debe iniciar vacía con
   el mensaje de selección de empresa, sin disparar ninguna consulta de
   empleados.
2. Seleccionar una empresa con empleados que tengan columnas dinámicas
   distintas (ej. una con "Departamento", otra con "Turno"): verificar que
   solo aparecen las columnas dinámicas relevantes a esa empresa.
3. Cambiar de empresa y confirmar que la tabla se recarga, la paginación se
   reinicia, y las columnas dinámicas cambian según la nueva empresa.
4. Medir/observar que el tiempo de carga mejora notablemente frente a la
   carga global anterior, especialmente en empresas con pocos empleados
   frente al total de la base de datos.
