# SPEC 04 — Profesionales, especialidades y locales de atención

> **Status:** Aprobado
> **Depends on:** SPEC 01, SPEC 02, SPEC 03
> **Date:** 2026-11-22
> **Objective:** Permitir que administración gestione la ficha del profesional, su cuenta de
> acceso, sus especialidades y los locales donde atiende.
> **Historia de origen:** H-03 en backlogs/backlog-v1-operacion-interna.md

## Alcance

**Incluye:**

- Consulta, búsqueda, creación, edición, desactivación y reactivación de profesionales por
  administración autenticada.
- Alta de la ficha con creación de la cuenta de acceso de perfil `Profesional` y envío de la
  invitación por correo, reutilizando el flujo de SPEC 02.
- Vínculo de una cuenta existente con perfil `Profesional` y sin ficha cuando el correo del alta ya
  está registrado.
- Asignación de una o varias especialidades del catálogo de SPEC 03 a cada profesional.
- Asignación de uno o varios locales de atención y extensión de la regla de acceso por local al
  perfil `Profesional`.
- Retiro del perfil `Profesional` del alta y la edición de cuentas en el módulo de personal.
- Interfaz de administración para la lista y la ficha del profesional, incluido el estado de su
  cuenta y el reenvío de la invitación pendiente.

**Fuera de alcance:**

- Disponibilidad y horarios del profesional por local, incluidas ventanas recurrentes, excepciones
  y solapes. Corresponde a H-05.
- Agenda, citas, reprogramación y consulta del profesional sobre sus pacientes. Corresponde a H-01,
  H-08 y H-09.
- Consulta de profesionales por recepción, por otros profesionales o por pacientes. La spec de H-01
  define la consulta que necesita el registro de citas y su filtro por local y especialidad.
- Foto, biografía, firma, datos clínicos o cualquier contenido de historia clínica del profesional.
- Edición de la propia ficha por el profesional.
- Eliminación definitiva de fichas o cuentas.
- Bloqueo de la desactivación de especialidades en uso desde el catálogo. H-04 no cambia.
- Edición de nombre, correo o teléfono de la cuenta desde la ficha, y cualquier duplicación de esos
  datos.

**Nota sobre el backlog:** la línea de H-03 decía solo «profesionales y sus locales de atención». Se
amplía el alcance con las especialidades del profesional por decisión de producto, porque el
catálogo ya existe y la agenda del v2 necesitará filtrar por especialidad. La línea de H-03 y el
contexto del backlog v1 se actualizaron el 2026-11-22 para reflejarlo. El ID y las dependencias no
cambian.

## Comportamiento

### Relación entre profesional y cuenta

- Cada ficha pertenece a exactamente una cuenta de perfil `Profesional`, y cada cuenta tiene como
  máximo una ficha. La relación es uno a uno y obligatoria: no existen profesionales sin cuenta.
- La ficha no guarda nombre, correo ni teléfono propios. Esos datos se leen de la cuenta vinculada y
  se editan únicamente en el módulo de personal. La ficha aporta número de colegiatura,
  especialidades, locales de atención y estado.
- El alta de un profesional pide correo y nombre de la persona y crea la cuenta con perfil
  `Profesional` en estado pendiente, más el envío de la invitación de SPEC 02.
- Si el correo ya pertenece a una cuenta con perfil `Profesional` y sin ficha, el alta vincula esa
  cuenta en lugar de crear una nueva. Se conserva su estado, su contraseña establecida y sus
  sesiones vigentes. Si la cuenta sigue pendiente, la ficha ofrece reenviar la invitación.
- Si el correo pertenece a una cuenta con otro perfil o con una ficha ya vinculada, el alta falla
  con un error comprensible y no crea datos parciales.
- La ficha y la cuenta se guardan en una sola operación. Si el envío de la invitación falla después
  del guardado, la ficha queda creada con la cuenta pendiente y la interfaz informa el fallo; el
  administrador puede reenviar la invitación desde la ficha.
