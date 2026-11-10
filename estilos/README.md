# MouseDent — Identidad visual «Abrazo»

Versión 1.0 · 10 de noviembre de 2026

## Esencia

**Propósito visual:** que los pacientes se sientan acogidos desde la primera interacción, sin perder la profesionalidad de una clínica odontológica.

- **Personalidad:** profesional, confiable, cercana y humana.
- **Audiencia:** público general y familias.
- **Emociones:** bienestar, calidez, optimismo y alegría.
- **Dirección:** orgánica, amigable, vibrante y optimista.
- **Nombre de la propuesta:** Abrazo.
- **Eslogan propuesto (no aprobado):** «Cuidamos cada sonrisa».

La identidad es compartida por el portal público y el panel del personal; cambia el grado de expresividad, no la marca.

## Paleta principal

| Color | HEX | Rol |
| --- | --- | --- |
| Turquesa | `#137F79` | Marca, CTA primario, enlaces y foco en light |
| Verde agua | `#99DAD0` | Ilustraciones, superficies suaves y decoración |
| Coral | `#EF756A` | Acentos expresivos, nunca texto blanco pequeño sobre este tono |
| Durazno | `#FFE0CA` | Acento cálido y superficies secundarias |
| Marfil | `#FFFAF5` | Fondo light |
| Carbón verdoso | `#2B3837` | Texto principal light |

### Tokens semánticos básicos

| Token | Light | Dark |
| --- | --- | --- |
| `background` | `#FFFAF5` | `#142222` |
| `foreground` | `#2B3837` | `#EAF5F0` |
| `card` | `#FFFFFF` | `#1C2D2B` |
| `primary` | `#137F79` | `#86DFD2` |
| `primary-foreground` | `#FFFFFF` | `#123C39` |
| `secondary` | `#E4F4EF` | `#294440` |
| `secondary-foreground` | `#215651` | `#EAF5F0` |
| `muted` | `#F4EFEA` | `#263936` |
| `muted-foreground` | `#5C6965` | `#B6CFC7` |
| `accent` | `#FFE0CA` | `#46352F` |
| `accent-foreground` | `#543A30` | `#FFE3D3` |
| `destructive` | `#B83B43` | `#F18E87` |
| `destructive-foreground` | `#FFFFFF` | `#3C1B1B` |
| `input` | `#81938B` | `#65877F` |
| `ring` | `#137F79` | `#86DFD2` |

El archivo `mousedent-abrazo.css` contiene los tokens completos para `card`, `popover`, `border`, `input`, `ring`, `chart-*`, `sidebar-*`, estados y colores propios. Se integra con **Tailwind CSS v4 + spartan/ui** y un `components.json` con `tailwind.cssVariables: true`.

> En spartan/ui, `accent` significa superficie de hover/selección; **no equivale al coral de marca**. El coral está disponible como `brand-coral`. Las variantes dark adaptan el turquesa para mantener contrastes.

### Contraste WCAG (comprobaciones de las parejas principales)

- Texto blanco `#FFFFFF` sobre `#137F79`: **4.84:1**.
- Texto `#2B3837` sobre marfil `#FFFAF5`: **11.75:1**.
- Texto `#5C6965` sobre marfil: **5.53:1**.
- Texto `#215651` sobre secundario `#E4F4EF`: **7.35:1**.
- Texto `#543A30` sobre durazno `#FFE0CA`: **8.28:1**.
- Texto `#123C39` sobre turquesa dark `#86DFD2`: **7.80:1**.
- **Evitar** texto blanco pequeño sobre el coral de marca `#EF756A`: su contraste es **2.83:1**. Usar coral como ilustración/detalle o con texto oscuro suficientemente contrastado.

Estos resultados son verificaciones de pares concretos, **no** una auditoría total de accesibilidad de la aplicación. Revisar estados hover, disabled, errores, gráficos, foco y combinaciones personalizadas.

## Tipografía

- **Wordmark y titulares destacados:** **Nunito** 700–800, una tipografía redondeada y amistosa.
- **Interfaz, navegación, tablas y formularios:** **Nunito Sans** 400, 500, 600 y 700 por su legibilidad.
- **Tipografía de reemplazo:** `ui-sans-serif, system-ui, sans-serif`.
- **Escala sugerida:** hero 40–56 px, H1 32–40 px, H2 24–32 px, H3 20–24 px, cuerpo 16 px, tablas 14–15 px, auxiliares 13–14 px.
- **Interlineado:** títulos 1.15–1.25, cuerpo 1.45–1.6.
- Evitar párrafos largos con peso 700 o letras pequeñas de 12 px en tablas administrativas.

