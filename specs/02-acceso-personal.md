# SPEC 02 — Acceso del personal por perfil y local

> **Status:** Borrador
> **Depends on:** SPEC 01
> **Date:** 2026-11-11
> **Objective:** Permitir que administración gestione las cuentas del personal y el acceso de recepción a sus locales asignados.
> **Historia de origen:** H-06 en backlogs/backlog-v1-operacion-interna.md

## Alcance

**Incluye:**

- Consulta, búsqueda, creación, edición, desactivación y reactivación de cuentas del personal por administración.
- Un perfil por cuenta: `Administrador`, `Recepcion` o `Profesional`.
- Asignación de locales a recepción y una regla de autorización por local reutilizable por las historias posteriores.
- Invitación y recuperación de contraseña por correo SMTP, con enlaces de un solo uso.
- Aplicación de las revocaciones de acceso a sesiones existentes.
- Una pantalla de bienvenida y cierre de sesión para perfiles que todavía no tienen módulos de trabajo.
- Interfaz para gestionar cuentas, invitaciones y recuperación de acceso. Su diseño visual se elegirá después mediante un prototipo.

**Fuera de alcance:**

- Creación de la primera cuenta de administración desde el producto. El equipo técnico la prepara según SPEC 01.
- Eliminación definitiva de cuentas y datos asociados.
- Gestión de profesionales, vínculo de una cuenta con un profesional y locales de atención. Corresponden a H-03. El acceso del profesional a su agenda y pacientes corresponde a H-09.
- Citas, agendas y fichas de pacientes. Esta spec no concede acceso a módulos todavía no implementados.
- Cambio de contraseña desde el panel para una persona autenticada. Queda como mejora futura sin historia asignada. La recuperación por correo sí está incluida.
- Proveedor de correo distinto del SMTP configurado para la evaluación, registro público de cuentas y cuentas de pacientes.
- Refresh tokens y las variantes visuales que no se elijan en el prototipo posterior.

## Comportamiento

### Cuentas y perfiles

- Solo una cuenta de administración autenticada puede consultar o gestionar las cuentas del personal. La API aplica esta regla también a las solicitudes directas.
- El administrador ve una lista con nombre, correo, perfil y estado. Puede buscar por nombre o correo y distinguir cuentas pendientes, activas e inactivas. La lista contempla los estados de carga, sin resultados y error con opción de reintentar.
- Una cuenta tiene nombre visible y correo obligatorios, teléfono opcional y exactamente un perfil. El correo identifica la cuenta y es único, incluidas las cuentas inactivas y pendientes. La API valida los datos y la web muestra los errores sin perder lo introducido.
- Administración accede a todos los locales por su perfil, sin asignaciones individuales. Recepción accede solo a sus locales asignados y activos. Un profesional puede tener cuenta aunque todavía no exista la entidad profesional; esta entrega no asigna sus locales de atención.
- Para crear una cuenta de recepción, el administrador selecciona uno o más locales activos. No puede añadir una asignación nueva a un local inactivo.
- Una cuenta de recepción conserva su asignación cuando se desactiva un local. El local inactivo no autoriza operaciones. Si ningún local asignado está activo, la cuenta puede iniciar sesión, pero no tiene acceso a ningún local. La reactivación del local restaura el acceso de las cuentas que conservan la asignación.
- Si una cuenta quedó solo con asignaciones a locales inactivos, el administrador puede editar otros datos sin asignar otro local. La interfaz distingue los locales inactivos y permite ajustar las asignaciones.
- El administrador puede desactivar y reactivar cuentas sin borrar sus datos. Una cuenta inactiva no inicia sesión ni puede usar una sesión anterior. Reactivar una cuenta que ya estableció contraseña permite iniciar una sesión nueva con esa contraseña; no restaura sesiones anteriores.
- Al desactivar una cuenta se invalidan sus enlaces pendientes. Si se reactiva una cuenta que todavía no estableció contraseña, el administrador puede enviarle una invitación nueva.
- Un administrador puede crear y gestionar otras cuentas de administración y modificar su propia cuenta. El sistema impide cualquier operación que deje sin un administrador activo que ya haya establecido su contraseña. Una invitación pendiente no cuenta. Esta protección incluye la desactivación, el cambio de perfil y el cambio de correo del último administrador disponible. La protección se mantiene ante solicitudes concurrentes.