- El fallo del último envío de invitación se guarda en la cuenta y dura hasta que un envío se
  entrega, la cuenta establece su contraseña o cambia su correo. La lista muestra la cuenta
  pendiente con correo fallido de forma distinta a la pendiente normal, y el resumen cuenta los
  correos sin enviar. Registrar el fallo no cambia la versión de sesión de la cuenta.
- Desde la ficha, administración ve el estado de la cuenta (pendiente, activa o inactiva) y puede
  reenviar una invitación pendiente. Cualquier otro cambio de la cuenta se hace en el módulo de
  personal.

### Perfil Profesional en el módulo de personal

- El alta de cuentas en el módulo de personal deja de ofrecer `Profesional`. Solo se crean cuentas
  `Administrador` y `Recepcion`.
- La API rechaza con `ProblemDetails` la creación de una cuenta con perfil `Profesional` y el cambio
  de perfil de una cuenta a `Profesional`, aunque la solicitud se invoque directamente.
- El perfil de una cuenta vinculada a una ficha no se puede cambiar desde el módulo de personal. La
  API rechaza la operación y la interfaz no ofrece el control.
- Las cuentas con perfil `Profesional` que existan sin ficha, por ejemplo por datos de Development,
  no aparecen en la lista de profesionales. Se vinculan al dar de alta un profesional con su correo.

### Datos y reglas de la ficha

- El número de colegiatura es opcional. Después de quitar espacios exteriores admite entre 1 y 30
  caracteres y es único entre todas las fichas, incluidas las inactivas. La comparación ignora
  mayúsculas y espacios exteriores e intermedios, de modo que `COP 12345` y `cop12345` son el mismo
  número.
- Dos profesionales pueden compartir nombre. La interfaz advierte posibles homónimos al crear una
  ficha y permite continuar.
- Cada profesional activo tiene al menos una especialidad activa y al menos un local activo. La
  regla se comprueba al crear, al editar una ficha activa y al reactivar, también frente a
  solicitudes simultáneas. Una edición que deje una ficha activa sin una especialidad activa o sin
  un local activo se rechaza y la ficha queda como estaba.
- No se puede asignar una especialidad inactiva ni añadir un local inactivo nuevo. Las asignaciones
  existentes a una especialidad que después se desactive, o a un local que después se inactive, se
  conservan y la ficha las muestra marcadas como inactivas.
- Un profesional puede editarse mientras sus especialidades o locales estén inactivos. Mientras no
  recupere al menos una especialidad activa y un local activo, no se puede reactivar.
- La desactivación de una ficha conserva sus especialidades, sus locales y su cuenta. La
  reactivación los recupera tal como estaban. No existe borrado definitivo.
- Las operaciones simultáneas no producen dos fichas para la misma cuenta ni dos fichas con el mismo
  número de colegiatura. Una operación rechazada no deja cambios parciales.

### Estados y acceso por local

- El estado de la ficha y el estado de la cuenta son independientes. Desactivar la ficha no
  desactiva la cuenta, y desactivar la cuenta no desactiva la ficha. La ficha muestra ambos estados
  y la lista de profesionales distingue combinaciones, por ejemplo ficha activa con cuenta
  pendiente.
- Un profesional opera en un local solo si su cuenta está activa y con contraseña establecida, su
  ficha está activa, el local está activo y el local figura entre sus locales de atención. La regla
  se incorpora a la comprobación de acceso vigente de SPEC 02 y se aplica en cada solicitud
  autorizada.
- Administración y recepción conservan su regla actual. Un profesional sin ficha activa no opera en
  ningún local.
- Cambiar los locales de atención, desactivar una ficha o reactivarla se aplica en la siguiente
  solicitud autorizada, sin esperar al vencimiento del token.
