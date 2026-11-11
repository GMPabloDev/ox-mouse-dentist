# SPEC 01 — Acceso de administración y gestión de locales

> **Status:** Implementada **Date:** 2026-11-10 **Objective:** Permitir que el administrador inicie
> sesión y gestione los locales de MouseDent. **Historia de origen:** H-02 en
> backlogs/backlog-v1-operacion-interna.md

## Alcance

**Incluye:**

- Acceso web con la cuenta de administrador preparada por el equipo técnico y cierre de sesión.
- Consulta, creación, edición, desactivación y reactivación de locales por un administrador
  autenticado.
- Nombre, dirección, teléfono y horario semanal obligatorios. Foto opcional.
- Persistencia de los locales y de sus fotos para la evaluación. El administrador registra los
  locales existentes desde la aplicación.
- Pantallas funcionales para móvil y escritorio conforme a la referencia visual elegida: listado A
  con filas y fotografía, formulario de creación de la versión 2 y horario A por grupos.

**Fuera de alcance:**

- Registro inicial de administradores desde el producto y preparación de la primera cuenta fuera de
  Development.
- Gestión de cuentas, perfiles y asignaciones del personal, que corresponde a H-06.
- Citas, profesionales y su disponibilidad. El horario del local no es el horario de un profesional.
- Página pública de locales y publicación de las fotos. Una historia posterior definirá su acceso
  público.
- Horarios con pausas, turnos nocturnos, feriados o excepciones por fecha.
- Eliminación definitiva de locales y almacenamiento de fotos en un servicio externo.
- Las variantes no elegidas del prototipo: listado B de directorio y horario B de semana visual. El
  diseño de otras pantallas y módulos queda para sus historias.

## Comportamiento

### Acceso

- Para la evaluación, el equipo técnico prepara la primera cuenta con la configuración de
  Development existente. El administrador utiliza esa cuenta para iniciar sesión en la web.
- La sesión se conserva al recargar. Termina al cerrar sesión o cuando caduca el token. Si caduca,
  la web pide iniciar sesión de nuevo.
- Solo un administrador autenticado puede consultar o gestionar los locales y sus fotos. La API
  rechaza solicitudes sin autenticación o con un perfil no autorizado, aunque se invoque
  directamente.
- Las credenciales incorrectas se comunican sin indicar si existe la cuenta. Los errores de conexión
  y del servidor se muestran sin informar que la operación tuvo éxito.

### Locales

- La lista comienza sin locales precargados. El administrador puede registrar los dos locales
  existentes y añadir otros.
- El administrador consulta los locales activos e inactivos y distingue su estado. Puede editar sus
  datos, desactivarlos y volver a activarlos. Desactivar no borra la información.
- El nombre es único entre todos los locales, incluidos los inactivos. Para detectar duplicados se
  ignoran las mayúsculas y minúsculas y los espacios al principio y al final.
- La dirección y el teléfono son texto obligatorio. No se exige un formato de dirección ni un
  formato telefónico específico.
- El horario se define para cada día de la semana. Cada día está cerrado o tiene una hora de
  apertura y una de cierre. Debe existir al menos un día abierto y, en cada día abierto, la apertura
  debe ser anterior al cierre dentro del mismo día.
- La web indica los errores de validación y conserva los datos introducidos para corregirlos. Ante
  un fallo al consultar o guardar, comunica el error y permite volver a intentar la operación.

### Foto

- Cada local puede tener una foto. El administrador puede subirla, cambiarla o quitarla y ver una
  vista previa durante la gestión.
- Se aceptan imágenes JPG, PNG o WebP de hasta 5 MB. La API valida el tipo y el tamaño del archivo y
  rechaza archivos no admitidos.
- La foto se almacena en almacenamiento persistente del servidor, fuera del repositorio. El
  despliegue debe conservar y respaldar ese almacenamiento.
- La foto y los demás cambios se confirman al guardar el local. Si se cancela o falla el guardado,
  se conserva la versión guardada anteriormente, incluida su foto. Una creación fallida no deja un
  local visible parcialmente creado.
- Solo un administrador autenticado puede consultar las fotos en esta entrega.

## Referencia visual elegida

El usuario eligió el listado A, el formulario de creación de la versión 2 y el horario A. Esta
selección define la referencia de UI de H-02.

El prototipo local está en `../prototypes/ox-prototype/locales-abrazo-v2-20261110/`, con entrada en
`../prototypes/ox-prototype/locales-abrazo-v2-20261110/index.html`. Las rutas son relativas a esta
spec.

| Elemento               | Elección                                                                            | Variante del prototipo        |
| ---------------------- | ----------------------------------------------------------------------------------- | ----------------------------- |
| Listado de locales     | A · Filas con fotografía                                                            | `?list=rows`                  |
| Formulario de creación | Formulario de la versión 2, con datos y fotografía en una sección y horario en otra | `?view=editor&variant=groups` |
| Editor de horario      | A · Por grupos                                                                      | `?view=editor&variant=groups` |

