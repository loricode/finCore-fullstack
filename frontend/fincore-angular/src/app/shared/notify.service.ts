import { Injectable, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { describeError } from '../core/api/problem';

/** Mensajes breves al usuario, con los colores de la paleta (salvia para éxito, rosa para error). */
@Injectable({ providedIn: 'root' })
export class Notify {
  private readonly snack = inject(MatSnackBar);

  success(message: string): void {
    this.snack.open(message, 'Cerrar', { panelClass: 'fc-snack-success' });
  }

  /** Aviso sin error de la API (validación en el navegador). */
  warn(message: string): void {
    this.snack.open(message, 'Cerrar', { panelClass: 'fc-snack-error', duration: 5000 });
  }

  error(error: unknown): void {
    const { message, correlationId } = describeError(error);
    this.snack.open(correlationId ? `${message} (ref. ${correlationId.slice(0, 8)})` : message, 'Cerrar', {
      panelClass: 'fc-snack-error',
      duration: 6000,
    });
  }
}
