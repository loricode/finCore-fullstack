import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Observable } from 'rxjs';
import { TransactionDetail } from '../../core/api/api.models';
import { FinCoreApi } from '../../core/api/fincore-api.service';
import { Notify } from '../../shared/notify.service';
import { ReasonDialogComponent, ReasonDialogData } from '../../shared/reason-dialog.component';
import { TYPE_ICONS } from './operation-icons';
import { TransactionFormData, TransactionFormDialog } from './transaction-form.dialog';

/** Detalle de una operación: datos, historial de estados (trigger de la base) y acciones permitidas. */
@Component({
  selector: 'fc-transaction-detail-dialog',
  imports: [CurrencyPipe, DatePipe, MatDialogModule, MatButtonModule, MatIconModule, MatProgressBarModule, MatTooltipModule],
  templateUrl: './transaction-detail.dialog.html',
  styleUrl: './transaction-detail.dialog.scss',
})
export class TransactionDetailDialog {
  private readonly publicId = signal(inject<string>(MAT_DIALOG_DATA));
  private readonly ref = inject(MatDialogRef<TransactionDetailDialog, boolean>);
  private readonly api = inject(FinCoreApi);
  private readonly dialog = inject(MatDialog);
  private readonly notify = inject(Notify);

  protected readonly typeIcons = TYPE_ICONS;
  protected readonly detail = signal<TransactionDetail | null>(null);
  protected readonly busy = signal(false);
  private changed = false;

  constructor() {
    this.load();
    // El diálogo se abre con disableClose: el fondo y Esc cierran devolviendo si hubo cambios (para recargar la lista).
    this.ref.backdropClick().subscribe(() => this.close());
    this.ref.keydownEvents().subscribe((e) => e.key === 'Escape' && this.close());
  }

  protected close(): void {
    this.ref.close(this.changed);
  }

  private load(): void {
    this.api.transaction(this.publicId()).subscribe({
      next: (d) => this.detail.set(d),
      error: (e) => {
        this.notify.error(e);
        this.ref.close(this.changed);
      },
    });
  }

  /** Abre otra operación relacionada (la original o su reversión) en el mismo diálogo. */
  protected open(publicId: string): void {
    this.detail.set(null);
    this.publicId.set(publicId);
    this.load();
  }

  protected process(): void {
    this.run(this.api.processTransaction(this.publicId()), 'Operación procesada.');
  }

  protected reject(): void {
    this.askReason({
      title: 'Rechazar operación', icon: 'block', tone: 'blush', confirmLabel: 'Rechazar',
      message: 'La operación quedará rechazada y no se podrá procesar. Cuéntanos por qué.',
    }, (reason) => this.run(this.api.rejectTransaction(this.publicId(), reason), 'Operación rechazada.'));
  }

  protected reverse(): void {
    this.askReason({
      title: 'Revertir operación', icon: 'undo', tone: 'lavender', confirmLabel: 'Revertir',
      message: 'Se creará una operación de reversión por el mismo valor y en sentido contrario. La original quedará revertida.',
    }, (reason) => this.run(this.api.reverseTransaction(this.publicId(), reason), 'Reversión registrada.', true));
  }

  protected edit(): void {
    const transaction = this.detail()?.transaction;
    if (!transaction) return;
    this.dialog
      .open<TransactionFormDialog, TransactionFormData, boolean>(TransactionFormDialog, {
        data: { mode: 'edit', transaction }, width: '640px', maxWidth: '96vw', panelClass: 'fc-dialog',
      })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) {
          this.changed = true;
          this.load();
        }
      });
  }

  private askReason(data: ReasonDialogData, then: (reason: string) => void): void {
    this.dialog
      .open<ReasonDialogComponent, ReasonDialogData, string>(ReasonDialogComponent, { data, width: '480px', panelClass: 'fc-dialog' })
      .afterClosed()
      .subscribe((reason) => reason && then(reason));
  }

  private run(action: Observable<TransactionDetail>, message: string, showResult = false): void {
    this.busy.set(true);
    action.subscribe({
      next: (result) => {
        this.changed = true;
        this.busy.set(false);
        this.notify.success(message);
        // Tras revertir, la respuesta es la reversión: se muestra la original actualizada.
        if (showResult) this.load();
        else this.detail.set(result);
      },
      error: (e) => {
        this.busy.set(false);
        this.notify.error(e);
      },
    });
  }
}
