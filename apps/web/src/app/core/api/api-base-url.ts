import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import { inject, InjectionToken, PLATFORM_ID, type Provider } from '@angular/core';

/**
 * Origen de la API. El cliente generado construye `new Request(url)`, que exige
 * una URL absoluta en el navegador. El proxy de desarrollo y el reverse proxy
 * de producción se encargan de enrutar /api.
 */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL');

export function provideApiBaseUrl(): Provider {
  return {
    provide: API_BASE_URL,
    useFactory: () => {
      const document = inject(DOCUMENT);
      const platformId = inject(PLATFORM_ID);

      return isPlatformBrowser(platformId) ? document.location.origin : '';
    },
  };
}