### Alcance de la referencia

- El listado presenta los locales en filas con fotografía o indicación de ausencia de foto, nombre,
  dirección, estado textual y acciones de gestión. En escritorio muestra también la atención semanal
  desde 1280 px de viewport y el contacto desde 1536 px, como la referencia elegida. En pantallas
  estrechas reorganiza la información y las acciones sin exigir desplazamiento horizontal.
- El formulario de creación presenta datos y fotografía lado a lado cuando hay ancho suficiente y
  los apila en móvil. El área de fotografía tiene vista previa grande, selección de archivo,
  arrastre, cambio y eliminación. Permite ampliar el original y deshacer un cambio o eliminación
  antes de guardar. Estos controles no alteran el guardado conjunto ni las reglas de la sección
  Foto.
- El horario permite seleccionar varios días, elegir una sugerencia o ajustar las horas, aplicar el
  tramo a los días elegidos y marcarlos como cerrados. El resumen semanal muestra el horario
  aplicado. Aplicar al grupo no guarda el local: los datos y la foto se confirman con el guardado
  del formulario.
- En escritorio, los controles del horario por grupos y el resumen se muestran en paralelo. En móvil
  se apilan y conservan el mismo comportamiento. Se mantiene un único tramo por día abierto, sin
  pausas ni turnos nocturnos.
- La referencia conserva la identidad Abrazo de `estilos/`, el contenido fluido y el sidebar
  inspirado en Spartan. Usa márgenes laterales de 72 px en escritorio, 56 px hasta 1279 px de
  viewport y 16 px hasta 767 px. No introduce destinos de navegación de otras historias.
- La implementación no incluye los selectores de comparación A y B, los controles de simulación, las
  etiquetas de demostración ni las variantes descartadas. La selección del formulario de creación no
  elimina los requisitos de edición de locales definidos en esta spec.

### Evidencia y límites del prototipo

Las capturas locales elegidas son
`../prototypes/ox-prototype/locales-abrazo-v2-20261110/lista-filas-1920.png`,
`../prototypes/ox-prototype/locales-abrazo-v2-20261110/crear-local-1920.png` y
`../prototypes/ox-prototype/locales-abrazo-v2-20261110/editor-grupos-horario-1920.png`. El
directorio contiene también sus capturas móviles y las comprobaciones en
`../prototypes/ox-prototype/locales-abrazo-v2-20261110/verificacion.json`.

El prototipo es código desechable con datos ficticios y fotos de referencia, sin API ni
persistencia. Simula el acceso, el guardado, los fallos y los cambios de estado en memoria. Recargar
restaura los ejemplos. Esas simulaciones no sustituyen las validaciones de la API, la autorización,
la sesión ni el almacenamiento persistente exigidos por esta spec. Los ejemplos del prototipo no se
precargan en la aplicación.

`prototypes/` está ignorado por Git. El prototipo, sus fotos y sus capturas son referencias locales
y no se recuperan desde el repositorio. Las elecciones y el comportamiento descritos en esta sección
quedan registrados en la spec, sin promover el código del experimento a producción.

## Plan de implementación

### Bloque 1: acceso del administrador

1. Conectar la pantalla de acceso con los endpoints de autenticación existentes y mantener la sesión
   al recargar.
2. Incorporar el cierre de sesión y el tratamiento de credenciales incorrectas y sesión caducada.
3. Proteger el panel para que solo el perfil `Administrador` acceda a la gestión de locales.
4. Verificar el acceso con la cuenta de Development y el rechazo de sesiones anónimas y de otros
   perfiles.

### Bloque 2: locales y fotos

1. Incorporar el modelo de locales y su persistencia con una migración, respetando las capas
   existentes.
2. Implementar los casos de uso y las operaciones de la API para consultar y gestionar locales,
   validar los datos y conservar el estado activo o inactivo. Restringir las operaciones al perfil
   `Administrador`.
3. Incorporar el almacenamiento persistente de fotos y el guardado conjunto con los datos del local.
   Proteger la consulta de fotos.
4. Generar el documento OpenAPI y el cliente TypeScript conforme a las convenciones del repositorio.
5. Verificar las reglas de horarios, unicidad, autorización, persistencia y fallo del guardado con
   pruebas de comportamiento.

### Bloque 3: gestión web y verificación

1. Crear la gestión web para consultar, registrar, editar, desactivar y reactivar locales, con
   edición de horario y foto. Seguir el listado A con filas y fotografía, el formulario de creación
   de la versión 2 y el editor de horario A por grupos descritos en la referencia visual elegida.
2. Mostrar los estados de carga, lista vacía, validación y error necesarios para completar o
   reintentar las operaciones.
3. Comprobar el recorrido de administrador en móvil y escritorio, incluida la recarga de la sesión y
   la persistencia de datos y fotos.
