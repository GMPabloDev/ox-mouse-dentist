import type { HttpValidationProblemDetails, ProblemDetails } from '@mousedent/api-client';

/**
 * Lectura de las respuestas de error de la API. El cuerpo siempre es
 * `ProblemDetails`; las escrituras inválidas llegan como
 * `HttpValidationProblemDetails` con los mensajes por campo.
 */

/** Mensaje legible de un `ProblemDetails`, si el servidor lo envió. */
export function problemDetail(error: unknown): string | undefined {
  return textField(error, 'detail');
}

/**
 * Título del `ProblemDetails`. La API no publica códigos de error estables, así
 * que el título es lo único que distingue dos conflictos con el mismo estado.
 */
export function problemTitle(error: unknown): string | undefined {
  return textField(error, 'title');
}

/** Errores por campo de un `ValidationProblemDetails`, sin perder los mensajes. */
export function validationErrors(error: unknown): Readonly<Record<string, readonly string[]>> {
  if (typeof error !== 'object' || error === null) {
    return {};
  }

  const errors = (error as HttpValidationProblemDetails).errors;
  if (typeof errors !== 'object' || errors === null) {
    return {};
  }

  return Object.fromEntries(
    Object.entries(errors).map(([field, messages]) => [
      field,
      Array.isArray(messages) ? messages.filter((message) => typeof message === 'string') : [],
    ]),
  );
}

function textField(error: unknown, field: 'detail' | 'title'): string | undefined {
  if (typeof error !== 'object' || error === null) {
    return undefined;
  }

  const value = (error as ProblemDetails)[field];
  return typeof value === 'string' && value.trim() ? value : undefined;
}
