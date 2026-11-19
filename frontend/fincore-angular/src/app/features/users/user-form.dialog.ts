import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { catchError, forkJoin, of, startWith, switchMap, tap } from 'rxjs';
import { Role, UserDetail } from '../../core/api/api.models';
import { FinCoreApi } from '../../core/api/fincore-api.service';
import { applyFieldErrors, describeError } from '../../core/api/problem';
import { AuthorizedBranch } from '../../core/auth/auth.models';
import { Notify } from '../../shared/notify.service';

export type UserFormData = { mode: 'create' } | { mode: 'edit'; userId: number };

const ROLE_ICONS: Record<string, string> = {
  ADMIN: 'admin_panel_settings', SUPERVISOR: 'supervisor_account', OPERATOR: 'point_of_sale', AUDITOR: 'fact_check', INTEGRATION: 'hub',
};

/** HU-02 · Crear o editar un usuario. HU-04 · Elegir sus sucursales. */
@Component({
  selector: 'fc-user-form-dialog',
  imports: [
    ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule, MatChipsModule, MatIconModule,
    MatProgressBarModule, MatProgressSpinnerModule,
  ],
  templateUrl: './user-form.dialog.html',
  styleUrl: './user-form.dialog.scss',
})
export class UserFormDialog {
  protected readonly data = inject<UserFormData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<UserFormDialog, boolean>);
  private readonly api = inject(FinCoreApi);
  private readonly notify = inject(Notify);

  protected readonly isEdit = this.data.mode === 'edit';
  protected readonly roleIcons = ROLE_ICONS;
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly showPassword = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly roles = signal<Role[]>([]);
  protected readonly branches = signal<AuthorizedBranch[]>([]);
  private user: UserDetail | null = null;

  protected readonly form = inject(NonNullableFormBuilder).group({
    userName: [{ value: '', disabled: this.isEdit },
      [Validators.required, Validators.pattern(/^[a-z0-9][a-z0-9._-]{2,49}$/)]],
    fullName: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(150)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(150)]],
    password: ['', this.isEdit ? [] : [Validators.required, Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{10,128}$/)]],
    roles: [[] as string[], Validators.required],
    branchIds: [[] as number[]],
    // Etapa 6 · Opcional: cuenta de Windows para Reporting Services (DOMINIO\usuario).
    windowsAccount: ['', [Validators.maxLength(205), Validators.pattern(/^\s*([^\\/:*?"<>|\s]+\\[^\\/:*?"<>|\s[\]=;,+]+)?\s*$/)]],
  });

  private readonly selectedRoles = toSignal(this.form.controls.roles.valueChanges.pipe(startWith([] as string[])), { initialValue: [] as string[] });

  /** Si algún rol elegido da acceso a todas las sucursales, no hace falta asignarlas. */
  protected readonly allBranches = computed(() =>
    this.roles().some((r) => r.grantsAllBranches && this.selectedRoles().includes(r.code)),
  );

  constructor() {
    forkJoin({
      roles: this.api.roles(),
      branches: this.api.branches(),
      user: this.data.mode === 'edit' ? this.api.user(this.data.userId) : of(null),
    })
      .pipe(catchError((e) => { this.notify.error(e); this.ref.close(false); return of(null); }))
      .subscribe((result) => {
        if (!result) return;
        this.roles.set(result.roles);
        this.branches.set(result.branches);
        if (result.user) {
          this.user = result.user;
          this.form.patchValue({
            userName: result.user.userName, fullName: result.user.fullName, email: result.user.email,
            roles: result.user.roles, branchIds: result.user.branchIds, windowsAccount: result.user.windowsAccount ?? '',
          });
        }
        this.loading.set(false);
      });
  }

  protected toggleRole(code: string): void {
    const current = this.form.controls.roles.value;
    this.form.controls.roles.setValue(current.includes(code) ? current.filter((c) => c !== code) : [...current, code]);
    this.form.controls.roles.markAsTouched();
  }

  protected toggleBranch(id: number): void {
    const current = this.form.controls.branchIds.value;
    this.form.controls.branchIds.setValue(current.includes(id) ? current.filter((b) => b !== id) : [...current, id]);
  }

  protected save(): void {
    this.error.set(null);
    const v = this.form.getRawValue();
    if (!this.allBranches() && v.branchIds.length === 0 && v.roles.length > 0) {
      this.error.set('Asigna al menos una sucursal: los roles elegidos no dan acceso a todas.');
      return;
    }
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const branchIds = this.allBranches() ? [] : v.branchIds;
    const saved = this.user
      ? this.api.updateUser(this.user.id, { email: v.email, fullName: v.fullName, roles: v.roles, branchIds, rowVersion: this.user.rowVersion })
      : this.api.createUser({ userName: v.userName.trim().toLowerCase(), email: v.email, fullName: v.fullName, password: v.password, roles: v.roles, branchIds });

    // La cuenta de Windows tiene su propio endpoint: solo se envía si cambió.
    const account = v.windowsAccount.trim() || null;
    // Si falla la cuenta, el usuario ya quedó guardado: reintentar edita (rowVersion nuevo) en vez de volver a crearlo.
    const request = saved.pipe(
      tap((u) => (this.user = u)),
      switchMap((u) => (account ?? '').toLowerCase() === (u.windowsAccount ?? '').toLowerCase()
        ? of(u)
        : this.api.setUserWindowsAccount(u.id, account).pipe(tap((updated) => (this.user = updated)))),
    );

    request.subscribe({
      next: (u) => {
        this.notify.success(this.user ? `Cambios guardados para ${u.fullName}.` : `Usuario ${u.userName} creado. Ya puede ingresar.`);
        this.ref.close(true);
      },
      error: (e) => {
        this.saving.set(false);
        const d = describeError(e);
        const unmatched = applyFieldErrors(this.form, d.fieldErrors);
        if (!d.fieldErrors || unmatched.length) this.error.set(unmatched[0] ?? d.message);
      },
    });
  }

  protected fieldError(name: 'userName' | 'fullName' | 'email' | 'password' | 'windowsAccount'): string | null {
    const c = this.form.controls[name];
    if (!c.touched || !c.errors) return null;
    if (c.errors['server']) return c.errors['server'];
    if (c.errors['required']) return 'Este campo es obligatorio.';
    if (c.errors['email']) return 'El correo no es válido.';
    if (c.errors['minlength']) return 'Mínimo 3 caracteres.';
    if (c.errors['pattern'] && name === 'windowsAccount') return 'Escríbela como DOMINIO\\usuario (por ejemplo EQUIPO\\ana).';
    if (c.errors['pattern']) {
      return name === 'password'
        ? '10+ caracteres con mayúscula, minúscula, número y símbolo.'
        : '3 a 50 caracteres: minúsculas, números, punto, guion o guion bajo.';
    }
    return 'Valor no válido.';
  }
}
