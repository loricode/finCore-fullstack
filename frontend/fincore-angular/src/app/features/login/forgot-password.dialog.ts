import { Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';

/** FinCore no envía correos: la contraseña la restablece el administrador (decisión de seguridad del MVP). */
@Component({
  selector: 'fc-forgot-password-dialog',
  imports: [MatDialogModule, MatButtonModule, MatIconModule],
  template: `
    <h2 mat-dialog-title class="fc-dialog-title">
      <span class="fc-dialog-icon"><mat-icon>key</mat-icon></span>
      ¿Olvidaste tu contraseña?
    </h2>
    <mat-dialog-content>
      <p>Tranquilo, le pasa a todos. Por seguridad, la contraseña la restablece el <strong>administrador de tu empresa</strong>.</p>
      <ol>
        <li>Escríbele indicando tu <strong>usuario</strong> (no tu contraseña).</li>
        <li>Él te asignará una contraseña temporal.</li>
        <li>Si tu cuenta quedó bloqueada por intentos fallidos, también puede desbloquearla al instante.</li>
      </ol>
      <p class="fc-muted">Si no recuerdas tu usuario, suele ser <span class="fc-mono">nombre.apellido</span> o <span class="fc-mono">operador.sucursal</span>.</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton="tonal" class="fc-pastel fc-pastel--teal" mat-dialog-close>Entendido</button>
    </mat-dialog-actions>
  `,
  styles: `ol { padding-left: 20px; display: grid; gap: 6px; margin: 0 0 12px; }`,
})
export class ForgotPasswordDialog {}
