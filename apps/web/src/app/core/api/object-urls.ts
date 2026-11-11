import { InjectionToken } from '@angular/core';

/** Crea y libera las URL de objeto con las que la web muestra blobs e imágenes elegidas. */
export interface ObjectUrls {
  create(blob: Blob): string;
  revoke(url: string): void;
}

/**
 * Borde con la API del navegador. Las pruebas no implementan `URL.createObjectURL`,
 * así que la sustituyen por un doble en lugar de depender de jsdom.
 */
export const OBJECT_URLS = new InjectionToken<ObjectUrls>('OBJECT_URLS', {
  providedIn: 'root',
  factory: () => ({
    create: (blob) => URL.createObjectURL(blob),
    revoke: (url) => URL.revokeObjectURL(url),
  }),
});
