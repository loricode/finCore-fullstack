import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { catchError, map, of } from 'rxjs';
import { describeError } from '../../core/api/problem';
import { AuthorizedBranch } from '../../core/auth/auth.models';
import { FinCoreApi } from '../../core/api/fincore-api.service';

type State = { status: 'loading' } | { status: 'ok'; data: AuthorizedBranch[] } | { status: 'error'; message: string };

const TONES = ['teal', 'ochre', 'sage', 'blush', 'lavender'] as const;

/** Sucursales que el usuario puede consultar. El filtro lo hace la API (sec.tvf_AuthorizedBranches). */
@Component({
  selector: 'fc-branches-page',
  imports: [MatIconModule],
  template: `
    <header class="fc-page-header fc-enter">
      <div>
        <h1>Sucursales</h1>
        <p>Solo ves las sucursales autorizadas para tu usuario.</p>
      </div>
    </header>

    @switch (state().status) {
      @case ('loading') { <div class="grid">@for (n of [1, 2, 3]; track n) { <div class="card skeleton"></div> }</div> }
      @case ('error') { <div class="fc-alert fc-alert--danger"><mat-icon>error</mat-icon> {{ errorMessage() }}</div> }
      @default {
        <div class="grid">
          @for (b of branches(); track b.branchId; let i = $index) {
            <article class="card fc-enter" [class]="'card card--' + tone(i) + ' fc-enter'" [style.--i]="i + 1">
              <span class="icon"><mat-icon>storefront</mat-icon></span>
              <span class="code fc-mono">{{ b.code }}</span>
              <h2>{{ b.name }}</h2>
              <p><mat-icon>location_on</mat-icon> {{ b.city }}</p>
            </article>
          } @empty {
            <div class="fc-empty"><mat-icon>storefront</mat-icon><p>No tienes sucursales autorizadas.</p></div>
          }
        </div>
      }
    }
  `,
  styles: `
    .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)); gap: 16px; }
    .card {
      position: relative; overflow: hidden; border-radius: 22px; padding: 22px; min-height: 170px;
      display: flex; flex-direction: column; gap: 6px; border: 1px solid var(--fc-border);
      transition: transform 0.25s ease, box-shadow 0.25s ease;
      &:hover { transform: translateY(-3px); box-shadow: var(--fc-shadow-lift); }
      h2 { font-size: 20px; }
      p { margin: auto 0 0; display: flex; align-items: center; gap: 4px; color: var(--fc-text-muted);
          mat-icon { font-size: 18px; width: 18px; height: 18px; } }
    }
    .card--teal { background: var(--fc-gradient-teal); --tone: var(--fc-teal); }
    .card--ochre { background: var(--fc-gradient-ochre); --tone: var(--fc-ochre); }
    .card--sage { background: var(--fc-gradient-sage); --tone: var(--fc-sage); }
    .card--blush { background: var(--fc-gradient-blush); --tone: var(--fc-blush); }
    .card--lavender { background: var(--fc-gradient-lavender); --tone: var(--fc-lavender); }
    .icon { width: 44px; height: 44px; border-radius: 14px; display: grid; place-items: center;
            background: var(--fc-surface); color: var(--tone); box-shadow: var(--fc-shadow); margin-bottom: 6px; }
    .code { position: absolute; top: 22px; right: 22px; font-weight: 800; color: var(--tone); opacity: 0.8; }
    .skeleton { background: var(--fc-linen); animation: pulse 1.4s ease-in-out infinite; }
    @keyframes pulse { 50% { opacity: 0.55; } }
  `,
})
export class BranchesPage {
  protected readonly state = toSignal(
    inject(FinCoreApi).branches().pipe(
      map((data): State => ({ status: 'ok', data })),
      catchError((e) => of<State>({ status: 'error', message: describeError(e).message })),
    ),
    { initialValue: { status: 'loading' } as State },
  );

  protected tone(i: number): string {
    return TONES[i % TONES.length];
  }

  protected branches(): AuthorizedBranch[] {
    const s = this.state();
    return s.status === 'ok' ? s.data : [];
  }

  protected errorMessage(): string {
    const s = this.state();
    return s.status === 'error' ? s.message : '';
  }
}
