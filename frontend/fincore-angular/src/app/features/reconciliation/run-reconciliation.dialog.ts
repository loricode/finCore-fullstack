import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatSliderModule } from '@angular/material/slider';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { catchError, of } from 'rxjs';
import { Account, ReconciliationSummary, toIsoDate } from '../../core/api/api.models';
import { FinCoreApi } from '../../core/api/fincore-api.service';
import { describeError } from '../../core/api/problem';
import { Notify } from '../../shared/notify.service';

/** HU-14 · Elegir cuenta, período y tolerancia y ejecutar el motor de conciliación. */
@Component({
  selector: 'fc-run-reconciliation-dialog',
  imports: [
    ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatSelectModule, MatDatepickerModule, MatSliderModule,
    MatButtonModule, MatIconModule, MatProgressSpinnerModule,
  ],
  template: `
    <h2 mat-dialog-title class="fc-dialog-title"><span class="fc-dialog-icon"><mat-icon>sync_alt</mat-icon></span> Nueva conciliación</h2>
    <form [formGroup]="form" (ngSubmit)="run()" novalidate>
      <mat-dialog-content class="content">
        <mat-form-field appearance="outline">
          <mat-label>Cuenta</mat-label>
          <mat-select formControlName="accountId">
            @for (a of accounts(); track a.id) { <mat-option [value]="a.id">{{ a.name }} · {{ a.bankName }} {{ a.bankAccountNumberMasked }}</mat-option> }
          </mat-select>
          @if (form.controls.accountId.hasError('required')) { <mat-error>Elige la cuenta.</mat-error> }
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Período</mat-label>
          <mat-date-range-input [rangePicker]="picker">
            <input matStartDate formControlName="from" placeholder="Desde" />
            <input matEndDate formControlName="to" placeholder="Hasta" />
          </mat-date-range-input>
          <mat-datepicker-toggle matIconSuffix [for]="picker" />
          <mat-date-range-picker #picker />
          <mat-hint>Máximo 93 días</mat-hint>
        </mat-form-field>

        <div class="tolerance">
          <label for="tol">Tolerancia de fechas: <strong>{{ form.controls.tolerance.value }} día(s)</strong></label>
          <mat-slider min="0" max="5" step="1" discrete showTickMarks class="slider">
            <input matSliderThumb formControlName="tolerance" id="tol" />
          </mat-slider>
          <p class="fc-muted">Un movimiento que el banco registra hasta {{ form.controls.tolerance.value }} día(s) después sigue contando como conciliado.</p>
        </div>

        @if (error(); as e) { <div class="fc-alert fc-alert--danger"><mat-icon>error</mat-icon> {{ e }}</div> }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button matButton type="button" mat-dialog-close>Cancelar</button>
        <button matButton="filled" class="fc-sofa-btn" type="submit" [disabled]="running()">
          @if (running()) { <mat-spinner diameter="18" /> } @else { <mat-icon>play_arrow</mat-icon> }
          {{ running() ? 'Conciliando…' : 'Conciliar' }}
        </button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .content { display: flex; flex-direction: column; gap: 6px; padding-top: 6px; }
    .tolerance { padding: 4px 4px 0; label { font-weight: 600; } p { margin: 0; font-size: 13px; } }
    .slider { width: 100%; --mat-slider-active-track-color: var(--fc-teal); --mat-slider-handle-color: var(--fc-teal); }
    mat-spinner { --mat-progress-spinner-active-indicator-color: #fff; }
  `,
})
export class RunReconciliationDialog {
  private readonly api = inject(FinCoreApi);
  private readonly ref = inject(MatDialogRef<RunReconciliationDialog, ReconciliationSummary>);
  private readonly notify = inject(Notify);

  protected readonly running = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly accounts = toSignal(this.api.accounts().pipe(catchError(() => of([] as Account[]))), { initialValue: [] as Account[] });

  // Por defecto: del 1 del mes anterior a hoy no; mes en curso hasta hoy.
  private readonly today = new Date();
  protected readonly form = inject(NonNullableFormBuilder).group({
    accountId: [null as number | null, Validators.required],
    from: [new Date(this.today.getFullYear(), this.today.getMonth(), 1) as Date | null, Validators.required],
    to: [this.today as Date | null, Validators.required],
    tolerance: [1],
  });

  protected run(): void {
    this.error.set(null);
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.getRawValue();
    this.running.set(true);
    this.api
      .runReconciliation({ accountId: v.accountId!, periodFrom: toIsoDate(v.from!), periodTo: toIsoDate(v.to!), dateToleranceDays: v.tolerance })
      .subscribe({
        next: (summary) => {
          this.notify.success(`Conciliación lista: ${summary.matchedCount} conciliadas y ${summary.differenceCount} diferencias.`);
          this.ref.close(summary);
        },
        error: (e) => { this.running.set(false); this.error.set(describeError(e).message); },
      });
  }
}
