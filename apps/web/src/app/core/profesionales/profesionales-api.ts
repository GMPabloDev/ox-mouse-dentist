import { InjectionToken } from '@angular/core';
import type {
  CreateProfesionalForm,
  ProfesionalResponse,
  UpdateProfesionalForm,
} from '@mousedent/api-client';

/**
 * Motivo por el que una operación de profesionales no se completó. Los rechazos
 * que comparten el estado 409 se separan porque el formulario corrige un campo
 * distinto en cada caso.
 */
export type ProfesionalFailure =
  | { readonly kind: 'validation'; readonly errors: Readonly<Record<string, readonly string[]>> }
  /** Otra ficha, incluso inactiva, ya usa ese número de colegiatura. */
  | { readonly kind: 'duplicate-colegiatura'; readonly detail?: string }
  /** El correo pertenece a una cuenta que ya tiene ficha. */
  | { readonly kind: 'email-taken'; readonly detail?: string }
  /** El correo pertenece a una cuenta de administración o recepción. */
  | { readonly kind: 'other-profile'; readonly detail?: string }
  /** La ficha activa no recupera una especialidad y un local activos. */
  | { readonly kind: 'activation-incomplete'; readonly detail?: string }
  | { readonly kind: 'not-found' }
  | { readonly kind: 'unauthorized' }
  | { readonly kind: 'forbidden' }
  | {
      /**
       * La ficha y la cuenta quedaron guardadas, pero el correo no salió. El
       * identificador permite abrir la ficha y reenviar la invitación.
       */
      readonly kind: 'invitation-failed';
      readonly profesionalId?: string;
      readonly detail?: string;
    }
  /** Otro conflicto con explicación del servidor: alta simultánea, cuenta inactiva, etc. */
  | { readonly kind: 'conflict'; readonly detail?: string }
  | { readonly kind: 'unavailable' };

export type ProfesionalResult<T> =
  | { readonly ok: true; readonly value: T }
  | { readonly ok: false; readonly failure: ProfesionalFailure };

/**
 * Acceso a los endpoints de profesionales. Es el borde entre la web y la API: el
 * adaptador traduce las respuestas HTTP y la pantalla solo ve resultados tipados.
 */
export interface ProfesionalesApi {
  list(): Promise<ProfesionalResult<readonly ProfesionalResponse[]>>;
  create(form: CreateProfesionalForm): Promise<ProfesionalResult<ProfesionalResponse>>;
  update(id: string, form: UpdateProfesionalForm): Promise<ProfesionalResult<ProfesionalResponse>>;
  setStatus(id: string, isActive: boolean): Promise<ProfesionalResult<ProfesionalResponse>>;
  resendInvitation(id: string): Promise<ProfesionalResult<ProfesionalResponse>>;
}

export const PROFESIONALES_API = new InjectionToken<ProfesionalesApi>('PROFESIONALES_API');
