import { Injectable, signal } from '@angular/core';

/** Aviso breve que la lista muestra después de guardar un local. */
@Injectable({ providedIn: 'root' })
export class LocalesNotice {
  private readonly current = signal<string | null>(null);

  set(message: string): void {
    this.current.set(message);
  }

  /** Devuelve el aviso pendiente y lo descarta para que no reaparezca. */
  take(): string | null {
    const message = this.current();
    this.current.set(null);

    return message;
  }
}
