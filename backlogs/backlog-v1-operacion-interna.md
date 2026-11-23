# Backlog v1: Operación interna

## Contexto

Panel interno de MouseDent para que administración, recepción y profesionales organicen la atención en sus locales. Es la primera entrega del MVP y precede a la reserva pública y los recordatorios del v2.

Stack: [Stack acordado](README.md#stack).

Decisiones costosas: una sola empresa con múltiples locales; ficha básica única del paciente; administración central; recepción limitada a sus locales asignados; profesionales con acceso de consulta a su agenda y fichas básicas de sus pacientes. La historia clínica queda fuera de esta entrega.

La evaluación será controlada con el equipo y pacientes ficticios, según el [contexto compartido](README.md).

El equipo técnico prepara la primera cuenta de administrador. La primera historia incluye el acceso necesario para que el administrador gestione los locales. Después, el administrador gestiona el acceso del personal por perfil y local antes de que recepción registre y consulte citas. Los datos necesarios para las citas que no estén cubiertos por historias anteriores se definen en la spec de H-01. Las especialidades de cada profesional forman parte de H-03, porque la agenda pública del v2 necesita filtrar por ellas.

## Backlog

- **H-02** Como administrador, quiero gestionar los locales de MouseDent, para mantener organizada la atención en las sedes actuales y las nuevas.
- **H-06** Como administrador, quiero gestionar el acceso del personal por perfil y local, para que cada trabajador opere dentro de sus responsabilidades. Requires: H-02
- **H-04** Como administrador, quiero gestionar las especialidades, servicios y tratamientos de MouseDent, para mantener actualizada la oferta de atención.
- **H-03** Como administrador, quiero gestionar los profesionales, sus especialidades y sus locales de atención, para mantener actualizado el equipo disponible.
- **H-05** Como administrador, quiero definir la disponibilidad de los profesionales por local, para ofrecer horarios que puedan atender. Requires: H-03
- **H-14** Como recepcionista, quiero registrar la ficha básica única de un paciente y su responsable cuando sea menor, para identificarlo al atenderlo en los locales de MouseDent. Requires: H-06
- **H-07** Como recepcionista, quiero consultar y actualizar la ficha básica única del paciente y su responsable cuando sea menor, para mantener sus datos al atenderlo en distintos locales. Requires: H-14
- **H-01** Como recepcionista, quiero registrar una cita y consultarla en la agenda de un local asignado, para organizar la atención del paciente. Requires: H-02 H-06 H-14
- **H-08** Como recepcionista, quiero reprogramar o cancelar citas de mis locales asignados, para mantener la agenda actualizada ante cambios. Requires: H-01
- **H-09** Como profesional, quiero consultar mi propia agenda y las fichas básicas de mis pacientes, para preparar su atención. Requires: H-01
