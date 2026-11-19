import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { catchError, debounceTime, filter, forkJoin, of } from 'rxjs';
import {
  BranchSummaryRow,
  FinancialIndicators,
  OperationsByPeriodRow,
  ReportName,
  UserActivityRow,
  toIsoDate,
} from '../../core/api/api.models';
import { FinCoreApi } from '../../core/api/fincore-api.service';
import { APP_SETTINGS } from '../../core/app-settings';
import { AuthService } from '../../core/auth/auth.service';
import { Notify } from '../../shared/notify.service';
import { PeriodChartComponent } from './period-chart.component';

/** Etapa 5 · Reportes financieros (mismos procedimientos rpt.* que los informes de Reporting Services). */
@Component({
  selector: 'fc-reports-page',
  imports: [
    ReactiveFormsModule, CurrencyPipe, DatePipe, DecimalPipe, MatFormFieldModule, MatDatepickerModule, MatButtonModule,
    MatButtonToggleModule, MatIconModule, MatProgressBarModule, MatTableModule, MatTooltipModule, PeriodChartComponent,
  ],
  templateUrl: './reports.page.html',
  styleUrl: './reports.page.scss',
})
export class ReportsPage {
  private readonly api = inject(FinCoreApi);
  private readonly notify = inject(Notify);
  protected readonly auth = inject(AuthService);

  protected readonly ssrsUrl = APP_SETTINGS.ssrsPortalUrl;
  protected readonly canExport = this.auth.hasPermission('reports.export');
  protected readonly canSeeActivity = this.auth.hasPermission('audit.read');
  protected readonly branchColumns = ['branch', 'operations', 'flow', 'income', 'expense', 'net', 'states'];
  protected readonly activityColumns = ['user', 'operations', 'amount', 'changes', 'actions', 'last'];

  private readonly today = new Date();
  protected readonly form = inject(NonNullableFormBuilder).group({
    from: [new Date(this.today.getFullYear(), this.today.getMonth(), 1) as Date | null],
    to: [this.today as Date | null],
  });

  protected readonly granularity = signal<'DAY' | 'MONTH'>('DAY');
  protected readonly loading = signal(true);
  protected readonly indicators = signal<FinancialIndicators | null>(null);
  protected readonly branches = signal<BranchSummaryRow[]>([]);
  protected readonly periods = signal<OperationsByPeriodRow[]>([]);
  protected readonly activity = signal<UserActivityRow[]>([]);
  protected readonly exporting = signal<ReportName | null>(null);

  protected readonly maxFlow = computed(() => Math.max(1, ...this.branches().flatMap((b) => [b.totalIncome, b.totalExpense])));

  protected readonly states = computed(() => {
    const i = this.indicators();
    if (!i || i.operationCount === 0) return [];
    return [
      { code: 'PENDING', name: 'Pendientes', value: i.pendingCount },
      { code: 'PROCESSED', name: 'Procesadas', value: i.processedCount },
      { code: 'RECONCILED', name: 'Conciliadas', value: i.reconciledCount },
      { code: 'REJECTED', name: 'Rechazadas', value: i.rejectedCount },
      { code: 'REVERSED', name: 'Revertidas', value: i.reversedCount },
    ].filter((s) => s.value > 0).map((s) => ({ ...s, pct: (100 * s.value) / i.operationCount }));
  });

  /** Anillo de cobertura: longitud del arco en un círculo de radio 34. */
  protected readonly coverageDash = computed(() => {
    const pct = this.indicators()?.reconciliationCoveragePct ?? 0;
    const length = 2 * Math.PI * 34;
    return `${(pct / 100) * length} ${length}`;
  });

  constructor() {
    this.form.valueChanges
      .pipe(debounceTime(300), filter(() => !!this.form.value.from && !!this.form.value.to), takeUntilDestroyed())
      .subscribe(() => this.load());
    this.load();
  }

  private params(): { from: string; to: string } {
    const v = this.form.getRawValue();
    return { from: toIsoDate(v.from!), to: toIsoDate(v.to!) };
  }

  protected load(): void {
    const { from, to } = this.params();
    this.loading.set(true);
    forkJoin({
      indicators: this.api.indicators(from, to),
      branches: this.api.branchSummary(from, to),
      periods: this.api.operationsByPeriod(from, to, this.granularity()),
      activity: this.canSeeActivity ? this.api.userActivity(from, to).pipe(catchError(() => of([]))) : of([]),
    }).subscribe({
      next: (r) => {
        this.indicators.set(r.indicators);
        this.branches.set(r.branches);
        this.periods.set(r.periods);
        this.activity.set(r.activity);
        this.loading.set(false);
      },
      error: (e) => { this.loading.set(false); this.notify.error(e); },
    });
  }

  protected setGranularity(g: 'DAY' | 'MONTH'): void {
    this.granularity.set(g);
    const { from, to } = this.params();
    this.api.operationsByPeriod(from, to, g).subscribe({ next: (p) => this.periods.set(p), error: (e) => this.notify.error(e) });
  }

  protected export(name: ReportName): void {
    const params: Record<string, string> = this.params();
    if (name === 'operations-by-period') params['granularity'] = this.granularity();
    this.exporting.set(name);
    this.api.exportReport(name, params).subscribe({
      next: (response) => {
        const disposition = response.headers.get('Content-Disposition') ?? '';
        const fileName = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition)?.[1] ?? `fincore-${name}.csv`;
        const url = URL.createObjectURL(response.body!);
        const a = document.createElement('a');
        a.href = url;
        a.download = decodeURIComponent(fileName);
        a.click();
        URL.revokeObjectURL(url);
        this.exporting.set(null);
        this.notify.success('Reporte exportado. La exportación quedó registrada en la auditoría.');
      },
      error: (e) => { this.exporting.set(null); this.notify.error(e); },
    });
  }
}
