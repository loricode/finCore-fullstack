import { CurrencyPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { catchError, of, startWith } from 'rxjs';
import { Account, TRANSACTION_TYPES, Transaction, TypeCode, fromIsoDate, toIsoDate } from '../../core/api/api.models';
import { FinCoreApi } from '../../core/api/fincore-api.service';
import { applyFieldErrors, describeError } from '../../core/api/problem';
import { Notify } from '../../shared/notify.service';

export type TransactionFormData = { mode: 'create' } | { mode: 'edit'; transaction: Transaction };

/** Registrar (HU-06) o editar una operación pendiente (RN-06). */
@Component({
  selector: 'fc-transaction-form-dialog',
  imports: [
    ReactiveFormsModule, CurrencyPipe, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule,
    MatDatepickerModule, MatButtonModule, MatButtonToggleModule, MatIconModule, MatProgressSpinnerModule,
  ],
  templateUrl: './transaction-form.dialog.html',
  styleUrl: './transaction-form.dialog.scss',
})
export class TransactionFormDialog {
  protected readonly data = inject<TransactionFormData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<TransactionFormDialog, boolean>);
  private readonly api = inject(FinCoreApi);
  private readonly notify = inject(Notify);

  protected readonly types = TRANSACTION_TYPES;
  protected readonly isEdit = this.data.mode === 'edit';
  protected readonly saving = signal(false);
  protected readonly error = signal<{ message: string; correlationId?: string } | null>(null);

  // Una clave por apertura del diálogo: si el usuario reintenta tras un error de red, no se duplica la operación.
  private readonly idempotencyKey = crypto.randomUUID();

  protected readonly today = new Date();
  protected readonly minDate = new Date(this.today.getFullYear(), this.today.getMonth(), this.today.getDate() - 30);

  protected readonly accounts = toSignal(this.api.accounts().pipe(catchError(() => of([] as Account[]))), {
    initialValue: [] as Account[],
  });

  private readonly t = this.data.mode === 'edit' ? this.data.transaction : null;

  protected readonly form = inject(NonNullableFormBuilder).group({
    accountId: [{ value: this.t?.accountId ?? (null as number | null), disabled: this.isEdit }, Validators.required],
    type: [{ value: (this.t?.typeCode ?? 'ING') as TypeCode, disabled: this.isEdit }, Validators.required],
    reference: [{ value: this.t?.reference ?? '', disabled: this.isEdit },
      [Validators.required, Validators.minLength(4), Validators.maxLength(40), Validators.pattern(/^[A-Za-z0-9][A-Za-z0-9_/.-]+$/)]],
    operationDate: [this.t ? fromIsoDate(this.t.operationDate) : this.today, Validators.required],
    amount: [this.t?.amount ?? (null as number | null), [Validators.required, Validators.min(0.01), Validators.max(5_000_000_000)]],
    counterpartyName: [this.t?.counterpartyName ?? '', Validators.maxLength(150)],
    counterpartyAccount: [this.t?.counterpartyAccount ?? '', Validators.maxLength(40)],
    description: [this.t?.description ?? '', Validators.maxLength(250)],
  });

  private readonly typeValue = toSignal(this.form.controls.type.valueChanges.pipe(startWith(this.form.controls.type.value)));
  private readonly amountValue = toSignal(this.form.controls.amount.valueChanges.pipe(startWith(this.form.controls.amount.value)));

  protected readonly needsCounterparty = computed(() => this.typeValue() === 'TRF' || this.typeValue() === 'PAG');
  protected readonly needsCounterpartyAccount = computed(() => this.typeValue() === 'TRF');
  protected readonly isIncome = computed(() => this.typeValue() === 'ING');
  protected readonly bigAmount = computed(() => (this.amountValue() ?? 0) >= 10_000_000);

  protected async save(): Promise<void> {
    this.error.set(null);
    const v = this.form.getRawValue();
    if (this.needsCounterparty() && !v.counterpartyName.trim()) this.form.controls.counterpartyName.setErrors({ required: true });
    if (this.needsCounterpartyAccount() && !v.counterpartyAccount.trim()) this.form.controls.counterpartyAccount.setErrors({ required: true });
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const common = {
      operationDate: toIsoDate(v.operationDate),
      amount: Number(v.amount),
      description: v.description.trim() || null,
      counterpartyName: this.needsCounterparty() ? v.counterpartyName.trim() : null,
      counterpartyAccount: this.needsCounterpartyAccount() ? v.counterpartyAccount.trim() : null,
    };
    const request = this.t
      ? this.api.updateTransaction(this.t.publicId, { ...common, rowVersion: this.t.rowVersion })
      : this.api.registerTransaction(
          { ...common, accountId: v.accountId!, type: v.type, reference: v.reference.trim().toUpperCase() },
          this.idempotencyKey,
        );

    request.subscribe({
      next: (detail) => {
        this.notify.success(this.t ? 'Operación actualizada.' : `Operación ${detail.transaction.reference} registrada. Queda pendiente de procesar.`);
        this.ref.close(true);
      },
      error: (e) => {
        this.saving.set(false);
        const described = describeError(e);
        const unmatched = applyFieldErrors(this.form, described.fieldErrors);
        if (!described.fieldErrors || unmatched.length) this.error.set({ message: unmatched[0] ?? described.message, correlationId: described.correlationId });
      },
    });
  }

  protected fieldError(name: keyof typeof this.form.controls): string | null {
    const c = this.form.controls[name];
    if (!c.touched || !c.errors) return null;
    if (c.errors['server']) return c.errors['server'];
    if (c.errors['required']) return 'Este campo es obligatorio.';
    if (c.errors['min']) return 'Debe ser mayor que cero.';
    if (c.errors['max']) return 'No puede superar 5.000 millones.';
    if (c.errors['minlength']) return 'Mínimo 4 caracteres.';
    if (c.errors['pattern']) return 'Solo letras, números, guion, punto, barra o guion bajo.';
    if (c.errors['matDatepickerMin'] || c.errors['matDatepickerMax']) return 'Entre hoy y 30 días atrás.';
    return 'Valor no válido.';
  }
}