- Esta spec entrega la regla y sus pruebas. Las operaciones reales de agenda y fichas de pacientes
  las consumen H-01 y H-09.

### Interfaz y errores

- Solo administración autenticada accede a la lista y a la ficha. Recepción y profesionales no ven
  la entrada en el panel ni acceden por ruta directa. La API aplica la misma restricción.
- La lista muestra nombre, especialidades, locales de atención, estado de la ficha y estado de la
  cuenta. Permite buscar por nombre o correo y filtrar por especialidad, local y estado.
- La lista contempla carga, ausencia de profesionales, ausencia de resultados y error con opción de
  reintentar.
- El formulario de alta pide correo y nombre de la cuenta, número de colegiatura, especialidades y
  locales. El formulario de edición muestra nombre, correo y teléfono de solo lectura con un acceso
  al módulo de personal.
- Los selectores de especialidad y de local muestran los registros activos y, en la edición, los
  ya asignados aunque estén inactivos, identificados como tales.
- Los errores de validación, de colegiatura duplicada, de correo ocupado, de reglas de activación,
  de permisos y de envío de invitación se comunican sin presentar la operación como exitosa. La
  interfaz conserva los datos introducidos y permite corregir o reintentar.
- La confirmación de desactivación explica que se conservan especialidades, locales y cuenta.

## Referencia visual

La variante elegida es **A · Tabla y panel lateral** del prototipo local
`prototypes/ox-prototype/profesionales-20261122/index.html`, registrada el 2026-11-22.

La referencia define:

- Una tabla con columnas para profesional, especialidades, locales, estado de la ficha y estado de
  la cuenta. La ficha se abre en un panel lateral de 660 px y la lista queda visible detrás.
- La cuenta pendiente se muestra con un solo estado. Si el correo de invitación falló, el estado usa
  el color de aviso rosa y un icono; una cuenta pendiente normal queda en ámbar. El resumen conserva
  el conteo de correos sin enviar y la ficha ofrece el reenvío.
- La tarjeta «Estado de la ficha» pone la acción de desactivar o reactivar en el encabezado, con
  tamaño compacto. Si faltan requisitos para reactivar, la acción se reemplaza por la caja que
  enumera lo que falta y un acceso a la edición.
- Las especialidades y los locales se muestran como chips en la tabla. Las asignaciones inactivas se
  marcan como tales.
- En pantallas de 1160 px o menos la tabla se convierte en fichas apiladas. El alta y la edición se
  abren en el mismo panel lateral.

Se compararon una vista de tarjetas y una de lista con detalle. Se descartan para la implementación:
la primera separa la lista de la ficha y la segunda comparte el ancho con la lista y se aleja del
patrón de tabla y panel que ya usa Personal.

El prototipo usa datos, sesiones y respuestas simuladas. Sus cambios no persisten y su código es
descartable, no apto para producción. La carpeta `prototypes/` está ignorada por Git mediante la
regla `/prototypes/`, así que esta referencia es local y no se recupera desde el repositorio.

### Decisión visual

- **Pregunta:** ¿cómo se presentan la lista y la ficha de profesional?
- **Objetivo:** gestionar entre seis y ocho profesionales que tienen una o varias especialidades y
  uno o varios locales, con dos estados independientes (ficha y cuenta).
- **Restricciones conocidas:** debe funcionar en móvil y escritorio; debe mostrar las especialidades
  y los locales asignados en la lista; debe distinguir especialidades y locales inactivos; debe
  exponer el estado de la cuenta y el reenvío de la invitación pendiente; debe permitir crear en un
  solo formulario.
- **Decisión:** tabla y panel lateral. Se prefiere la lectura por columnas y el patrón de Personal; la
  vista de lista y detalle se descarta para la implementación porque comparte el ancho con la lista
  y se aleja del patrón de Personal.
- **Siguiente paso:** implementar la interfaz con esta referencia en el Bloque 5.

## Plan de implementación

### Bloque 1: modelo de la ficha

