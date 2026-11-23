import { InjectionToken } from '@angular/core';
import type {
  EspecialidadRequest,
  EspecialidadResponse,
  ServicioRequest,
  ServicioResponse,
} from '@mousedent/api-client';

/** Motivo por el que una operación del catálogo no se completó. */
export type CatalogoFailure =
  | { readonly kind: 'validation'; readonly errors: Readonly<Record<string, readonly string[]>> }
  | {
      /**
       * La API rechazó la operación con un `ProblemDetails`: nombre repetido,
       * especialidad con servicios activos, destino inactivo o registro
       * inexistente. El mensaje del servidor explica cuál.
       */
      readonly kind: 'rejected';
      readonly title?: string;
      readonly detail?: string;
    }
  | { readonly kind: 'forbidden' }
  | { readonly kind: 'unavailable' };

export type CatalogoResult<T> =
  | { readonly ok: true; readonly value: T }
  | { readonly ok: false; readonly failure: CatalogoFailure };

/**
 * Acceso a los endpoints del catálogo. Es el borde entre la web y la API: el
 * adaptador traduce las respuestas HTTP y la pantalla solo ve resultados
 * tipados.
 */
export interface CatalogoApi {
  especialidades(): Promise<CatalogoResult<readonly EspecialidadResponse[]>>;
  servicios(): Promise<CatalogoResult<readonly ServicioResponse[]>>;
  createEspecialidad(request: EspecialidadRequest): Promise<CatalogoResult<EspecialidadResponse>>;
  updateEspecialidad(
    id: string,
    request: EspecialidadRequest,
  ): Promise<CatalogoResult<EspecialidadResponse>>;
  createServicio(request: ServicioRequest): Promise<CatalogoResult<ServicioResponse>>;
  updateServicio(id: string, request: ServicioRequest): Promise<CatalogoResult<ServicioResponse>>;
  setEspecialidadStatus(
    id: string,
    isActive: boolean,
  ): Promise<CatalogoResult<EspecialidadResponse>>;
  setServicioStatus(id: string, isActive: boolean): Promise<CatalogoResult<ServicioResponse>>;
}

export const CATALOGO_API = new InjectionToken<CatalogoApi>('CATALOGO_API');
