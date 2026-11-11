# MouseDent

Contexto para agentes y para quien trabaje en este repositorio. El alcance del producto está en
`backlogs/`. La spec aprobada de cada historia manda sobre este documento.

## Stack

- Backend: C# y .NET 10, Minimal APIs, EF Core 10 con SQLite, Identity y JWT bearer.
- Frontend: Angular 22 zoneless con signals, Tailwind CSS 4 y Spartan.
- Contrato: OpenAPI generado en build, Scalar para explorarlo y cliente TypeScript generado.
- Monorepo: npm workspaces para la web y el cliente, solución `.slnx` para .NET.

## Estructura

```text
ox-dentist/
  apps/
    api/
      src/
        MouseDent.Domain/          Entidades, reglas y contratos de repositorio. Sin dependencias.
          Auth/                    Slice de referencia: StaffAccount y StaffRole.
        MouseDent.Application/     Casos de uso y puertos. Depende de Domain.
          Auth/                    AuthService, IStaffAccounts e ITokenIssuer.
        MouseDent.Infrastructure/  Adaptadores. Depende de Application.
          Auth/                    StaffUser, IdentityStaffAccounts y JwtTokenIssuer.
          Persistence/             DbContext, configuraciones, migraciones e inicialización.
        MouseDent.Api/             Endpoints, DTOs, validación y composición.
          Auth/                    Endpoints, contratos y LoginRequestValidator.
          Common/                  OpenAPI, health, rate limit y ValidationFilter.
      tests/
        MouseDent.Api.Tests/       Integración con WebApplicationFactory.
        MouseDent.Application.Tests/ Unitarias con dobles.
      openapi/                     Documento OpenAPI generado en build.
    web/
      src/app/core/                Cliente de API y configuración transversal.
      src/app/features/            Vistas por historia, con carga diferida.
      src/app/ui/                  Componentes de Spartan copiados al proyecto.
      src/app/app.routes.ts        Rutas de producto.
      src/app/app.routes.server.ts Modo de renderizado por ruta.
      proxy.conf.json              Redirección de /api en desarrollo.
  packages/
    api-client/                    Cliente TypeScript generado desde OpenAPI.
  backlogs/                        Historias y decisiones del producto.
```

El backend está separado por capas en proyectos. El frontend está separado por feature en carpetas.
No agregues vistas, rutas, copys ni componentes fuera de una spec.

## Backend

### Capas

| Proyecto                   | Contiene                                                       | Puede referenciar             |
| -------------------------- | -------------------------------------------------------------- | ----------------------------- |
| `MouseDent.Domain`         | Entidades, reglas y contratos de repositorio.                  | Nada.                         |
| `MouseDent.Application`    | Casos de uso y puertos como `IStaffAccounts` e `ITokenIssuer`. | Domain.                       |
| `MouseDent.Infrastructure` | EF Core, Identity, JWT y adaptadores de los puertos.           | Application.                  |
| `MouseDent.Api`            | Endpoints, DTOs, validación, OpenAPI y composición.            | Application e Infrastructure. |

La regla se aplica con referencias de proyecto. Domain no tiene `<ProjectReference>` ni paquetes de
EF Core, Identity o ASP.NET Core. Application solo referencia Domain. Infrastructure referencia
Application. Api referencia Application e Infrastructure para la composición.

Solo `Program.cs` conoce las implementaciones concretas. Los endpoints usan casos de uso de
Application, no tipos de Infrastructure.

### Dónde va cada cosa

| Tipo                                         | Proyecto                                |
| -------------------------------------------- | --------------------------------------- |
| Entidad, value object o regla de negocio     | `MouseDent.Domain/<Feature>/`           |
| Contrato de repositorio                      | `MouseDent.Domain/<Feature>/`           |
| Caso de uso                                  | `MouseDent.Application/<Feature>/`      |
| Puerto de infraestructura                    | `MouseDent.Application/<Feature>/`      |
| Adaptador de EF Core, Identity, JWT o correo | `MouseDent.Infrastructure/<Feature>/`   |
| DbContext, configuraciones y migraciones     | `MouseDent.Infrastructure/Persistence/` |
| Endpoint                                     | `MouseDent.Api/<Feature>/`              |
| DTO de entrada o salida                      | `MouseDent.Api/<Feature>/`              |
| Validador de entrada                         | `MouseDent.Api/<Feature>/`              |
| Composición y registro de dependencias       | `MouseDent.Api/Program.cs`              |

