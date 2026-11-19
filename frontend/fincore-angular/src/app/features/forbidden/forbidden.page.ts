import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'fc-forbidden-page',
  imports: [RouterLink],
  template: `
    <section class="fc-card box">
      <h1>Sin permiso</h1>
      <p class="fc-muted">Tu perfil no tiene acceso a esta sección. Si lo necesitas, solicítalo al administrador.</p>
      <a class="fc-btn" routerLink="/inicio">Volver al inicio</a>
    </section>
  `,
  styles: `.box { max-width: 480px; display: grid; gap: 12px; justify-items: start; }`,
})
export class ForbiddenPage {}