4. Ejecutar `npm test` y `npm run build` y verificar los artefactos generados del contrato.

## Criterios de aceptación

- [x] El administrador preparado en Development puede iniciar sesión desde la web, recargar y seguir
      autenticado mientras el token sea válido.
- [x] Al cerrar sesión o caducar el token, el acceso a la gestión requiere iniciar sesión otra vez.
- [x] La web muestra un error para credenciales incorrectas y no distingue entre cuenta inexistente
      y contraseña incorrecta.
- [x] La API rechaza las operaciones de locales y fotos sin autenticación y las deniega a cuentas
      sin perfil `Administrador`.
- [x] Con una base sin locales, el administrador ve una lista vacía y puede registrar los locales
      existentes desde la aplicación.
- [x] El administrador puede consultar y editar locales activos e inactivos, desactivar uno y
      reactivarlo sin perder sus datos.
- [x] El sistema rechaza un nombre repetido, incluso si difiere solo en mayúsculas, minúsculas o
      espacios al principio o al final, también cuando el local existente está inactivo.
- [x] El sistema exige nombre, dirección, teléfono y un horario con al menos un día abierto. No
      exige un formato concreto de dirección o teléfono.
- [x] En cada día abierto, el sistema exige una apertura anterior al cierre dentro del mismo día.
      Permite marcar días cerrados.
- [x] El administrador puede guardar un local sin foto y subir, sustituir o quitar una foto JPG, PNG
      o WebP de hasta 5 MB.
- [x] La API rechaza una foto que no cumpla los tipos o el tamaño permitidos.
- [x] Tras guardar y volver a entrar, se conservan los datos, el estado, el horario y la foto del
      local.
- [x] Cancelar o fallar un guardado conserva la versión previamente guardada, incluida la foto. Una
      creación fallida no deja un local visible.
- [x] La web presenta los errores de validación y permite corregir los datos introducidos. Ante un
      fallo de consulta o guardado, muestra el error y permite reintentar.
- [x] El acceso y la gestión de locales se pueden utilizar en móvil y escritorio.
- [x] `npm test` y `npm run build` finalizan correctamente y los artefactos OpenAPI y del cliente
      generado acompañan al cambio de contrato.
- [x] El listado usa la variante A de filas con fotografía y distingue locales activos, inactivos y
      sin foto. Su adaptación a móvil no exige desplazamiento horizontal.
- [x] El formulario de creación sigue la distribución y los controles de fotografía de la versión 2
      elegida, sin incluir controles de comparación ni simulación.
- [x] El horario usa la variante A por grupos: permite seleccionar días, aplicar un tramo o
      cerrarlos y revisar el resumen semanal antes de guardar el local.

## Decisiones

- **Sí:** El administrador registra los locales desde la aplicación. No se precargan los dos locales
  existentes.
- **Sí:** La primera cuenta de evaluación se prepara mediante Development. **No:** crear un registro
  inicial de administrador en el producto o definir aquí su preparación en producción.
- **Sí:** Desactivar y reactivar conserva los datos. **No:** borrar definitivamente locales en esta
  historia.
- **Sí:** Un horario semanal por día, con al menos un día abierto. **No:** pausas, turnos nocturnos
  o excepciones por fecha en esta entrega.
- **Sí:** Foto opcional, con archivos JPG, PNG o WebP de hasta 5 MB en almacenamiento persistente
  del servidor. **No:** un servicio externo de almacenamiento en esta entrega.
- **Sí:** La foto se confirma junto con los demás cambios y su consulta es privada para
  administradores. **No:** una página pública ni acceso público a fotos en H-02.
- **Sí:** La sesión se conserva al recargar hasta cerrar sesión o vencer el token. **No:** limitarla
  al cierre de la pestaña.
- **Sí:** Una interfaz funcional adaptable a móvil y escritorio con el listado A de filas con
  fotografía, el formulario de creación de la versión 2 y el horario A por grupos, elegidos por el
  usuario. **No:** implementar el directorio B, la semana visual B ni los controles de comparación
  del prototipo.
- **Sí:** Usar el prototipo local como referencia de UI. **No:** reutilizar su código desechable,
  sus datos ficticios o sus simulaciones como implementación productiva.

## Riesgos

| Riesgo                                                                            | Mitigación                                                                                                 |
| --------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------- |
| El almacenamiento del servidor se pierde al desplegar o restaurar.                | Configurar almacenamiento persistente fuera del repositorio y respaldarlo junto con los datos del local.   |
| Los datos del local y la foto quedan desincronizados si falla un guardado.        | Confirmar ambos como una operación visible conjunta y probar los fallos de guardado.                       |
| La página pública futura necesite cambios en el acceso a fotos o en los horarios. | Mantener la consulta privada en H-02 y definir la publicación y sus reglas en la historia correspondiente. |
