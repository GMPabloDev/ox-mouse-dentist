# Backlog v1: Operación interna

## Contexto

Panel interno de MouseDent para que administración, recepción y profesionales organicen la atención en sus locales. Es la primera entrega del MVP y precede a la reserva pública y los recordatorios del v2.

Stack: [Stack acordado](README.md#stack).

Decisiones costosas: una sola empresa con múltiples locales; ficha básica única del paciente; administración central; recepción limitada a sus locales asignados; profesionales con acceso de consulta a su agenda y fichas básicas de sus pacientes. La historia clínica queda fuera de esta entrega.

La evaluación será controlada con el equipo y pacientes ficticios, según el [contexto compartido](README.md).

H-01 incluye el acceso autorizado y los datos mínimos necesarios para registrar y consultar una cita real. Las historias posteriores amplían la gestión autónoma de la operación; la primera entrega no depende de completar antes gestores separados para cada dato.

## Backlog

- **H-01** Como recepcionista, quiero registrar una cita y consultarla en la agenda de un local asignado, para organizar la atención del paciente.
- **H-02** Como administrador, quiero gestionar los locales de MouseDent, para mantener organizada la atención en las sedes actuales y las nuevas.
- **H-03** Como administrador, quiero gestionar los profesionales y sus locales de atención, para mantener actualizado el equipo disponible.
- **H-04** Como administrador, quiero gestionar las especialidades, servicios y tratamientos de MouseDent, para mantener actualizada la oferta de atención.
- **H-05** Como administrador, quiero definir la disponibilidad de los profesionales por local, para ofrecer horarios que puedan atender. Requires: H-03
- **H-06** Como administrador, quiero gestionar el acceso del personal por perfil y local, para que cada trabajador opere dentro de sus responsabilidades.
- **H-07** Como recepcionista, quiero consultar y actualizar la ficha básica única del paciente y su responsable cuando sea menor, para mantener sus datos al atenderlo en distintos locales.
- **H-08** Como recepcionista, quiero reprogramar o cancelar citas de mis locales asignados, para mantener la agenda actualizada ante cambios. Requires: H-01
- **H-09** Como profesional, quiero consultar mi propia agenda y las fichas básicas de mis pacientes, para preparar su atención. Requires: H-01
