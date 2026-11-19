import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { RESULT_TYPES, ReconciliationWithDetails } from '../../core/api/api.models';
import { FinCoreApi } from '../../core/api/fincore-api.service';
import { Notify } from '../../shared/notify.service';

/** Resumen de una conciliación con el conteo de cada tipo de resultado y todos sus resultados. */
@Component({
  selector: 'fc-reconciliation-detail-dialog',
  imports: [CurrencyPipe, DatePipe, MatDialogModule, MatButtonModule, MatIconModule, MatProgressBarModule, MatTableModule],
  template: `
    @if (data(); as d) {
      <div class="hero">
        <div>
          <span class="fc-muted">Conciliación #{{ d.summary.reconciliationId }} · {{ d.summary.accountCode }}</span>
          <h2>{{ d.summary.periodFrom + 'T00:00:00' | date: 'd MMM' }} – {{ d.summary.periodTo + 'T00:00:00' | date: 'd MMM y' }}</h2>
        </div>
        <div class="score">
          <strong>{{ score() }}%</strong>
          <span class="fc-muted">conciliado</span>
        </div>
      </div>

      <mat-dialog-content>
        <div class="counts">
          @for (c of counts(); track c.code; let i = $index) {
            <div class="count fc-enter" [class]="'count fc-enter fc-result--' + c.code" [style.--i]="i">
              <mat-icon>{{ c.icon }}</mat-icon>
              <strong>{{ c.count }}</strong>
              <span>{{ c.name }}</span>
            </div>
          }
        </div>

        <div class="fc-table-card">
          <div class="fc-table-scroll">
            <table mat-table [dataSource]="d.details" class="fc-table">
              <ng-container matColumnDef="type">
                <th mat-header-cell *matHeaderCellDef>Resultado</th>
                <td mat-cell *matCellDef="let r"><span [class]="'fc-status fc-result--' + r.resultTypeCode">{{ r.resultTypeName }}</span></td>
              </ng-container>
              <ng-container matColumnDef="internal">
                <th mat-header-cell *matHeaderCellDef>FinCore</th>
                <td mat-cell *matCellDef="let r"><span class="fc-mono">{{ r.internalReference ?? '—' }}</span>
                  @if (r.operationDate) { <div class="sub">{{ r.operationDate + 'T00:00:00' | date: 'd MMM' }}</div> }</td>
              </ng-container>
              <ng-container matColumnDef="bank">
                <th mat-header-cell *matHeaderCellDef>Banco</th>
                <td mat-cell *matCellDef="let r"><span class="fc-mono">{{ r.bankReference ?? (r.movementDate ? 'sin referencia' : '—') }}</span>
                  @if (r.movementDate) { <div class="sub">{{ r.movementDate + 'T00:00:00' | date: 'd MMM' }} · {{ r.bankDescription }}</div> }</td>
              </ng-container>
              <ng-container matColumnDef="amounts">
                <th mat-header-cell *matHeaderCellDef class="fc-num">Interno / Banco</th>
                <td mat-cell *matCellDef="let r" class="fc-num fc-money">
                  {{ r.internalAmount === null ? '—' : (r.internalAmount | currency: 'COP' : 'symbol-narrow' : '1.0-2') }}
                  <div class="sub">{{ r.bankAmount === null ? '—' : (r.bankAmount | currency: 'COP' : 'symbol-narrow' : '1.0-2') }}</div>
                </td>
              </ng-container>
              <ng-container matColumnDef="resolution">
                <th mat-header-cell *matHeaderCellDef>Seguimiento</th>
                <td mat-cell *matCellDef="let r">
                  @if (r.resolvedAtUtc) { <span class="resolved"><mat-icon>task_alt</mat-icon> {{ r.resolvedBy }}: {{ r.resolutionNote }}</span> }
                  @else if (r.requiresAction) { <span class="sub">{{ r.notes }}</span> }
                </td>
              </ng-container>
              <tr mat-header-row *matHeaderRowDef="columns"></tr>
              <tr mat-row *matRowDef="let row; columns: columns"></tr>
            </table>
          </div>
        </div>
      </mat-dialog-content>
      <mat-dialog-actions align="end"><button matButton mat-dialog-close>Cerrar</button></mat-dialog-actions>
    } @else {
      <div class="loading"><mat-progress-bar mode="indeterminate" /><p class="fc-muted">Cargando conciliación…</p></div>
    }
  `,
  styles: `
    .hero { display: flex; justify-content: space-between; align-items: center; gap: 16px; padding: 22px 24px; background: var(--fc-gradient-sage); }
    .hero h2 { font-size: 22px; margin-top: 2px; }
    .score { text-align: right; display: flex; flex-direction: column; strong { font-family: var(--fc-display); font-size: 32px; color: var(--fc-sage); line-height: 1; } }
    .counts { display: grid; grid-template-columns: repeat(auto-fit, minmax(118px, 1fr)); gap: 10px; margin: 16px 0; }
    .count { border-radius: 16px; padding: 12px; display: flex; flex-direction: column; gap: 2px;
      strong { font-family: var(--fc-display); font-size: 22px; color: var(--fc-text); } span { font-size: 12px; font-weight: 700; } }
    .sub { color: var(--fc-text-muted); font-size: 12px; max-width: 260px; }
    .resolved { display: inline-flex; gap: 6px; align-items: flex-start; color: var(--fc-sage); font-size: 13px;
      mat-icon { font-size: 18px; width: 18px; height: 18px; } }
    .loading { padding: 40px 24px; text-align: center; }
  `,
})
export class ReconciliationDetailDialog {
  private readonly api = inject(FinCoreApi);
  private readonly notify = inject(Notify);
  protected readonly data = signal<ReconciliationWithDetails | null>(null);
  protected readonly columns = ['type', 'internal', 'bank', 'amounts', 'resolution'];

  protected readonly counts = computed(() => {
    const s = this.data()?.summary;
    if (!s) return [];
    const values: Record<string, number> = {
      MATCHED: s.matchedCount, AMOUNT_MISMATCH: s.amountMismatchCount, DATE_MISMATCH: s.dateMismatchCount,
      MISSING_IN_BANK: s.missingInBankCount, MISSING_INTERNAL: s.missingInternalCount,
      DUPLICATE_REFERENCE: s.duplicateReferenceCount, PENDING_REVIEW: s.pendingReviewCount,
    };
    return [...RESULT_TYPES].reverse().map((t) => ({ ...t, count: values[t.code] ?? 0 }));
  });

  protected readonly score = computed(() => {
    const s = this.data()?.summary;
    const total = s ? s.matchedCount + s.differenceCount : 0;
    return total ? Math.round((100 * s!.matchedCount) / total) : 0;
  });

  constructor() {
    this.api.reconciliation(inject<number>(MAT_DIALOG_DATA)).subscribe({
      next: (d) => this.data.set(d),
      error: (e) => this.notify.error(e),
    });
  }
}
