# UI

Componentes de Spartan copiados al proyecto. Genera cada componente desde `apps/web` con:

```bash
ng g @spartan-ng/cli:ui <nombre>
```

El generador crea la carpeta del componente y actualiza `components.json` y los alias de
`tsconfig.json`.

## Ajustes a la referencia

Los componentes generados son código del proyecto. Los de esta carpeta llevan las medidas de la
referencia visual de `specs/01-gestion-locales.md`: botones de 44 px de alto con radio 10 px,
entradas de 48 px con radio 9 px, insignias de radio 7 px y el select de 40 px de alto. Regenerar
un componente con el CLI devuelve los valores por defecto de Spartan y pierde esos ajustes.

Los iconos de los componentes generados son SVG propios. El CLI añade `@ng-icons/core` y
`@ng-icons/lucide` como dependencias; el proyecto no los usa, así que se quitan de `package.json`
tras generar un componente que los importe.
