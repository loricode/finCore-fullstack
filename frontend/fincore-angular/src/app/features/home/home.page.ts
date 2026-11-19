import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';
import { Account, TransactionSummary, fromIsoDate } from '../../core/api/api.models';
import { FinCoreApi } from '../../core/api/fincore-api.service';
import { Permissions } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';

interface Kpi {
  label: string;
  value: string;
  icon: string;
  tone: 'ochre' | 'teal' | 'sage' | 'blush' | 'lavender';
  hint: string;
}

/** Inicio: cómo va el mes, qué está pendiente y cómo están las cuentas. */
@Component({
  selector: 'fc-home-page',
  imports: [MatIconModule, MatButtonModule, RouterLink, CurrencyPipe, DatePipe],
  templateUrl: './home.page.html',
  styleUrl: './home.page.scss',
})
export class HomePage {
  protected readonly auth = inject(AuthService);
  private readonly api = inject(FinCoreApi);
  private readonly currency = new CurrencyPipe('es-CO', 'COP');

  protected readonly canSeeOperations = this.auth.hasPermission(Permissions.operationsRead);

  protected readonly summary = toSignal(
    this.canSeeOperations ? this.api.summary().pipe(catchError(() => of(null))) : of<TransactionSummary | null>(null),
  );

  protected readonly accounts = toSignal(
    this.canSeeOperations ? this.api.accounts().pipe(catchError(() => of([] as Account[]))) : of([] as Account[]),
  );

  protected readonly monthName = computed(() => {
    const s = this.summary();
    return s ? fromIsoDate(s.monthStart) : new Date();
  });

  protected readonly kpis = computed<Kpi[]>(() => {
    const s = this.summary();
    if (!s) return [];
    const money = (v: number) => this.currency.transform(v, 'COP', 'symbol-narrow', '1.0-0') ?? '';
    return [
      { label: 'Pendientes', value: String(s.pendingCount), icon: 'hourglass_top', tone: 'ochre', hint: 'Esperan revisión' },
      { label: 'Procesadas', value: String(s.processedCount), icon: 'task_alt', tone: 'teal', hint: 'Este mes' },
      { label: 'Conciliadas', value: String(s.reconciledCount), icon: 'verified', tone: 'sage', hint: 'Este mes' },
      { label: 'Ingresos', value: money(s.monthIncome), icon: 'trending_up', tone: 'sage', hint: 'Efectivos del mes' },
      { label: 'Egresos', value: money(s.monthExpense), icon: 'trending_down', tone: 'blush', hint: 'Efectivos del mes' },
      { label: 'Neto', value: money(s.monthNet), icon: 'balance', tone: 'lavender', hint: 'Ingresos − egresos' },
    ];
  });

  protected readonly totalBalance = computed(() => (this.accounts() ?? []).reduce((sum, a) => sum + a.balance, 0));
}
