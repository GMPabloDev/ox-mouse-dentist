# Backlog v2: Reserva pública y recordatorios

## Contexto

Reserva online de MouseDent para adultos y responsables de menores, con recordatorios reales por correo y SMS. Esta entrega completa el recorrido del MVP sobre la [operación interna del v1](backlog-v1-operacion-interna.md).

Stack: [Stack acordado](README.md#stack).

Decisiones costosas: reserva sin cuenta con verificación de contacto y confirmación sin aprobación de recepción; soporte para responsables de menores; disponibilidad compartida con la operación interna; ficha única del paciente entre locales. Las cuentas de pacientes y la historia clínica quedan fuera de esta entrega.

La evaluación será controlada con pacientes ficticios y contactos reales autorizados, según el [contexto compartido](README.md#evaluación-del-mvp). El canal de verificación de contacto y el proveedor de SMS están pendientes.

Los recordatorios también aplican a citas registradas por recepción. Por eso H-12 y H-13 no dependen de activar la reserva pública ni una de otra.

## Backlog

- **H-10** Como paciente adulto, quiero reservar una cita sin cuenta y verificar mi contacto, para obtener una cita confirmada en el local y horario elegidos.
- **H-11** Como responsable de un menor, quiero reservar una cita a su nombre, para organizar su atención y recibir sus recordatorios como responsable. Requires: H-10
- **H-12** Como paciente o responsable, quiero recibir un recordatorio de la cita por correo electrónico, para recordar cuándo y dónde acudir.
- **H-13** Como paciente o responsable, quiero recibir un recordatorio de la cita por SMS, para recordar cuándo y dónde acudir.