### Cómo agregar una feature

Ejemplo con `Citas`:

1. Crea `Domain/Citas/` con entidades, reglas y contratos de repositorio.
2. Crea `Application/Citas/` con el caso de uso y los puertos que necesite.
3. Crea `Infrastructure/Citas/` con los adaptadores. La persistencia va en
   `Infrastructure/Persistence/`.
4. Crea `Api/Citas/` con endpoints, DTOs y validadores.
5. Registra el caso de uso en `Application/DependencyInjection.cs` y los adaptadores en
   `Infrastructure/DependencyInjection.cs`.
6. Mapea los endpoints en `Program.cs`.
7. Agrega la migración si cambia el modelo.
8. Regenera el cliente si cambia el contrato.

`Auth/` es la implementación de referencia.

### Reglas

- Domain y Application no usan `Microsoft.EntityFrameworkCore`, `Microsoft.AspNetCore.*` ni
  Identity.
- Los endpoints no contienen reglas de negocio. Llaman a un caso de uso.
- La validación de entrada usa FluentValidation y `ValidationFilter<T>`.
- Los errores de la API son `ProblemDetails`.
- No agregues paquetes sin consultar.

## Frontend

- `core/` contiene el cliente de API, la configuración y lo transversal.
- `features/` contiene las vistas por historia, cargadas de forma diferida desde `app.routes.ts`.
- `ui/` contiene los componentes de Spartan copiados con `ng g @spartan-ng/cli:ui <nombre>`.
- Las rutas públicas se declaran en `app.routes.server.ts` con `RenderMode.Prerender` o
  `RenderMode.Server`. El panel usa `RenderMode.Client`.
- El cliente de API se genera desde OpenAPI. No escribas tipos de la API a mano.
- `API_BASE_URL` es absoluto en el navegador y en el servidor. En SSR se toma de `API_ORIGIN`.
- El proxy de desarrollo redirige `/api` a `http://localhost:5080`.

SSR no cambia las carpetas. Una vista pública sigue en `features/`; lo que cambia es su modo en
`app.routes.server.ts`.

## Contrato

1. Cambia el endpoint.
2. Ejecuta `npm run api:client`.
3. Incluye `apps/api/openapi/MouseDent.Api.json` y `packages/api-client/src/generated` en el mismo
   cambio. `packages/api-client/dist` es salida de build y está en `.gitignore`.

El cuerpo de error es `ProblemDetails`. La web lo consume desde el cliente generado.

## Comandos

| Comando                          | Qué hace                                                        |
| -------------------------------- | --------------------------------------------------------------- |
| `npm run dev`                    | Arranca la API con `dotnet watch` y la web con `ng serve`.      |
| `npm run build`                  | Genera OpenAPI, compila el cliente y construye la API y la web. |
| `npm test`                       | Ejecuta las pruebas de .NET y de Angular.                       |
| `npm run format`                 | Aplica `dotnet format` y Prettier.                              |
| `npm run api:client`             | Regenera el cliente TypeScript desde el OpenAPI.                |
| `npm run db:update`              | Aplica las migraciones pendientes.                              |
| `npm run db:migration -- Nombre` | Crea una migración nueva.                                       |

Los comandos `db:*` necesitan que ejecutes `dotnet tool restore` al menos una vez.

## Convenciones

- Español para el dominio y los mensajes al usuario.
- Nombres de roles sin tildes: `Administrador`, `Recepcion` y `Profesional`.
- Fechas en UTC en la API. La web formatea en `es-PE`.
- Pruebas de comportamiento. Integración para endpoints y unitarias para casos de uso.
- Migraciones en `Infrastructure/Persistence/Migrations`.
- `Jwt:SigningKey` solo en Development. En producción se inyecta `Jwt__SigningKey`.
- En Development la API siembra los roles y la cuenta de administración. Las credenciales y su
  configuración están en el README; no las dupliques en otros documentos ni crees administradores
  desde el producto.

## Decisiones