1. Crear el modelo del profesional con su cuenta vinculada, su número de colegiatura normalizado y
   su estado, respetando las dependencias entre capas. La persistencia de especialidades y locales
   se resuelve como relaciones, no como texto.
2. Incorporar la persistencia y una migración. Ajustar la normalización del número de colegiatura y
   su unicidad para que resista solicitudes simultáneas.
3. Comprobar con pruebas de comportamiento la creación, la edición, la desactivación, la
   reactivación, la unicidad de la colegiatura, los límites de longitud y la conservación de
   asignaciones inactivas.

### Bloque 2: alta con cuenta e invitación

1. Implementar el caso de uso de alta que crea la cuenta pendiente, la ficha y la invitación en una
   sola operación, reutilizando el servicio de cuentas y el puerto de correo de SPEC 02.
2. Implementar el vínculo a una cuenta existente con perfil `Profesional` y sin ficha, incluido el
   rechazo cuando el correo pertenece a otro perfil o a una ficha ya vinculada.
3. Implementar el reenvío de la invitación pendiente desde la ficha y el estado de la cuenta en la
   lista, incluido el fallo guardado del último envío.
4. Comprobar con pruebas de integración el fallo de envío sin datos parciales, el alta concurrente
   con el mismo correo y el vínculo a cuentas pendientes y a cuentas con contraseña establecida.

### Bloque 3: personal sin perfil Profesional

1. Retirar `Profesional` del alta de cuentas y bloquear en la API la creación con ese perfil, el
   cambio de perfil hacia `Profesional` y el cambio de perfil de una cuenta con ficha.
2. Ajustar la interfaz de personal para no ofrecer el perfil ni habilitar su edición en cuentas
   vinculadas, e informar el motivo.
3. Comprobar con pruebas de integración los rechazos y que las cuentas Profesional existentes siguen
   autenticándose.

### Bloque 4: acceso por local y contrato

1. Extender la regla de acceso vigente para el perfil `Profesional` según los estados acordados de
   cuenta, ficha y local.
2. Comprobar la regla con pruebas de comportamiento, incluidas la desactivación de la ficha, el
   cambio de locales y la desactivación del local asignado.
3. Exponer las operaciones de administración con validación y errores `ProblemDetails`, y ejecutar
   `npm run api:client` incluyendo el OpenAPI y el cliente generado en el mismo cambio.
4. Comprobar con pruebas de integración la autorización por perfil, la exclusión de recepción y
   profesionales, y los casos de error del alta y la edición.

### Bloque 5: interfaz

1. Implementar la lista con la tabla, el panel lateral y la transformación móvil de la referencia A,
   con búsqueda, filtros y estados de carga, vacío, sin resultados y error, con carga diferida y el
   cliente generado.
2. Implementar el alta y la edición con los selectores de especialidad y local, los estados de la
   cuenta y el reenvío de la invitación.
3. Comprobar los recorridos de alta con invitación, vínculo de una cuenta existente, corrección de
   errores, desactivación y reactivación, y confirmar que recepción y profesionales no acceden por
   ruta directa.

### Bloque 6: verificación final

1. Ejecutar `npm test` y `npm run build`.
2. Recorrer el alta completa con el SMTP de evaluación configurado fuera del repositorio y
   comprobar la recepción de la invitación, el reenvío y el primer acceso del profesional.
3. Confirmar con datos persistidos que la regla de acceso por local deja disponible para H-01 y
   H-09 sin adelantar sus endpoints ni sus pantallas.

## Criterios de aceptación

- [ ] Administración autenticada puede listar, buscar, crear, editar, desactivar y reactivar
      profesionales. Los datos persisten tras recargar.
- [ ] Toda ficha tiene una cuenta de perfil `Profesional` y toda cuenta tiene como máximo una ficha.
      La API impide ambos incumplimientos.
