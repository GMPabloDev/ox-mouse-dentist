# Backlogs de MouseDent

## Contexto

MouseDent es una clínica dental peruana con dos locales y un tercero previsto. Atiende a pacientes de todas las edades y cuenta con aproximadamente seis a ocho profesionales, con variaciones en el tiempo.

El MVP es una web para que los pacientes reserven citas y el personal organice la atención entre locales. El cliente evaluará el recorrido completo antes de decidir si paga por el sistema. La base debe permitir continuar el producto después de la evaluación y reutilizar el código para otro cliente.

## Stack

- Backend con C# y .NET 10, con EF Core 10 como ORM.
- Frontend con Angular moderno, Signals, Tailwind CSS y [Spartan](https://spartan.ng/).
- SQLite para el MVP. PostgreSQL o SQL Server son posibles destinos futuros, no una migración comprometida.
- S3 si se necesita almacenar archivos.
- Correo mediante Gmail por SMTP, usando inicialmente la cuenta del desarrollador para la demostración.
- SMS mediante un proveedor pendiente de elegir. Twilio y Bird son candidatos, no decisiones tomadas.

Se acordó trabajar con buenas prácticas y repositorios. Esto no garantiza por sí solo la portabilidad entre bases de datos. Los demás paquetes y detalles de implementación se concretarán en las specs.

El setup del monorepo, los paquetes del contrato OpenAPI y la estructura de proyectos están documentados en el [README raíz](../README.md).

## Decisiones de producto

- El MVP atiende solo a MouseDent, con múltiples locales desde el inicio. No incluye otras empresas independientes.
- Administración accede a todos los locales y gestiona locales, profesionales, oferta de atención, disponibilidad y accesos del personal.
- Recepción gestiona las citas únicamente en sus locales asignados. Cada persona puede tener uno o varios locales asignados.
- Los profesionales consultan su propia agenda y las fichas básicas de sus pacientes. No crean, reprograman ni cancelan citas en este MVP.
- Cada paciente tiene una ficha básica única compartida entre locales. Compartir la ficha no implica acceso universal del personal.
- La reserva pública no exige crear una cuenta. La persona verifica un medio de contacto antes de obtener una cita confirmada, sin aprobación posterior de recepción.
- Un adulto puede reservar para sí mismo o para un menor a su cargo. El paciente y la persona responsable pueden ser distintos.
- Los recordatorios por correo y SMS son envíos reales y también aplican a las citas registradas por recepción.
- La historia clínica queda para una entrega posterior. Las fichas de este MVP son básicas, no registros de atención clínica.
- Las cuentas de pacientes quedan para una decisión posterior. No se ha acordado que sean obligatorias en el futuro.
- El mecanismo concreto de autorización, como RBAC, ABAC o una combinación, no está decidido. Las reglas de acceso anteriores son los requisitos acordados.

## Evaluación del MVP

La primera evaluación será controlada con el desarrollador y el equipo de MouseDent. Se usarán pacientes ficticios y correos y teléfonos reales de participantes autorizados, sin simular los envíos. El uso con pacientes reales no forma parte de esta primera evaluación.

El cliente conoce la posibilidad de gastos por servicios, pero quiere evaluar una demostración funcional antes de comprometer más dinero. La disponibilidad de créditos gratuitos para SMS no está garantizada; proveedor y coste siguen pendientes.

La ventana disponible es de dos a tres semanas. No se ha verificado que todo el alcance quepa en ese plazo.

## Entregas acordadas

1. [Backlog v1: Operación interna](backlog-v1-operacion-interna.md). Permite al personal organizar citas y atención entre locales.
2. [Backlog v2: Reserva pública y recordatorios](backlog-v2-reserva-publica-recordatorios.md). Completa la reserva online y los recordatorios reales sobre la operación interna.

Ambos backlogs forman parte del MVP inicial. La versión de un backlog no equivale por sí sola a todo el MVP. La historia clínica no se incluye en estas dos entregas.

## Registro de IDs

| Backlog | IDs asignados | Estado |
| --- | --- | --- |
| v1: Operación interna | H-01 a H-09 | Aprobados |
| v2: Reserva pública y recordatorios | H-10 a H-13 | Aprobados |

El siguiente ID disponible es H-14. Los IDs se conservan al reordenar historias y no se reutilizan al retirarlas o sustituirlas. Las historias sustituidas se conservan como registro y sus IDs se anotan como retirados en el backlog correspondiente. Actualmente no hay IDs retirados.

Las referencias `Requires:` solo apuntan a historias del mismo backlog. La relación entre v1 y v2 se expresa en sus contextos.

## Desarrollo de specs

Para iniciar una historia, usa `/skill:ox-to-spec` e indica la ruta del backlog y el ID. Por ejemplo:

```text
/skill:ox-to-spec backlogs/backlog-v1-operacion-interna.md H-01
```
