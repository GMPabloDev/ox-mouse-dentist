import type { HttpValidationProblemDetails } from '@mousedent/api-client';

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
 * Título del `ProblemDetails`. Es el texto visible; solo se usa como respaldo
 * cuando la respuesta no trae un código estable.
 */
export function problemTitle(error: unknown): string | undefined {
  return textField(error, 'title');
}

/**
 * Código estable de la extensión `code` del `ProblemDetails`. Los errores de
 * profesionales lo publican para clasificar sin depender del título en español.
 */
export function problemCode(error: unknown): string | undefined {
  return textField(error, 'code');
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

function textField(error: unknown, field: 'detail' | 'title' | 'code'): string | undefined {
  if (typeof error !== 'object' || error === null) {
    return undefined;
  }

  const value = (error as Record<string, unknown>)[field];
  return typeof value === 'string' && value.trim() ? value : undefined;
}