Las familias Nunito y Nunito Sans se distribuyen con licencia abierta OFL por Google Fonts.

## Concepto del logotipo

**Composición principal:** mascota abstracta a la izquierda + wordmark «MouseDent». El nombre debe poder leerse con claridad; se puede separar visualmente «Mouse» y «Dent» mediante dos tonos cercanos de turquesa, sin sacrificar legibilidad.

### Mascota abstracta

- **Silueta:** criatura blanda original, ligeramente asimétrica y orgánica; evitar que parezca un diente o un ratón literal.
- **Rostro:** ojos curvos pequeños, sonrisa abierta o leve, mejillas cálidas muy discretas; ojos y boca siempre contrastados.
- **Volumen:** cuerpo verde agua/turquesa, zona de luz menta, acentos coral solo en gestos y mejillas.
- **Movimiento:** expresiones mediante inclinación leve, brazos cortos y gestos simples; evitar movimientos insistentes.
- **Carácter:** empático, expresivo y seguro; **amigable para todas las edades**, no exclusivamente infantil.
- **Variantes:** bienvenida (saludo), cita confirmada (gesto de celebración moderado), tranquilidad (expresión serena), recordatorio (gesto de atención), estado vacío (expresión curiosa, nunca preocupante).

### Variantes del logo

1. **Horizontal:** mascota + MouseDent, uso principal en navbar y documentos.
2. **Apilado:** mascota arriba, nombre debajo, para portada de marca y piezas promocionales.
3. **Isotipo:** silueta simplificada con ojos/sonrisa, para favicon y avatar de aplicación.
4. **Monocromo:** una sola tinta para sellos, documentos y superficies con restricciones.

**Área de protección sugerida:** al menos la mitad del ancho de la mascota en todo el perímetro del lockup. **Tamaños de referencia:** logotipo horizontal ≥ 140 px de ancho; isotipo ≥ 24 px solo si los detalles siguen legibles. Antes del lanzamiento, convertir el diseño final a SVG con trazados y probarlo a 16, 24, 32 y 64 px.

## Reglas de composición y componentes

| Elemento | Regla sugerida |
| --- | --- |
| `Button` primary | Turquesa, texto blanco en light; menta brillante, texto oscuro en dark |
| `Button` secondary | Fondo menta suave, texto verde oscuro |
| `Button` outline | Borde visible y foco distinguible; no solo color |
| `Card` | Superficie blanca en light, carbón verdoso en dark; `rounded-2xl` para tarjetas públicas |
| `Input/Select` | Altura 44 px recomendada, borde claramente identificable, validación textual |
| `Badge` | Colores semánticos para estados, con icono o etiqueta |
| `Calendar` | Fecha seleccionada turquesa; citas con texto + estado, no solo color |
| `Dialog` | `rounded-2xl`, máximo foco en tarea principal, CTA claro |
| `Sidebar` | Neutra, navegación activa con fondo secundario y acento turquesa |
| `Table` | Densidad moderada, texto 14–15 px, líneas divisorias suaves |
| `Toast/Sonner` | Confirmación verde semántico; errores rojos; sin usar coral como error por defecto |
| `Chart` | Series diferenciadas por leyenda, forma y etiquetas, además del color |

**Radii de referencia:** buttons 14 px, cards 20 px, dialogs 24 px, inputs 12 px, badges en píldora. Base `--radius: 0.875rem`.

### Web pública (pacientes)

- Hero con texto acogedor, una fotografía real de personas/instalaciones y mascota como recurso de apoyo.
- CTA «Reservar cita» en turquesa, CTA secundario «Ver tratamientos» con menor énfasis.
- Tarjetas por especialidad con imágenes reales o ilustraciones controladas.
- Proceso de reserva por pasos, con confirmación clara, lenguaje tranquilizador y mascota al finalizar.
- Utilizar coral y durazno en bloques decorativos y momentos de alegría, sin convertir toda la interfaz en colores saturados.

### Panel interno (administración, recepción, profesionales)

- Predominio de fondos neutros, tarjetas y tablas; navegación y foco en turquesa.
- Mascota restringida a bienvenida, onboarding, estados vacíos y comunicaciones, **no** a cada fila de tablas.
- Estados como confirmada, pendiente, reprogramada o cancelada deben incluir etiqueta textual además del color.
- Controles y calendarios con contraste, foco visible y acceso por teclado.