- [ ] El alta crea la cuenta pendiente y envía la invitación con el flujo de SPEC 02. Si el envío
      falla, la ficha queda creada, la interfaz informa el fallo, la lista conserva la marca de
      correo fallido y el reenvío funciona.
- [ ] Un alta con un correo de una cuenta `Profesional` sin ficha vincula esa cuenta. Un correo de
      otro perfil o de una cuenta con ficha se rechaza sin datos parciales.
- [ ] El módulo de personal no ofrece el perfil `Profesional`, y la API rechaza crearlo, asignarlo o
      retirarlo de una cuenta con ficha.
- [ ] La ficha no duplica nombre, correo ni teléfono. Esos datos se editan en personal y la ficha
      los muestra actualizados.
- [ ] El número de colegiatura es opcional, admite 1 a 30 caracteres tras quitar espacios y es
      único entre todas las fichas, incluso inactivas y con diferencias de mayúsculas o espacios.
- [ ] Se pueden registrar dos profesionales con el mismo nombre y la interfaz advierte posibles
      homónimos sin bloquear.
- [ ] Crear, editar una ficha activa o reactivar exige al menos una especialidad activa y al
      menos un local activo. Especialidades y locales inactivos solo pueden conservarse, no
      añadirse.
- [ ] Una especialidad desactivada en el catálogo permanece asignada y visible como inactiva. Un
      profesional que se queda sin especialidad activa no se puede reactivar.
- [ ] Un profesional inactivo opera en ningún local; al reactivarlo recupera sus especialidades y
      locales. La cuenta y la ficha cambian de estado de forma independiente.
- [ ] La comprobación de acceso vigente permite operar al profesional solo en locales activos
      asignados, con cuenta activa y ficha activa. Los cambios se aplican en la siguiente solicitud.
- [ ] Recepción y profesionales no acceden a la lista ni a la ficha, por la web ni por la API.
- [ ] La lista distingue carga, vacío, sin resultados y error, busca por nombre o correo y filtra por
      especialidad, local y estado.
- [ ] Las operaciones simultáneas no crean fichas duplicadas ni números de colegiatura repetidos.
- [x] La variante de prototipo se elige y se registra en esta spec antes de implementar la interfaz.
- [ ] El OpenAPI y el cliente generado están actualizados. `npm test` y `npm run build` terminan
      correctamente.

## Decisiones

- **Cuenta obligatoria y uno a uno.** Se descarta el profesional sin cuenta y el profesional con
  varias cuentas. H-09 necesita una identidad única para su agenda y una ficha sin cuenta dejaría
  casos sin resolver.
- **Un solo origen por dato.** Nombre, correo y teléfono viven en la cuenta; colegiatura,
  especialidades, locales y estado viven en la ficha. Se descarta duplicar los datos de contacto:
  dos fuentes producen contradicciones y reglas de sesión repetidas.
- **El alta crea la cuenta.** Se descarta exigir dos pasos (crear la cuenta en personal y después
  vincularla), porque el recorrido real de administración es dar de alta a la persona una vez.
- **El fallo de la invitación se guarda en la cuenta.** Se descarta dejarlo solo en la respuesta del
  alta: la lista tiene que distinguir la cuenta pendiente con correo fallido después de recargar y
  el resumen cuenta los correos sin enviar. Vive en la cuenta porque la invitación es de la cuenta,
  no de la ficha, y no altera la versión de sesión.
- **Perfil `Profesional` fuera del módulo de personal.** Se descarta mantener el perfil como opción
  del alta: crearía cuentas sin ficha que nadie puede usar y dos caminos para el mismo dato. Las
  cuentas Profesional heredadas de datos de prueba se vinculan por correo al dar de alta.
- **Especialidades en esta spec.** Se descarta posponerlas a H-05: la disponibilidad no es el lugar
  para decidir qué atiende cada persona, y la reserva del v2 necesitará filtrar por especialidad.
