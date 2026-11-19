import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { Component, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { EtlError, EtlExecution, ImportResult } from '../../core/api/api.models';
import { FinCoreApi } from '../../core/api/fincore-api.service';
import { describeError } from '../../core/api/problem';
import { Notify } from '../../shared/notify.service';

const TEMPLATE = 'account_number;movement_id;reference;date;amount;description\n' +
  '123-456789-01;BCO-0101;TRF-2001;2026-11-18;-250000.00;TRANSFERENCIA A PROVEEDOR\n' +
  '123-456789-01;BCO-0102;ING-2002;2026-11-18;480000.00;CONSIGNACION\n';

/** HU-13 · Importar extractos CSV y ver el historial de cargas con sus rechazos. */
@Component({
  selector: 'fc-statements-tab',
  imports: [DatePipe, NgTemplateOutlet, MatButtonModule, MatIconModule, MatProgressBarModule, MatTableModule],
  template: `
    @if (canImport()) {
      <section class="drop fc-card" [class.drop--over]="dragOver()" (dragover)="onDragOver($event)" (dragleave)="dragOver.set(false)"
               (drop)="onDrop($event)">
        <span class="drop__icon"><mat-icon>upload_file</mat-icon></span>
        <div class="drop__text">
          <h3>Arrastra aquí el extracto del banco</h3>
          <p class="fc-muted">Archivo .csv de hasta 5 MB: <span class="fc-mono">account_number;movement_id;reference;date;amount;description</span></p>
        </div>
        <div class="drop__actions">
          <input #file type="file" accept=".csv,text/csv" hidden (change)="onPick(file)" />
          <button matButton="filled" class="fc-sofa-btn" [disabled]="uploading()" (click)="file.click()"><mat-icon>folder_open</mat-icon> Elegir archivo</button>
          <button matButton="tonal" class="fc-pastel fc-pastel--ochre" (click)="downloadTemplate()"><mat-icon>download</mat-icon> Plantilla</button>
        </div>
        @if (uploading()) { <mat-progress-bar mode="indeterminate" class="drop__bar" /> }
      </section>
    }

    @if (result(); as r) {
      <section class="result fc-card fc-anim-enter">
        <header>
          <h3>Resultado de la carga #{{ r.etlExecutionId }}</h3>
          <span [class]="'fc-status fc-status--' + r.status">{{ statusName(r.status) }}</span>
        </header>
        <div class="stats">
          <div class="stat stat--teal"><strong>{{ r.rowsRead }}</strong><span>leídos</span></div>
          <div class="stat stat--sage"><strong>{{ r.rowsLoaded }}</strong><span>cargados</span></div>
          <div class="stat stat--blush"><strong>{{ r.rowsRejected }}</strong><span>rechazados</span></div>
        </div>
        @if (r.errors.length) { <ng-container *ngTemplateOutlet="errorsTable; context: { $implicit: r.errors }" /> }
        @else { <p class="ok"><mat-icon>check_circle</mat-icon> Todos los movimientos quedaron listos para conciliar.</p> }
      </section>
    }

    <h3 class="section-title">Cargas anteriores</h3>
    <section class="fc-table-card">
      <div class="fc-table-scroll">
        <table mat-table [dataSource]="history()" class="fc-table">
          <ng-container matColumnDef="when">
            <th mat-header-cell *matHeaderCellDef>Fecha</th>
            <td mat-cell *matCellDef="let e">{{ e.startedAtUtc + 'Z' | date: 'd MMM, h:mm a' }}<div class="sub">{{ e.triggeredBy }}</div></td>
          </ng-container>
          <ng-container matColumnDef="source">
            <th mat-header-cell *matHeaderCellDef>Origen</th>
            <td mat-cell *matCellDef="let e"><span class="fc-tag">{{ e.sourceSystem }}</span> <span class="sub">{{ e.sourceName }}</span></td>
          </ng-container>
          <ng-container matColumnDef="rows">
            <th mat-header-cell *matHeaderCellDef class="fc-num">Leídos · Cargados · Rechazados</th>
            <td mat-cell *matCellDef="let e" class="fc-num fc-money">{{ e.rowsRead }} · {{ e.rowsLoaded }} · <span [class.fc-money--out]="e.rowsRejected">{{ e.rowsRejected }}</span></td>
          </ng-container>
          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Estado</th>
            <td mat-cell *matCellDef="let e"><span [class]="'fc-status fc-status--' + e.status">{{ statusName(e.status) }}</span></td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let row; columns: columns" [class.fc-row-clickable]="row.rowsRejected > 0"
              (click)="row.rowsRejected > 0 && showErrors(row)"></tr>
        </table>
      </div>
    </section>

    @if (selectedErrors(); as sel) {
      <section class="result fc-card fc-anim-enter">
        <header><h3>Rechazos de la carga #{{ sel.id }}</h3><button matIconButton (click)="selectedErrors.set(null)" aria-label="Cerrar"><mat-icon>close</mat-icon></button></header>
        <ng-container *ngTemplateOutlet="errorsTable; context: { $implicit: sel.errors }" />
      </section>
    }

    <ng-template #errorsTable let-errors>
      <div class="fc-table-scroll">
        <table mat-table [dataSource]="errors" class="fc-table">
          <ng-container matColumnDef="row"><th mat-header-cell *matHeaderCellDef>Línea</th><td mat-cell *matCellDef="let e">{{ e.rowNumber }}</td></ng-container>
          <ng-container matColumnDef="key"><th mat-header-cell *matHeaderCellDef>Movimiento</th><td mat-cell *matCellDef="let e" class="fc-mono">{{ e.sourceKey ?? '—' }}</td></ng-container>
          <ng-container matColumnDef="code"><th mat-header-cell *matHeaderCellDef>Código</th><td mat-cell *matCellDef="let e"><span class="fc-status fc-status--REJECTED">{{ e.errorCode }}</span></td></ng-container>
          <ng-container matColumnDef="message"><th mat-header-cell *matHeaderCellDef>Qué pasó</th><td mat-cell *matCellDef="let e">{{ e.errorMessage }}</td></ng-container>
          <tr mat-header-row *matHeaderRowDef="errorColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: errorColumns"></tr>
        </table>
      </div>
    </ng-template>
  `,
  styles: `
    .drop { position: relative; display: flex; align-items: center; gap: 18px; flex-wrap: wrap; border: 2px dashed var(--fc-border);
      background: linear-gradient(160deg, var(--fc-cream), var(--fc-linen)); margin-bottom: 18px; transition: border-color .2s, background .2s; }
    .drop--over { border-color: var(--fc-teal); background: var(--fc-gradient-teal); }
    .drop__icon { width: 56px; height: 56px; border-radius: 18px; display: grid; place-items: center; background: var(--fc-gradient-sofa); color: #fff;
      mat-icon { font-size: 28px; width: 28px; height: 28px; } }
    .drop__text { flex: 1; min-width: 240px; h3 { font-family: var(--fc-display); font-size: 18px; } p { margin: 4px 0 0; font-size: 13px; } }
    .drop__actions { display: flex; gap: 8px; flex-wrap: wrap; }
    .drop__bar { position: absolute; left: 0; right: 0; bottom: 0; border-radius: 0 0 20px 20px; }
    .result { margin-bottom: 18px; header { display: flex; justify-content: space-between; align-items: center; gap: 10px; margin-bottom: 12px; } h3 { font-family: var(--fc-display); font-size: 17px; } }
    .stats { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 10px; margin-bottom: 14px; }
    .stat { border-radius: 16px; padding: 12px 14px; display: flex; flex-direction: column;
      strong { font-family: var(--fc-display); font-size: 26px; line-height: 1.1; } span { font-size: 12.5px; font-weight: 700; } }
    .stat--teal { background: var(--fc-gradient-teal); color: var(--fc-teal); }
    .stat--sage { background: var(--fc-gradient-sage); color: var(--fc-sage); }
    .stat--blush { background: var(--fc-gradient-blush); color: var(--fc-blush); }
    .ok { display: flex; align-items: center; gap: 8px; color: var(--fc-sage); margin: 0; font-weight: 600; }
    .section-title { font-family: var(--fc-display); font-size: 17px; margin: 6px 0 10px; }
    .sub { color: var(--fc-text-muted); font-size: 12.5px; }
  `,
})
export class StatementsTab {
  private readonly api = inject(FinCoreApi);
  private readonly notify = inject(Notify);

  readonly canImport = input(false);

  protected readonly columns = ['when', 'source', 'rows', 'status'];
  protected readonly errorColumns = ['row', 'key', 'code', 'message'];
  protected readonly uploading = signal(false);
  protected readonly dragOver = signal(false);
  protected readonly result = signal<ImportResult | null>(null);
  protected readonly history = signal<EtlExecution[]>([]);
  protected readonly selectedErrors = signal<{ id: number; errors: EtlError[] } | null>(null);

  constructor() {
    this.loadHistory();
  }

  protected statusName(status: string): string {
    return ({ COMPLETED: 'Completa', COMPLETED_WITH_ERRORS: 'Con rechazos', FAILED: 'Falló', RUNNING: 'En curso' } as Record<string, string>)[status] ?? status;
  }

  protected onDragOver(event: DragEvent): void {
    event.preventDefault();
    this.dragOver.set(true);
  }

  protected onDrop(event: DragEvent): void {
    event.preventDefault();
    this.dragOver.set(false);
    const file = event.dataTransfer?.files?.[0];
    if (file) this.upload(file);
  }

  protected onPick(input: HTMLInputElement): void {
    const file = input.files?.[0];
    input.value = '';
    if (file) this.upload(file);
  }

  protected downloadTemplate(): void {
    const url = URL.createObjectURL(new Blob([TEMPLATE], { type: 'text/csv;charset=utf-8' }));
    const a = document.createElement('a');
    a.href = url;
    a.download = 'plantilla_extracto_fincore.csv';
    a.click();
    URL.revokeObjectURL(url);
  }

  private upload(file: File): void {
    if (!file.name.toLowerCase().endsWith('.csv')) { this.notify.warn('El archivo debe ser .csv.'); return; }
    if (file.size > 5 * 1024 * 1024) { this.notify.warn('El archivo supera 5 MB.'); return; }
    this.uploading.set(true);
    this.result.set(null);
    this.api.importStatement(file).subscribe({
      next: (r) => {
        this.uploading.set(false);
        this.result.set(r);
        this.loadHistory();
        if (r.rowsRejected === 0) this.notify.success(`${r.rowsLoaded} movimientos cargados.`);
      },
      error: (e) => {
        this.uploading.set(false);
        const d = describeError(e);
        if (d.fieldErrors?.['file']) this.notify.warn(d.fieldErrors['file'][0]); else this.notify.error(e);
      },
    });
  }

  protected showErrors(e: EtlExecution): void {
    this.api.etlErrors(e.etlExecutionId).subscribe({
      next: (errors) => this.selectedErrors.set({ id: e.etlExecutionId, errors }),
      error: (err) => this.notify.error(err),
    });
  }

  private loadHistory(): void {
    this.api.etlExecutions(1, 15).subscribe({ next: (p) => this.history.set(p.items), error: (e) => this.notify.error(e) });
  }
}
