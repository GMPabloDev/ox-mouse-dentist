# MouseDent

Monorepo de MouseDent: API en .NET 10 y web en Angular 22. La web renderiza en el servidor las
páginas públicas y deja el panel del personal en el navegador. El frontend queda sin rutas de
producto; cada spec agrega las suyas y declara su modo de renderizado.

La arquitectura, las capas y las convenciones para agentes están en [AGENTS.md](AGENTS.md).

## Requisitos

- .NET SDK 10.0.113. `global.json` lo fija y permite bandas de características posteriores.
- Node 22.22.3 o posterior, o Node 24.15.0 o posterior, o Node 26 o posterior. Angular 22 no acepta
  Node 23 ni 25.
- npm 11.

## Puesta en marcha

1. Instala las dependencias de JavaScript y las herramientas de .NET:

   ```bash
   npm install
   dotnet tool restore
   ```

2. Arranca la API y la web:

   ```bash
   npm run dev
   ```

3. Abre la web en <http://localhost:4200>. La aplicación arranca sin rutas de producto.

La API aplica las migraciones y crea la cuenta de desarrollo al arrancar. La base SQLite queda en
`apps/api/src/MouseDent.Api/mousedent.db`.

| Recurso              | URL                                   |
| -------------------- | ------------------------------------- |
| Web                  | <http://localhost:4200>                 |
| API                  | <http://localhost:5080>                 |
| Documentación Scalar | <http://localhost:5080/scalar/v1>       |
| Documento OpenAPI    | <http://localhost:5080/openapi/v1.json> |
| Salud de la API      | <http://localhost:5080/health>          |

Cuenta de desarrollo: `admin@mousedent.local` con la contraseña `Admin123!`. El sembrado solo ocurre
en Development, cuando `Seed:Enabled` es true. La cuenta sirve para probar los endpoints de
autenticación; la pantalla de acceso llega en su spec.

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

## Estructura

```text
apps/api/       API .NET con las capas Domain, Application, Infrastructure y Api.
apps/web/       Angular con core, features y ui.
packages/       Cliente TypeScript generado desde OpenAPI.
backlogs/       Historias y decisiones del producto.
```

El detalle de cada capa, la regla de dependencias y las convenciones están en
[AGENTS.md](AGENTS.md).

## Contrato de la API

El documento OpenAPI es la fuente del cliente. Después de cambiar un endpoint:

```bash
npm run api:client
```

El comando compila la API, escribe `apps/api/openapi/MouseDent.Api.json`, regenera
`packages/api-client/src/generated` y compila el paquete. Incluye los artefactos generados en el
mismo cambio que el endpoint.

## Despliegue

Construye y arranca el servidor SSR:

```bash
npm run build
PORT=4000 API_ORIGIN=http://localhost:5080 node apps/web/dist/web/server/server.mjs
```

Variables de entorno:

| Variable                     | Uso                                                                |
| ---------------------------- | ------------------------------------------------------------------ |
| `Jwt__SigningKey`            | Clave HMAC de al menos 32 bytes. Obligatoria fuera de Development. |
| `ConnectionStrings__Default` | Cadena de conexión. Por defecto `Data Source=mousedent.db`.        |
| `API_ORIGIN`                 | Origen absoluto de la API para las peticiones del SSR.             |
| `NG_ALLOWED_HOSTS`           | Hosts permitidos por el servidor SSR. Separa varios con comas.     |

La aplicación no arranca fuera de Development si `Jwt:SigningKey` conserva el valor de desarrollo.
