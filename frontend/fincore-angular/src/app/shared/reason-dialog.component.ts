import { Component, inject } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';

export interface ReasonDialogData {
  title: string;
  message: string;
  confirmLabel: string;
  icon: string;
  tone: 'blush' | 'lavender' | 'sage';
  /** Etiqueta del campo (por defecto "Motivo"). */
  label?: string;
  /** Longitud máxima (por defecto 200). */
  maxLength?: number;
}

/** Pide un texto antes de una acción irreversible (rechazar, revertir, resolver). Devuelve el texto o undefined. */
@Component({
  selector: 'fc-reason-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatIconModule],
  template: `
    <h2 mat-dialog-title class="fc-dialog-title">
      <span class="fc-dialog-icon" [class]="'fc-dialog-icon ' + data.tone"><mat-icon>{{ data.icon }}</mat-icon></span>
      {{ data.title }}
    </h2>
    <mat-dialog-content>
      <p class="fc-muted">{{ data.message }}</p>
      <mat-form-field appearance="outline" class="full">
        <mat-label>{{ data.label ?? 'Motivo' }}</mat-label>
        <textarea matInput [formControl]="reason" rows="3" [maxlength]="max" cdkFocusInitial></textarea>
        <mat-hint align="end">{{ reason.value.length }}/{{ max }}</mat-hint>
        @if (reason.hasError('required')) { <mat-error>Este campo es obligatorio.</mat-error> }
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton mat-dialog-close type="button">Cancelar</button>
      <button matButton="tonal" [class]="'fc-pastel fc-pastel--' + data.tone" type="button" (click)="confirm()">
        <mat-icon>{{ data.icon }}</mat-icon> {{ data.confirmLabel }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .full { width: 100%; }
    .fc-dialog-icon.blush { background: var(--fc-gradient-blush); color: var(--fc-blush); }
    .fc-dialog-icon.lavender { background: var(--fc-gradient-lavender); color: var(--fc-lavender); }
    .fc-dialog-icon.sage { background: var(--fc-gradient-sage); color: var(--fc-sage); }
  `,
})
export class ReasonDialogComponent {
  protected readonly data = inject<ReasonDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<ReasonDialogComponent, string>);
  protected readonly max = this.data.maxLength ?? 200;
  protected readonly reason = inject(NonNullableFormBuilder).control('', [Validators.required, Validators.maxLength(this.max)]);

  protected confirm(): void {
    if (this.reason.invalid || !this.reason.value.trim()) {
      this.reason.setErrors({ required: true });
      this.reason.markAsTouched();
      return;
    }
    this.ref.close(this.reason.value.trim());
  }
}
