import { Component, inject, signal, viewChild } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';
import { AuthService } from '../../core/auth/auth.service';
import { InboxTab } from './inbox.tab';
import { RunsTab } from './runs.tab';
import { StatementsTab } from './statements.tab';

/** Etapa 3 · Conciliación bancaria: bandeja de diferencias, ejecuciones y extractos. */
@Component({
  selector: 'fc-reconciliation-page',
  imports: [MatTabsModule, MatIconModule, InboxTab, RunsTab, StatementsTab],
  template: `
    <header class="fc-page-header fc-enter">
      <div>
        <h1>Conciliación</h1>
        <p>Compara tus operaciones con el banco y revisa con calma lo que no cuadra.</p>
      </div>
    </header>

    <mat-tab-group class="tabs fc-enter" style="--i: 1" [selectedIndex]="tab()" (selectedIndexChange)="select($event)"
                   mat-stretch-tabs="false" animationDuration="200ms" preserveContent>
      <mat-tab>
        <ng-template mat-tab-label><mat-icon>inbox</mat-icon>&nbsp; Bandeja de diferencias</ng-template>
        <fc-inbox-tab [canResolve]="auth.hasPermission('reconciliation.resolve')" (goToRuns)="tab.set(1)" />
      </mat-tab>
      <mat-tab>
        <ng-template mat-tab-label><mat-icon>sync_alt</mat-icon>&nbsp; Conciliaciones</ng-template>
        <fc-runs-tab [canRun]="auth.hasPermission('reconciliation.run')" />
      </mat-tab>
      <mat-tab>
        <ng-template mat-tab-label><mat-icon>upload_file</mat-icon>&nbsp; Extractos</ng-template>
        <fc-statements-tab [canImport]="auth.hasPermission('statements.import')" />
      </mat-tab>
    </mat-tab-group>
  `,
  styles: `
    .tabs {
      --mat-tab-active-label-text-color: var(--fc-teal);
      --mat-tab-active-indicator-color: var(--fc-teal);
      --mat-tab-active-focus-label-text-color: var(--fc-teal);
      --mat-tab-active-hover-label-text-color: var(--fc-teal);
      --mat-tab-label-text-weight: 700;
    }
    :host ::ng-deep .mat-mdc-tab-body-content { padding-top: 20px; overflow: visible; }
  `,
})
export class ReconciliationPage {
  protected readonly auth = inject(AuthService);
  protected readonly tab = signal(0);
  private readonly inbox = viewChild(InboxTab);

  /** Al volver a la bandeja se recarga: puede haber una conciliación nueva. */
  protected select(index: number): void {
    this.tab.set(index);
    if (index === 0) this.inbox()?.load();
  }
}
