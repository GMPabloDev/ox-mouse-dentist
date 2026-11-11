import { InjectionToken } from '@angular/core';
import type { CreateLocalForm, LocalResponse, UpdateLocalForm } from '@mousedent/api-client';

/** Indicación explícita sobre la foto guardada al editar un local. */
export type PhotoAction = 'keep' | 'replace' | 'remove';

/** Motivo por el que una operación de locales no se completó. */
export type LocalesFailure =
  | { readonly kind: 'validation'; readonly errors: Readonly<Record<string, readonly string[]>> }
  | { readonly kind: 'duplicate'; readonly detail?: string }
  | { readonly kind: 'not-found' }
  | { readonly kind: 'unavailable' };

export type LocalesResult<T> =
  | { readonly ok: true; readonly value: T }
  | { readonly ok: false; readonly failure: LocalesFailure };

/**
 * Acceso a los endpoints de locales. Es el borde entre la web y la API: el
 * adaptador traduce las respuestas HTTP y el resto de la aplicación solo ve
 * resultados tipados.
 */
export interface LocalesApi {
  list(): Promise<LocalesResult<readonly LocalResponse[]>>;
  create(form: CreateLocalForm): Promise<LocalesResult<LocalResponse>>;
  update(id: string, form: UpdateLocalForm): Promise<LocalesResult<LocalResponse>>;
  setStatus(id: string, isActive: boolean): Promise<LocalesResult<LocalResponse>>;
  photo(id: string): Promise<LocalesResult<Blob>>;
}

export const LOCALES_API = new InjectionToken<LocalesApi>('LOCALES_API');