### Invitación y correo

- Al crear una cuenta, el sistema guarda sus datos y envía una invitación a su correo. La cuenta queda pendiente hasta que la persona establezca su contraseña. Mientras esté pendiente, no puede iniciar sesión.
- La invitación contiene un enlace de un solo uso válido durante 24 horas. Al abrirlo, la persona establece su contraseña con la política de Identity configurada en el proyecto. El formulario muestra los requisitos y los errores de validación.
- Si el envío falla, la cuenta permanece pendiente. El administrador ve que el envío falló y puede reenviar la invitación sin crear una cuenta duplicada. La web no presenta el envío como exitoso si falla.
- El administrador puede reenviar una invitación pendiente, por ejemplo cuando vence. Al emitir un enlace nuevo para la cuenta, los anteriores dejan de ser válidos, incluso si el nuevo envío falla.
- El administrador puede cambiar el correo de una cuenta, salvo que deje sin administrador disponible. El cambio invalida las sesiones y enlaces anteriores, guarda el correo nuevo y deja la cuenta pendiente. Se envía un enlace al correo nuevo para establecer contraseña. Si el envío falla, la cuenta sigue pendiente y el administrador puede reenviarlo.
- El envío usa SMTP con Gmail. El responsable configura manualmente las variables de entorno, las credenciales y la dirección pública necesaria para construir los enlaces. No se guardan secretos en el repositorio. La configuración admite el entorno de generación de OpenAPI sin exigir secretos de producción.

### Recuperación y sesiones

- Desde la pantalla de acceso, una persona puede solicitar un enlace de recuperación por correo. La respuesta es genérica y no confirma si existe una cuenta. El sistema no envía enlaces utilizables a cuentas inactivas.
- El enlace de recuperación es de un solo uso y dura 24 horas. Al emitir un enlace nuevo para la cuenta, cualquier enlace anterior de invitación o recuperación deja de ser válido. Se aplican las mismas reglas de contraseña que en la invitación.
- Solicitar recuperación no cierra las sesiones existentes. Al completar el cambio de contraseña, las sesiones anteriores dejan de servir en la siguiente solicitud.
- Los enlaces usados, vencidos o inválidos no permiten cambiar la contraseña. La pantalla muestra un mensaje genérico y orienta a solicitar recuperación o contactar al administrador para una nueva invitación, sin revelar información interna del sistema.
- Desactivar una cuenta, cambiar su correo o cambiar su perfil invalida sus sesiones en la siguiente solicitud. Tras un cambio de perfil, la persona inicia sesión de nuevo. La asignación o retirada de locales y la activación o desactivación de un local se aplican en la siguiente solicitud autorizada, sin esperar al vencimiento del token.
- La API comprueba el estado y los permisos vigentes, no solo los permisos presentes en un token anterior. Las solicitudes sin autenticación o sin autorización reciben el rechazo correspondiente. La web gestiona el cierre de sesión y no presenta datos ni acciones como autorizados después de una revocación.
- Después de iniciar sesión, administración accede a sus módulos disponibles. Recepción y profesionales ven una bienvenida con su identidad y una opción para cerrar sesión hasta que existan sus módulos. La pantalla de acceso denegado no se limita al caso de gestión de locales.

### Fallos y protección

- Los errores de conexión y servidor no se presentan como operaciones exitosas. La interfaz conserva los datos editados cuando es posible y permite reintentar.
- Las respuestas públicas de acceso y recuperación no revelan si existe una cuenta. Las respuestas y registros no exponen contraseñas, tokens, credenciales SMTP ni enlaces completos.
- Las operaciones públicas de envío de enlaces tienen limitación de solicitudes para reducir abuso. Las pruebas comprueban el límite configurado y que no revela la existencia de una cuenta.
- Las reglas de acceso por local se comprueban mediante pruebas de comportamiento de la API o de la política integrada. Las operaciones reales de recepción se implementarán y verificarán en H-01, no en esta spec.

