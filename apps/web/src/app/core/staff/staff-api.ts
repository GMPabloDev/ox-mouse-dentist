import { InjectionToken } from '@angular/core';
import type {
  CreateStaffAccountForm,
  StaffAccountResponse,
  UpdateStaffAccountForm,
} from '@mousedent/api-client';

/** Motivo por el que una operación de personal no se completó. */
export type StaffFailure =
  | { readonly kind: 'validation'; readonly errors: Readonly<Record<string, readonly string[]>> }
  | { readonly kind: 'duplicate-email'; readonly detail?: string }
  | { readonly kind: 'last-administrator'; readonly detail?: string }
  | { readonly kind: 'not-found' }
  | { readonly kind: 'unauthorized' }
  | { readonly kind: 'forbidden' }
  | {
      /**
       * La cuenta se guardó, pero el correo no salió. El identificador permite
       * abrir su detalle y reenviar la invitación sin crear otra cuenta.
       */
      readonly kind: 'invitation-failed';
      readonly accountId?: string;
      readonly detail?: string;
    }
  | { readonly kind: 'concurrent-change'; readonly detail?: string }
  | { readonly kind: 'conflict'; readonly detail?: string }
  | { readonly kind: 'unavailable' };

export type StaffResult<T> =
  { readonly ok: true; readonly value: T } | { readonly ok: false; readonly failure: StaffFailure };

/**
 * Acceso a los endpoints de cuentas del personal. Es el borde entre la web y la
 * API: el adaptador traduce las respuestas HTTP y la pantalla solo ve resultados
 * tipados.
 */
export interface StaffApi {
  list(): Promise<StaffResult<readonly StaffAccountResponse[]>>;
  create(form: CreateStaffAccountForm): Promise<StaffResult<StaffAccountResponse>>;
  update(id: string, form: UpdateStaffAccountForm): Promise<StaffResult<StaffAccountResponse>>;
  setStatus(id: string, isActive: boolean): Promise<StaffResult<StaffAccountResponse>>;
  resendInvitation(id: string): Promise<StaffResult<StaffAccountResponse>>;
}

export const STAFF_API = new InjectionToken<StaffApi>('STAFF_API');
