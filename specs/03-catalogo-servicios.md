# SPEC 03 — Catálogo de especialidades y servicios

> **Status:** Implementada **Depends on:** SPEC 01, SPEC 02 **Date:** 2026-11-19 **Objective:** Permitir
> que administración gestione un catálogo global de especialidades y servicios, y que el personal
> consulte la oferta activa. **Historia de origen:** H-04 en
> backlogs/backlog-v1-operacion-interna.md

## Alcance

**Incluye:**

- Consulta, creación, edición, desactivación y reactivación de especialidades y servicios por
  administración.
- Un catálogo común a todos los locales, persistido en la aplicación.
- Consulta de especialidades y servicios activos por recepción y profesionales, sin permisos de
  modificación.
- Listados con búsqueda, filtros y estados de carga, resultados vacíos y error.
- Interfaz para los tres perfiles, con el prototipo de catálogo agrupado como referencia visual y de
  interacción.

**Fuera de alcance:**

- Registro de tratamientos clínicos de pacientes, piezas dentales, sesiones y avance. Requiere una
  historia y una spec posteriores, todavía no asignadas. La historia clínica está fuera del backlog
  v1.
- Agenda y citas, incluida la selección de un servicio en una cita. H-01 definirá los datos
  necesarios para las citas.
- Asignación de especialidades o servicios a profesionales o locales, disponibilidad y diferencias
  entre locales.
- Precios, duración, imágenes y publicación del catálogo para visitantes.
- Eliminación definitiva de especialidades o servicios.

## Comportamiento

### Conceptos y datos

- Una especialidad agrupa servicios. Cada servicio pertenece a una especialidad.
- Una especialidad tiene nombre obligatorio y descripción opcional. Un servicio tiene nombre
  obligatorio, descripción opcional y especialidad obligatoria.
- El nombre, después de quitar espacios al principio y al final, tiene entre 1 y 100 caracteres. La
  descripción es opcional y admite hasta 500 caracteres después de quitar esos espacios.
- El nombre de una especialidad es único en todo el catálogo. El nombre de un servicio es único
  dentro de su especialidad. Ambas reglas incluyen los registros inactivos e ignoran mayúsculas y
  espacios exteriores.
- El catálogo representa prestaciones disponibles, llamadas «servicios» en la interfaz. No
  representa el tratamiento clínico indicado a una persona. Un tratamiento futuro podrá relacionarse
  con varias prestaciones sin que esta spec implemente esa relación.

### Gestión y estados

- Solo administración autenticada puede crear, editar, desactivar o reactivar especialidades y
  servicios. La API aplica la misma restricción a las solicitudes directas.
- El catálogo empieza vacío. Administración puede crear especialidades y servicios, consultar
  activos e inactivos y distinguir sus estados.
- Administración puede cambiar la especialidad de un servicio a otra activa, siempre que el nombre
  no esté ocupado en el destino, incluidos sus servicios inactivos.
- Una especialidad con servicios activos no se puede desactivar. La interfaz explica que primero
  deben desactivarse esos servicios. Desactivar una especialidad no borra sus servicios inactivos.
- Para crear o activar un servicio, o moverlo a otra especialidad, la especialidad de destino debe
  estar activa. La API rechaza la operación si no se cumple la regla.
- La desactivación conserva los datos y la reactivación vuelve a poner el registro activo. Reactivar
  una especialidad no reactiva automáticamente sus servicios.
- Las reglas de estado y unicidad se aplican también ante solicitudes simultáneas. Una operación
  rechazada no deja cambios parciales.

### Consulta y acceso

- Administración consulta especialidades y servicios activos e inactivos. Puede buscar por nombre y
  filtrar por estado. En los servicios también puede filtrar por especialidad.
- Recepción y profesionales consultan solo especialidades y servicios activos, con búsqueda por
  nombre y filtro por especialidad. Su vista es de solo lectura y no muestra controles de gestión.
- La API no entrega registros inactivos a recepción ni a profesionales y rechaza sus intentos de
  modificación. Las solicitudes anónimas no acceden al catálogo.
- El acceso sigue los mecanismos de autenticación y autorización existentes. El catálogo no depende
  de la asignación del personal a un local.

### Interfaz y errores

- La interfaz distingue carga, catálogo vacío, ausencia de resultados y error de consulta. Ante un
  error de consulta, permite reintentar.
- Los errores de validación, duplicidad, estado y permisos se comunican sin presentar la operación
  como exitosa. Ante errores de guardado, la interfaz conserva los datos introducidos cuando sea
  posible y permite corregir o reintentar.
- La referencia elegida es la vista única agrupada por especialidad de
  `prototypes/ox-prototype/catalogo-agrupado-20261120/index.html`. Incluye creación de servicios
  dentro de cada especialidad, filtros estables y un panel lateral para consultar y gestionar los
  registros. Recepción y profesionales usan la vista de consulta sin controles de gestión.
- El prototipo usa datos y respuestas simulados. Sus cambios no persisten y su código es
  descartable, no apto para producción. La carpeta `prototypes/` está ignorada por Git, por lo que
  esta referencia local no se recupera desde el repositorio. Quedan detalles de diseño por mejorar
  que aún no se han definido.

## Plan de implementación

### Bloque 1: catálogo y persistencia

1. Modelar especialidades y servicios con las reglas de datos, unicidad y estado acordadas,
   respetando las dependencias entre capas.