## Referencia visual

Estilo a seguir: [Opción A, tabla y panel lateral](../prototypes/ox-prototype/acceso-personal-20261111-144919/index.html?v=a&screen=team), incluidos los recorridos de invitación, recuperación y bienvenida.

El prototipo usa datos y comportamientos simulados. `prototypes/` está ignorado por Git; esta referencia es local y no se recupera desde el repositorio.

## Plan de implementación

### Bloque 1: cuentas y autorización vigente

1. Extender el modelo persistente para el estado de cuenta y las asignaciones de recepción a locales. Incorporar una migración que conserve el administrador existente.
2. Implementar los casos de uso de gestión y la protección del último administrador disponible, incluida la concurrencia. Respetar las dependencias entre Domain, Application e Infrastructure.
3. Implementar la comprobación de sesión, perfil y asignaciones vigentes. Comprobar cambios y revocaciones con pruebas de comportamiento, incluidos locales inactivos.
4. Publicar las operaciones para administración con validación y errores `ProblemDetails`. Comprobar autenticación, autorización y casos de error mediante pruebas de integración.

### Bloque 2: invitaciones y recuperación

1. Incorporar el envío SMTP detrás de un puerto de Application y configurar el adaptador en Infrastructure, sin guardar secretos. Consultar antes de añadir cualquier paquete.
2. Implementar emisión, caducidad, invalidación y consumo de enlaces de invitación y recuperación. Integrar el cambio de correo y la invalidación de sesiones.
3. Comprobar el contenido y destino del correo con un adaptador de prueba, los fallos de envío, el reenvío, los enlaces inválidos y la protección de respuestas públicas. La prueba automatizada no sustituye la comprobación manual del SMTP configurado.

### Bloque 3: contrato y web

1. Regenerar el documento OpenAPI y el cliente TypeScript con `npm run api:client`. Incluir los artefactos generados en el mismo cambio.
2. Implementar las pantallas y estados de gestión, invitación, recuperación y bienvenida según el prototipo que se vincule antes de aprobar la spec. Mantener la carga diferida por feature y usar el cliente generado.
3. Comprobar las rutas por perfil, la búsqueda, la edición y los errores visibles. Verificar que la interfaz reacciona a una sesión invalidada.

### Bloque 4: verificación final

1. Ejecutar `npm test` y `npm run build`.
2. Con el SMTP de evaluación configurado fuera del repositorio, comprobar el envío real de invitación y recuperación a un correo autorizado, la caducidad y el uso único de los enlaces.
3. Verificar que la asignación por local queda disponible para H-01 sin adelantar sus endpoints ni sus pantallas.

## Criterios de aceptación

