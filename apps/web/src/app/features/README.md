# Features

Vistas por historia, cargadas de forma diferida desde `app.routes.ts`. Cada spec agrega su carpeta
con su componente y sus rutas.

Las rutas públicas declaran su modo en `app.routes.server.ts` con `RenderMode.Prerender` o
`RenderMode.Server`. El SSR no cambia la ubicación de la vista: una página pública sigue aquí.
