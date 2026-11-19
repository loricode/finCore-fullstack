import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, computed, inject, input, output, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { OpenDifference, RESULT_TYPES, ResultTypeCode } from '../../core/api/api.models';
import { FinCoreApi } from '../../core/api/fincore-api.service';
import { Notify } from '../../shared/notify.service';
import { ReasonDialogComponent, ReasonDialogData } from '../../shared/reason-dialog.component';

/** HU-17 y HU-18 · Diferencias abiertas de la última conciliación de cada cuenta, con resolución por nota. */
@Component({
  selector: 'fc-inbox-tab',
  imports: [CurrencyPipe, DatePipe, MatTableModule, MatChipsModule, MatButtonModule, MatIconModule, MatProgressBarModule, MatTooltipModule],
  template: `
    @if (types().length) {
      <mat-chip-listbox class="filters" aria-label="Filtrar por tipo" [value]="filter()" (change)="filter.set($event.value ?? null)">
        @for (t of types(); track t.code) {
          <mat-chip-option [value]="t.code" [class]="'chip fc-result--' + t.code">
            <mat-icon matChipAvatar>{{ t.icon }}</mat-icon>{{ t.name }} · {{ t.count }}
          </mat-chip-option>
        }
      </mat-chip-listbox>
    }

    <section class="fc-table-card">
      @if (loading()) { <mat-progress-bar mode="indeterminate" /> }
      <div class="fc-table-scroll">
        <table mat-table [dataSource]="visible()" class="fc-table">
          <ng-container matColumnDef="type">
            <th mat-header-cell *matHeaderCellDef>Diferencia</th>
            <td mat-cell *matCellDef="let d">
              <span class="fc-status" [class]="'fc-status fc-result--' + d.resultTypeCode">{{ d.resultTypeName }}</span>
              @if (d.notes) { <div class="sub">{{ d.notes }}</div> }
            </td>
          </ng-container>
          <ng-container matColumnDef="account">
            <th mat-header-cell *matHeaderCellDef>Cuenta</th>
            <td mat-cell *matCellDef="let d"><strong>{{ d.accountCode }}</strong><div class="sub">{{ d.branchName }}</div></td>
          </ng-container>
          <ng-container matColumnDef="internal">
            <th mat-header-cell *matHeaderCellDef>En FinCore</th>
            <td mat-cell *matCellDef="let d">
              @if (d.internalReference) {
                <span class="fc-mono">{{ d.internalReference }}</span>
                <div class="sub">{{ d.operationDate + 'T00:00:00' | date: 'd MMM' }}</div>
              } @else { <span class="sub">—</span> }
            </td>
          </ng-container>
          <ng-container matColumnDef="bank">
            <th mat-header-cell *matHeaderCellDef>En el banco</th>
            <td mat-cell *matCellDef="let d">
              @if (d.movementDate) {
                <span class="fc-mono">{{ d.bankReference ?? 'sin referencia' }}</span>
                <div class="sub">{{ d.movementDate + 'T00:00:00' | date: 'd MMM' }}</div>
              } @else { <span class="sub">—</span> }
            </td>
          </ng-container>
          <ng-container matColumnDef="amounts">
            <th mat-header-cell *matHeaderCellDef class="fc-num">Interno / Banco</th>
            <td mat-cell *matCellDef="let d" class="fc-num">
              <span class="fc-money">{{ d.internalAmount === null ? '—' : (d.internalAmount | currency: 'COP' : 'symbol-narrow' : '1.0-2') }}</span>
              <div class="sub fc-money">{{ d.bankAmount === null ? '—' : (d.bankAmount | currency: 'COP' : 'symbol-narrow' : '1.0-2') }}</div>
            </td>
          </ng-container>
          <ng-container matColumnDef="difference">
            <th mat-header-cell *matHeaderCellDef class="fc-num">Diferencia</th>
            <td mat-cell *matCellDef="let d" class="fc-num">
              <strong class="fc-money" [class.fc-money--out]="d.differenceAmount !== 0">{{ d.differenceAmount | currency: 'COP' : 'symbol-narrow' : '1.0-2' }}</strong>
            </td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let d" class="fc-num">
              @if (canResolve()) {
                <button matButton="tonal" class="fc-pastel fc-pastel--sage" (click)="resolve(d)"><mat-icon>task_alt</mat-icon> Resolver</button>
              }
            </td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let row; columns: columns"></tr>
        </table>
      </div>

      @if (!loading() && rows().length === 0) {
        <div class="fc-empty calm">
          <mat-icon>spa</mat-icon>
          <h3>Todo cuadra</h3>
          <p>No hay diferencias abiertas. Cuando concilies una cuenta, lo que no coincida aparecerá aquí.</p>
          <button matButton="tonal" class="fc-pastel fc-pastel--teal" (click)="goToRuns.emit()"><mat-icon>sync_alt</mat-icon> Ir a conciliar</button>
        </div>
      }
    </section>
  `,
  styles: `
    .filters { display: block; margin-bottom: 14px; }
    .chip { --mat-chip-label-text-color: currentColor; --mat-chip-with-icon-icon-color: currentColor; border: 0 !important; }
    .sub { color: var(--fc-text-muted); font-size: 12.5px; max-width: 280px; }
    .calm h3 { font-family: var(--fc-display); color: var(--fc-text); margin: 8px 0 4px; }
    .calm mat-icon { color: var(--fc-sage) !important; }
  `,
})
export class InboxTab {
  private readonly api = inject(FinCoreApi);
  private readonly dialog = inject(MatDialog);
  private readonly notify = inject(Notify);

  readonly canResolve = input(false);
  readonly goToRuns = output<void>();

  protected readonly columns = ['type', 'account', 'internal', 'bank', 'amounts', 'difference', 'actions'];
  protected readonly rows = signal<OpenDifference[]>([]);
  protected readonly loading = signal(true);
  protected readonly filter = signal<ResultTypeCode | null>(null);

  protected readonly visible = computed(() => {
    const f = this.filter();
    return f ? this.rows().filter((r) => r.resultTypeCode === f) : this.rows();
  });

  protected readonly types = computed(() =>
    RESULT_TYPES.map((t) => ({ ...t, count: this.rows().filter((r) => r.resultTypeCode === t.code).length })).filter((t) => t.count > 0),
  );

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.openDifferences().subscribe({
      next: (rows) => { this.rows.set(rows); this.loading.set(false); },
      error: (e) => { this.loading.set(false); this.notify.error(e); },
    });
  }

  protected resolve(d: OpenDifference): void {
    this.dialog
      .open<ReasonDialogComponent, ReasonDialogData, string>(ReasonDialogComponent, {
        width: '500px', panelClass: 'fc-dialog',
        data: {
          title: 'Resolver diferencia', icon: 'task_alt', tone: 'sage', confirmLabel: 'Marcar como resuelta',
          label: 'Nota de resolución', maxLength: 500,
          message: `${d.resultTypeName} en ${d.accountCode}. Cuenta qué se hizo (por ejemplo: "Comisión del banco, se registró el egreso EGR-1110").`,
        },
      })
      .afterClosed()
      .subscribe((note) => {
        if (!note) return;
        this.api.resolveDifference(d.reconciliationDetailId, note).subscribe({
          next: () => {
            this.notify.success('Diferencia resuelta. Queda registrada con tu nota.');
            this.rows.update((rows) => rows.filter((r) => r.reconciliationDetailId !== d.reconciliationDetailId));
          },
          error: (e) => this.notify.error(e),
        });
      });
  }
}