- **Varias especialidades, al menos una activa.** Se descarta la especialidad única obligatoria,
  porque obligaría a inventar una especialidad por defecto para profesionales que rotan.
- **Colegiatura opcional y única.** Se descarta exigirla: no todos los profesionales del equipo
  pueden tenerla registrada al iniciar la evaluación. Se descarta hacer único el nombre: dos
  odontólogos reales pueden llamarse igual.
- **Los locales de atención determinan el acceso.** Se descarta dejarlos como dato informativo.
  `LocalAccess` ya anticipa esta regla y H-01 y H-09 la necesitan; posponerla obligaría a reabrir el
  modelo en dos historias.
- **Estados independientes entre ficha y cuenta.** Se descarta que desactivar la ficha desactive la
  cuenta. Son decisiones distintas: disponibilidad para agenda frente a permiso de acceso.
- **Especialidad inactiva conservada, activación condicionada.** Se descarta bloquear la
  desactivación en el catálogo: H-04 no gana reglas y la ficha se corrige donde se ve el problema.
- **La edición de una ficha activa mantiene el mínimo.** Se descarta permitir que una ficha activa
  quede sin especialidad activa ni local activo: ese estado contradice la regla de activación y
  obligaría a inventar un comportamiento para la agenda. Para vaciarla se desactiva primero. Una
  especialidad o un local que se desactive después en el catálogo o en el local se sigue
  conservando como inactivo.
- **Sin foto.** Se descarta por ahora: ninguna vista del v1 la muestra y la reserva del v2 todavía
  no está especificada.
- **Solo administración consulta profesionales.** Se descarta adelantar la consulta de H-01. Sus
  filtros por local y especialidad dependen de la agenda y de las citas.
- **Desactivación en lugar de borrado.** Coherente con locales, cuentas y catálogo, y evita perder
  referencias que las citas usarán después.
- **Datos de Development descartables.** La base de desarrollo puede recrearse, así que la migración
  no conserva cuentas Profesional huérfanas. La regla del proyecto vive en `AGENTS.md`, sección
  «Datos de Development», y no forma parte del plan de implementación de esta spec.
- **Tabla y panel lateral en la referencia visual.** Se elige la variante A del prototipo local
  `prototypes/ox-prototype/profesionales-20261122/index.html`. La lista se lee por columnas, la ficha
  se abre en un panel lateral de 660 px y en pantallas angostas la tabla se convierte en fichas. La
  cuenta pendiente con correo fallido se muestra en rosa; la cuenta pendiente normal, en ámbar. La
  acción de estado de la ficha vive en el encabezado de su tarjeta. Se descartan las tarjetas y la lista con
  detalle para la implementación. La referencia es local y no se recupera desde Git.
- **Definición con aclaraciones detalladas.** Las decisiones anteriores se acordaron en la sesión de
  diseño de esta spec, no por definición rápida.

## Riesgos

| Riesgo                                                  | Mitigación                                                                                                                       |
| ------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------- |
| El envío SMTP falla durante el alta                     | La ficha y la cuenta pendiente se guardan primero. La interfaz informa el fallo y la ficha permite reenviar.                       |
| Existen cuentas Profesional de prueba con contraseña    | El alta por correo las vincula conservando su estado. Si estorban, se recrea la base de Development.                             |
| Una especialidad se desactiva y el profesional queda inactivable | La lista y la ficha marcan la especialidad inactiva; administración asigna una activa antes de reactivar.                |
| La regla de acceso cambia el comportamiento de cuentas Profesional existentes | Se cubre con pruebas de comportamiento y con la nota de que H-09 reutiliza la regla, no la redefine.                  |
| Ficha y cuenta se desincronizan                         | Una dirección de escritura por campo y pruebas de integración de ambos módulos.                                                  |
| Dos administradores dan de alta al mismo profesional    | Unicidad de correo y de colegiatura en la base, con pruebas concurrentes.                                                        |
