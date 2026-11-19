import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorIntl, MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { catchError, debounceTime, of } from 'rxjs';
import { STATUSES, TRANSACTION_TYPES, Transaction, toIsoDate } from '../../core/api/api.models';
import { FinCoreApi } from '../../core/api/fincore-api.service';
import { AuthService } from '../../core/auth/auth.service';
import { Notify } from '../../shared/notify.service';
import { spanishPaginator } from '../../shared/spanish-paginator';
import { TransactionDetailDialog } from './transaction-detail.dialog';
import { TransactionFormDialog, TransactionFormData } from './transaction-form.dialog';
import { TYPE_ICONS } from './operation-icons';

/** HU-06 a HU-10 · Operaciones de las sucursales autorizadas. */
@Component({
  selector: 'fc-operations-page',
  imports: [
    ReactiveFormsModule, CurrencyPipe, DatePipe, MatTableModule, MatPaginatorModule, MatFormFieldModule, MatSelectModule,
    MatInputModule, MatDatepickerModule, MatButtonModule, MatIconModule, MatProgressBarModule, MatTooltipModule,
  ],
  providers: [{ provide: MatPaginatorIntl, useFactory: spanishPaginator }],
  templateUrl: './operations.page.html',
  styleUrl: './operations.page.scss',
})
export class OperationsPage {
  private readonly api = inject(FinCoreApi);
  private readonly dialog = inject(MatDialog);
  private readonly notify = inject(Notify);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly auth = inject(AuthService);

  protected readonly statuses = STATUSES;
  protected readonly types = TRANSACTION_TYPES;
  protected readonly typeIcons = TYPE_ICONS;
  protected readonly columns = ['operationDate', 'reference', 'branch', 'type', 'amount', 'status', 'createdBy'];
  protected readonly canCreate = this.auth.hasPermission('operations.create');

  protected readonly branches = toSignal(this.api.branches().pipe(catchError(() => of([]))), { initialValue: [] });

  protected readonly filters = inject(NonNullableFormBuilder).group({
    search: [''],
    branchId: [null as number | null],
    status: [null as string | null],
    type: [null as string | null],
    from: [null as Date | null],
    to: [null as Date | null],
  });

  protected readonly rows = signal<Transaction[]>([]);
  protected readonly total = signal(0);
  protected readonly page = signal(0);
  protected readonly pageSize = signal(15);
  protected readonly loading = signal(false);
  protected readonly loadError = signal<string | null>(null);

  constructor() {
    this.filters.valueChanges.pipe(debounceTime(350), takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.page.set(0);
      this.load();
    });
    this.load();
  }

  protected load(): void {
    const f = this.filters.getRawValue();
    this.loading.set(true);
    this.api
      .transactions({
        search: f.search.trim() || null,
        branchId: f.branchId,
        status: f.status,
        type: f.type,
        from: f.from ? toIsoDate(f.from) : null,
        to: f.to ? toIsoDate(f.to) : null,
        page: this.page() + 1,
        pageSize: this.pageSize(),
      })
      .subscribe({
        next: (result) => {
          this.rows.set(result.items);
          this.total.set(result.totalCount);
          this.loadError.set(null);
          this.loading.set(false);
        },
        error: (e) => {
          this.loadError.set('No pudimos cargar las operaciones. Intenta de nuevo.');
          this.loading.set(false);
          this.notify.error(e);
        },
      });
  }

  protected onPage(event: PageEvent): void {
    this.page.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
    this.load();
  }

  protected clearFilters(): void {
    this.filters.reset({ search: '', branchId: null, status: null, type: null, from: null, to: null });
  }

  protected hasFilters(): boolean {
    const f = this.filters.getRawValue();
    return !!(f.search || f.branchId || f.status || f.type || f.from || f.to);
  }

  protected openNew(): void {
    this.dialog
      .open<TransactionFormDialog, TransactionFormData, boolean>(TransactionFormDialog, {
        data: { mode: 'create' }, width: '640px', maxWidth: '96vw', panelClass: 'fc-dialog', autoFocus: 'first-tabbable',
      })
      .afterClosed()
      .subscribe((saved) => saved && this.load());
  }

  protected openDetail(row: Transaction): void {
    this.dialog
      .open<TransactionDetailDialog, string, boolean>(TransactionDetailDialog, {
        data: row.publicId, width: '760px', maxWidth: '96vw', panelClass: 'fc-dialog', disableClose: true,
      })
      .afterClosed()
      .subscribe((changed) => changed && this.load());
  }
}