2. Incorporar la persistencia y una migración que conserve los datos existentes. Garantizar las
   reglas frente a solicitudes simultáneas.
3. Comprobar con pruebas de comportamiento la creación, edición, cambio de especialidad, estados,
   duplicados y rechazo de operaciones inválidas.

### Bloque 2: API y autorización

1. Exponer las operaciones de gestión y consulta con validación y errores `ProblemDetails`.
2. Comprobar mediante pruebas de integración el acceso de cada perfil, la exclusión de registros
   inactivos para recepción y profesionales, los filtros y los errores.
3. Ejecutar `npm run api:client` e incluir el OpenAPI y el cliente TypeScript generados en el mismo
   cambio del contrato.

### Bloque 3: interfaz

1. Tomar la vista agrupada elegida como referencia para los recorridos de administración y de
   consulta.
2. Implementar las vistas de gestión y consulta con carga diferida y el cliente generado. Respetar
   los permisos y estados acordados.
3. Comprobar búsqueda, filtros, cambios de estado, formularios, estados vacíos y errores para los
   perfiles correspondientes.

### Bloque 4: verificación final

1. Verificar los recorridos completos de administración, recepción y profesionales con datos
   persistidos.
2. Ejecutar `npm test` y `npm run build`.

## Criterios de aceptación

- [x] Administración autenticada puede crear, consultar, editar, desactivar y reactivar
      especialidades y servicios. Los datos persisten tras recargar.
- [x] Especialidades y servicios aceptan nombres de 1 a 100 caracteres y descripciones opcionales de
      hasta 500, tras quitar espacios exteriores. Se rechazan nombres vacíos y datos fuera de esos
      límites.
- [x] No se pueden guardar especialidades con nombres duplicados ni servicios con nombres duplicados
      dentro de la misma especialidad, incluidos los registros inactivos y las diferencias de
      mayúsculas o espacios exteriores. El mismo nombre de servicio puede existir en especialidades
      distintas.
- [x] Administración puede cambiar un servicio a una especialidad activa si no hay un nombre
      duplicado en el destino. Un rechazo no modifica el servicio.
- [x] No se puede desactivar una especialidad con servicios activos. La interfaz explica la
      condición y permite corregirla.
- [x] No se puede crear ni activar un servicio en una especialidad inactiva, ni mover un servicio a
      ella. Reactivar una especialidad no reactiva sus servicios.
- [x] Las operaciones simultáneas no producen nombres duplicados ni servicios activos bajo
      especialidades inactivas.
- [x] Administración puede buscar por nombre, filtrar por estado y filtrar servicios por
      especialidad. Los listados distinguen activos e inactivos.
- [x] Recepción y profesionales pueden consultar únicamente especialidades y servicios activos,
      buscar por nombre y filtrar por especialidad. No ven controles de modificación.
- [x] La API rechaza las solicitudes anónimas y los intentos de modificación de recepción o
      profesionales. Tampoco devuelve registros inactivos a esos perfiles.
- [x] Las vistas muestran carga, catálogo vacío, ausencia de resultados y errores. Un error de
      consulta permite reintentar y un fallo de guardado no se presenta como éxito.
- [x] La variante de prototipo se elige y se incorpora a esta spec antes de implementar la interfaz.
      Las pantallas implementadas siguen esa referencia.
- [x] El OpenAPI y el cliente generado están actualizados. `npm test` y `npm run build` terminan
      correctamente.

## Decisiones

- **Catálogo de servicios por especialidad.** Se descarta una jerarquía de tres catálogos de
  especialidades, servicios y tratamientos. El tratamiento clínico es trabajo indicado a un paciente
  y se definirá por separado.
- **Catálogo global.** Se descarta asignar servicios a locales o configurar datos por local en esta
  entrega.
- **Datos básicos.** Se guardan nombre y descripción opcional, además de la especialidad de cada
  servicio. Se descartan precio y duración por ahora.
- **Desactivación en lugar de borrado.** Los registros se conservan y pueden reactivarse. Esto evita
  perder referencias que puedan necesitar funcionalidades posteriores.
- **Bloqueo de especialidades con servicios activos.** Se descarta desactivar sus servicios
  automáticamente. Administración decide cuáles desactivar antes de retirar la especialidad.
- **Consulta para el personal.** Recepción y profesionales ven la oferta activa. Se descarta limitar
  la consulta a administración, pues no se identificó una razón para ocultar el catálogo al
  personal. Solo administración puede modificarlo.
- **Vista agrupada.** Se elige el prototipo de catálogo agrupado por especialidad como referencia
  para la interfaz. El código del prototipo no se incorpora a producción.
- **Entrada al catálogo.** Recepción y profesionales comienzan en el catálogo al iniciar sesión,
  porque es el primer módulo disponible para ellos. Se descarta llevarles a la bienvenida, que
  anunciaba la falta de módulos. Una ruta compartida del panel sustituirá este destino en una
  historia posterior; hasta entonces la bienvenida deja de ser su pantalla inicial.
- **Panel según el perfil.** Recepción y profesionales ven en el panel solo el catálogo: se omiten
  la etiqueta de administración y los enlaces a locales y personal, que exigen permisos de
  administración y terminaban en la pantalla de acceso restringido. Se descarta mantener la
  navegación completa para esos perfiles con los destinos deshabilitados.