**EF Core 10 con SQLite.** `MouseDentDbContext` vive en Infrastructure y contiene las tablas de
Identity y, más adelante, el modelo de dominio. Cambiar de base implica cambiar `UseSqlite` y el
paquete del proveedor.

**Fechas y agenda.** El instante de una cita se guarda como `DateTime` en UTC, no como
`DateTimeOffset`. El proveedor de SQLite solo traduce igualdad para `DateTimeOffset`, `TimeSpan` y
`ulong`; la comparación y el orden se evalúan en cliente, y la agenda necesita filtrar por rango y
ordenar por inicio. El día se pide con `DateOnly` (`?date=2026-11-10`) y el rango lo calcula la API
en la zona del local, así un equipo con la zona mal configurada no mueve la agenda. Las ventanas de
disponibilidad usan `DayOfWeek` y `TimeOnly`, y los solapes se resuelven en Domain sobre las
ventanas del día, porque `TimeOnly` tampoco tiene traducciones de comparación en SQLite. `Ahora`
sale de `TimeProvider`, ya registrado en Infrastructure, nunca de `DateTime.UtcNow`. No se agrega
NodaTime: solo hace falta con reglas recurrentes, aritmética de calendario o varios países.

**Identity y JWT bearer.** Application define los puertos `IStaffAccounts` e `ITokenIssuer`.
Infrastructure los implementa con Identity y JWT. El alcance por local llega con H-06.

**Validación con FluentValidation.** El DTO es un `record` de C# y `System.Text.Json` lo
materializa. `LoginRequestValidator` agrega las reglas y `ValidationFilter<T>` devuelve
`ValidationProblemDetails`. FluentValidation valida un tipo ya construido; no infiere tipos ni
convierte datos.

**Tipos compartidos con la web.** El contrato OpenAPI genera los tipos de la web. El cuerpo de error
es `ProblemDetails`. La lógica de fechas y solapes pertenece a Domain cuando llegue la agenda.

**OpenAPI en build y Scalar en runtime.** `Microsoft.Extensions.ApiDescription.Server` genera el
documento con el entorno `OpenApiBuild`, que no aplica migraciones ni exige la clave de producción.
Scalar se publica en Development y fuera de Development solo con `Api:EnableDocumentation=true`.

**Cliente generado con `@hey-api/openapi-ts`.** `openapi-typescript` todavía declara TypeScript 5 y
el proyecto usa TypeScript 6.

**SSR selectivo.** Las páginas públicas usan `RenderMode.Prerender` o `RenderMode.Server`. El panel
usa `RenderMode.Client`.

**Tailwind 4 y Spartan.** El tema vive en `apps/web/src/styles.css`. La configuración de Spartan
vive en `apps/web/components.json`. El tema actual es el de Spartan por defecto; la spec de UI
decide el diseño final.

**Fechas en la web.** `date-fns` es la librería de fechas del proyecto y la conversión y el formato
viven en un solo archivo de `core/`, para que las features no importen la librería directamente. El
formato visible es `es-PE` con `timeZone: 'America/Lima'`. La web nunca parsea una fecha sin offset:
`new Date('2026-11-10')` se interpreta como medianoche UTC y corre el día en los bordes de mes.
Temporal no se usa mientras Safari e iOS no lo envíen de forma estable.

## Pendiente

- Rutas y UI de las historias H-01 y H-03 a H-13.
- Canal de verificación de contacto y proveedor de SMS para H-10 y H-13.
- Modelo de autorización por local para H-06.
- Refresh tokens. Hoy el token dura 480 minutos y no se renueva.
- Integración continua. El repositorio no define todavía la plataforma.

## Reglas para agentes

- Lee el backlog y la spec antes de cambiar código.
- No inventes vistas, rutas, copys ni componentes. La UI sale de las specs.
- No rompas la regla de dependencias. Si Domain o Application necesitan algo de afuera, usa un
  puerto.
- No escribas tipos de la API a mano. Regenera el cliente.
- No agregues paquetes sin consultar.
- Ejecuta `npm test` y `npm run build` antes de dar una tarea por terminada.
- Mantén los artefactos generados en el mismo cambio que el contrato.
- No guardes secretos en el repositorio.
