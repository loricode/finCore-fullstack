import { DatePipe } from '@angular/common';
import { Component, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { ReconciliationSummary } from '../../core/api/api.models';
import { FinCoreApi } from '../../core/api/fincore-api.service';
import { Notify } from '../../shared/notify.service';
import { ReconciliationDetailDialog } from './reconciliation-detail.dialog';
import { RunReconciliationDialog } from './run-reconciliation.dialog';

/** HU-14 a HU-16 · Historial de conciliaciones y ejecución de una nueva. */
@Component({
  selector: 'fc-runs-tab',
  imports: [DatePipe, MatTableModule, MatButtonModule, MatIconModule, MatProgressBarModule],
  template: `
    <div class="toolbar">
      <p class="fc-muted">Cada conciliación compara referencia, valor y fecha. Repetirla no duplica nada.</p>
      @if (canRun()) {
        <button matButton="filled" class="fc-sofa-btn" (click)="run()"><mat-icon>play_arrow</mat-icon> Nueva conciliación</button>
      }
    </div>

    <section class="fc-table-card">
      @if (loading()) { <mat-progress-bar mode="indeterminate" /> }
      <div class="fc-table-scroll">
        <table mat-table [dataSource]="rows()" class="fc-table">
          <ng-container matColumnDef="when">
            <th mat-header-cell *matHeaderCellDef>Ejecutada</th>
            <td mat-cell *matCellDef="let r">{{ r.startedAtUtc + 'Z' | date: 'd MMM, h:mm a' }}<div class="sub">por {{ r.executedBy ?? '—' }}</div></td>
          </ng-container>
          <ng-container matColumnDef="account">
            <th mat-header-cell *matHeaderCellDef>Cuenta</th>
            <td mat-cell *matCellDef="let r"><strong>{{ r.accountCode }}</strong></td>
          </ng-container>
          <ng-container matColumnDef="period">
            <th mat-header-cell *matHeaderCellDef>Período</th>
            <td mat-cell *matCellDef="let r">
              {{ r.periodFrom + 'T00:00:00' | date: 'd MMM' }} – {{ r.periodTo + 'T00:00:00' | date: 'd MMM y' }}
              <div class="sub">tolerancia {{ r.dateToleranceDays }} día(s)</div>
            </td>
          </ng-container>
          <ng-container matColumnDef="result">
            <th mat-header-cell *matHeaderCellDef>Resultado</th>
            <td mat-cell *matCellDef="let r">
              <div class="bar-mini" [attr.aria-label]="r.matchedCount + ' conciliadas de ' + (r.matchedCount + r.differenceCount)">
                <span class="ok" [style.flex]="r.matchedCount || 0.0001"></span>
                <span class="diff" [style.flex]="r.differenceCount || 0.0001"></span>
              </div>
              <div class="sub">{{ r.matchedCount }} conciliadas · {{ r.differenceCount }} diferencias · {{ r.openDifferenceCount }} abiertas</div>
            </td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Estado</th>
            <td mat-cell *matCellDef="let r"><span [class]="'fc-status fc-status--' + r.status">{{ statusName(r.status) }}</span></td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let row; columns: columns" class="fc-row-clickable" tabindex="0"
              (click)="open(row)" (keydown.enter)="open(row)"></tr>
        </table>
      </div>
      @if (!loading() && rows().length === 0) {
        <div class="fc-empty"><mat-icon>sync_alt</mat-icon><p>Aún no hay conciliaciones. Empieza con la cuenta de Montería del 1 al 15 de noviembre.</p></div>
      }
    </section>
  `,
  styles: `
    .toolbar { display: flex; align-items: center; justify-content: space-between; gap: 12px; flex-wrap: wrap; margin-bottom: 14px; p { margin: 0; } }
    .sub { color: var(--fc-text-muted); font-size: 12.5px; }
    .bar-mini { display: flex; height: 8px; width: 180px; border-radius: 999px; overflow: hidden; background: var(--fc-linen); margin-bottom: 4px; }
    .bar-mini .ok { background: var(--fc-sage); }
    .bar-mini .diff { background: var(--fc-blush); opacity: .75; }
  `,
})
export class RunsTab {
  private readonly api = inject(FinCoreApi);
  private readonly dialog = inject(MatDialog);
  private readonly notify = inject(Notify);

  readonly canRun = input(false);

  protected readonly columns = ['when', 'account', 'period', 'result', 'status'];
  protected readonly rows = signal<ReconciliationSummary[]>([]);
  protected readonly loading = signal(true);

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.api.reconciliations(1, 30).subscribe({
      next: (page) => { this.rows.set(page.items); this.loading.set(false); },
      error: (e) => { this.loading.set(false); this.notify.error(e); },
    });
  }

  protected statusName(status: string): string {
    return status === 'COMPLETED' ? 'Completada' : status === 'FAILED' ? 'Falló' : 'En curso';
  }

  protected run(): void {
    this.dialog
      .open<RunReconciliationDialog, void, ReconciliationSummary>(RunReconciliationDialog, { width: '620px', maxWidth: '96vw', panelClass: 'fc-dialog' })
      .afterClosed()
      .subscribe((result) => {
        if (!result) return;
        this.load();
        this.open(result);
      });
  }

  protected open(row: ReconciliationSummary): void {
    this.dialog.open(ReconciliationDetailDialog, { data: row.reconciliationId, width: '980px', maxWidth: '96vw', panelClass: 'fc-dialog' });
  }
}
