import {
  applyEach,
  maxLength,
  required,
  requiredError,
  schema,
  validate,
  type SchemaPath,
} from '@angular/forms/signals';
import { dayProblem, scheduleProblem } from './schedule-model';
import type { LocalDraft } from './local-form';

/**
 * Marca el campo como obligatorio y exige texto después de recortar: `required`
 * por sí solo considera vacío únicamente `''`, así que una cadena de espacios
 * pasaría la comprobación.
 */
function requiredText(path: SchemaPath<string>, message: string): void {
  required(path, { message });
  validate(path, ({ value }) =>
    value() && !value().trim() ? requiredError({ message }) : undefined,
  );
}

/**
 * Reglas del formulario de local declaradas para Signal Forms. Los mensajes son
 * los mismos que devuelve la API para que la corrección sea equivalente.
 */
export const localFormSchema = schema<LocalDraft>((path) => {
  requiredText(path.name, 'El nombre es obligatorio.');
  maxLength(path.name, 120, { message: 'El nombre no puede superar los 120 caracteres.' });

  requiredText(path.address, 'La dirección es obligatoria.');
  maxLength(path.address, 200, { message: 'La dirección no puede superar los 200 caracteres.' });

  requiredText(path.phone, 'El teléfono es obligatorio.');
  maxLength(path.phone, 40, { message: 'El teléfono no puede superar los 40 caracteres.' });

  validate(path.schedule, ({ value }) => {
    const week = value();
    const weekProblem = scheduleProblem(week);

    if (weekProblem) {
      return { kind: 'schedule', message: weekProblem };
    }

    return week.some((day) => dayProblem(day))
      ? { kind: 'schedule', message: 'Revisa los días abiertos del horario.' }
      : undefined;
  });

  applyEach(path.schedule, (day) =>
    validate(day, ({ value }) => {
      const problem = dayProblem(value());

      return problem ? { kind: 'day', message: problem } : undefined;
    }),
  );
});