- [ ] Solo administración autenticada puede listar, buscar, crear, editar, desactivar y reactivar cuentas por la API y la web.
- [ ] La búsqueda encuentra cuentas por nombre o correo y la lista distingue pendientes, activas e inactivas.
- [ ] Cada cuenta tiene un perfil, correo único, nombre obligatorio y teléfono opcional. La validación impide guardar datos inválidos.
- [ ] Una cuenta nueva de recepción requiere al menos un local activo. No se puede añadir una asignación nueva a un local inactivo.
- [ ] Una cuenta que conserva solo locales inactivos puede iniciar sesión y editarse, pero no obtiene acceso a esos locales. Reactivar un local restaura el acceso de sus cuentas asignadas.
- [ ] Administración tiene acceso a todos los locales por su perfil. Un profesional puede recibir una cuenta sin requerir una entidad profesional ni asignación de locales de atención.
- [ ] Desactivar una cuenta impide acceder en la siguiente solicitud e invalida sus enlaces. Reactivarla no restaura sesiones anteriores y conserva la contraseña ya establecida.
- [ ] Ninguna operación, incluidas las concurrentes, deja el sistema sin un administrador activo que ya haya establecido su contraseña.
- [ ] El administrador puede invitar al personal y reenviar una invitación pendiente. Si el correo falla, la cuenta se conserva pendiente y el fallo se comunica.
- [ ] Una invitación permite establecer contraseña una sola vez durante 24 horas. Un enlace nuevo invalida los anteriores, también cuando falla el envío nuevo.
- [ ] Cambiar el correo invalida sesiones y enlaces anteriores, deja la cuenta pendiente y envía una invitación al correo nuevo. El fallo permite reenviarla.
- [ ] La recuperación no revela si existe la cuenta. El enlace vence a las 24 horas, se usa una vez y al establecer contraseña invalida las sesiones anteriores. Solicitarlo no invalida sesiones.
- [ ] Los enlaces inválidos, usados y vencidos se rechazan con un mensaje genérico y una orientación sin detalles internos.
- [ ] El perfil cambiado invalida la sesión en la siguiente solicitud. Los cambios de asignación o de estado del local se reflejan en la siguiente solicitud autorizada.
- [ ] Las pruebas cubren el acceso por perfil y local con solicitudes autorizadas y rechazadas. La comprobación no depende de implementar H-01.
- [ ] Recepción y profesionales pueden iniciar sesión y llegar a una bienvenida con su identidad y cierre de sesión. No se muestran módulos futuros como disponibles.
- [ ] Las pantallas siguen la variante de prototipo vinculada a esta spec antes de su aprobación y contemplan carga, ausencia de resultados y errores recuperables.
- [ ] El correo real de invitación y recuperación llega a un destinatario autorizado mediante el SMTP configurado. No hay secretos ni enlaces completos en el repositorio o los registros.
- [ ] El contrato OpenAPI y el cliente generado están actualizados. `npm test` y `npm run build` terminan correctamente.

## Decisiones

- **Un perfil por cuenta.** Se descarta combinar perfiles para mantener explícitos los permisos de cada persona.
- **Administración global y recepción por local.** Administración no necesita asignaciones por local. H-06 define el alcance operativo de recepción; no sustituye los locales de atención de H-03.
- **Cuenta profesional sin vínculo.** H-03 definirá el vínculo con el profesional. H-09 definirá el acceso a su agenda y sus pacientes.
- **Desactivación en lugar de borrado.** Se conservan los datos y se puede reactivar la cuenta.
- **Permisos vigentes en la siguiente solicitud.** Se descarta esperar el vencimiento del JWT para revocar permisos. El cambio de perfil exige una nueva sesión.
- **Invitación por correo con enlace.** Se descarta enviar contraseñas por correo o entregarlas manualmente. El usuario configura Gmail por SMTP.
- **Conservar cuentas cuando falla el correo.** Se permite reenviar, en vez de borrar la cuenta y repetir el alta.
- **Protección del último administrador disponible.** Una cuenta pendiente no garantiza acceso administrativo. El sistema bloquea acciones que dejen sin acceso a administración.
- **Locales inactivos conservados.** Se descarta bloquear la desactivación del local o desactivar las cuentas afectadas. Las asignaciones se conservan sin conceder acceso mientras el local esté inactivo.
- **Recuperación por correo.** La solicitud no revela si existe la cuenta y solo el cambio efectivo de contraseña invalida las sesiones. El cambio desde el panel queda para una mejora futura.
- **Diseño posterior mediante prototipo.** La spec define comportamiento y estados. El usuario elegirá y vinculará el diseño antes de aprobarla.

## Riesgos

| Riesgo | Mitigación |
| --- | --- |
| Una sesión conserva permisos revocados en el JWT. | Comprobar el estado y los permisos vigentes en cada solicitud protegida y probar la revocación con tokens ya emitidos. |
| El correo no llega o la configuración SMTP falla. | Conservar la cuenta pendiente, informar al administrador y permitir reenvío. Comprobar el envío real en el entorno de evaluación. |
| Dos administradores modifican sus cuentas a la vez y dejan el sistema sin acceso. | Proteger la regla del último administrador frente a concurrencia y cubrirla con pruebas. |
| El alcance de profesionales se confunde con las asignaciones de recepción. | Dejar explícito que H-03 define los locales de atención y H-09 el acceso a agenda y pacientes. |
| No hay todavía operaciones de recepción para probar el alcance completo. | Probar la regla integrada en H-06 y verificar su uso en endpoints reales durante H-01. |
