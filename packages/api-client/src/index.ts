import { client } from './generated/client.gen';

export * from './generated';
export { client };

export interface ApiClientOptions {
  /**
   * Origen de la API. En el navegador queda vacío para usar el mismo origen
   * (proxy en desarrollo, reverse proxy en producción). En SSR debe ser absoluto.
   */
  baseUrl: string;
  /**
   * Devuelve el token actual o null. Se evalúa en cada petición.
   * No uses esta opción en rutas con SSR: el cliente es un singleton del proceso.
   */
  accessToken?: () => string | null;
}

/**
 * Configura el cliente generado. Las funciones del SDK lo usan por defecto,
 * así que basta importarlas y llamarlas.
 */
export function configureApiClient({ baseUrl, accessToken }: ApiClientOptions): void {
  client.setConfig({
    baseUrl,
    auth: accessToken ? () => accessToken() ?? undefined : undefined,
  });
}
