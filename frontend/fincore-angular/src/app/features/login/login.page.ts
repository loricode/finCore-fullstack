import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatListModule } from '@angular/material/list';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { ActivatedRoute, Router } from '@angular/router';
import { describeError } from '../../core/api/problem';
import { AuthService } from '../../core/auth/auth.service';
import { ForgotPasswordDialog } from './forgot-password.dialog';
import { LoginIllustrationComponent } from './login-illustration.component';

const LAST_USER_KEY = 'fincore.lastUser';

/** Preferencia local de cortesía: recordar el último usuario para saludarlo. Puede no estar disponible (modo privado). */
function readLastUser(): string | null {
  try { return localStorage.getItem(LAST_USER_KEY); } catch { return null; }
}
function writeLastUser(value: string | null): void {
  try { value ? localStorage.setItem(LAST_USER_KEY, value) : localStorage.removeItem(LAST_USER_KEY); } catch { /* sin almacenamiento */ }
}

@Component({
  selector: 'fc-login-page',
  imports: [
    ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule, MatCheckboxModule, MatTabsModule,
    MatListModule, MatProgressBarModule, LoginIllustrationComponent,
  ],
  templateUrl: './login.page.html',
  styleUrl: './login.page.scss',
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly dialog = inject(MatDialog);

  protected readonly submitting = signal(false);
  protected readonly showPassword = signal(false);
  protected readonly tab = signal(0);
  protected readonly error = signal<{ message: string; correlationId?: string } | null>(null);
  protected readonly expired = this.route.snapshot.queryParamMap.has('expired');
  protected readonly lastUser = readLastUser();

  protected readonly greeting = (() => {
    const hour = new Date().getHours();
    return hour < 12 ? 'Buenos días' : hour < 19 ? 'Buenas tardes' : 'Buenas noches';
  })();

  protected readonly demoUsers = [
    { userName: 'operador.monteria', name: 'Carlos · Operador', hint: 'Registra operaciones en Montería', icon: 'point_of_sale', tone: 'ochre' },
    { userName: 'supervisor', name: 'Laura · Supervisora', hint: 'Procesa, revierte y concilia', icon: 'supervisor_account', tone: 'teal' },
    { userName: 'admin', name: 'Administrador', hint: 'Usuarios, roles y sucursales', icon: 'admin_panel_settings', tone: 'sage' },
    { userName: 'auditor', name: 'Marta · Auditora', hint: 'Consulta todo, sin modificar', icon: 'fact_check', tone: 'lavender' },
  ];

  protected readonly form = inject(NonNullableFormBuilder).group({
    userName: [this.lastUser ?? '', [Validators.required, Validators.maxLength(50)]],
    password: ['', [Validators.required, Validators.maxLength(128)]],
    rememberMe: [true],
  });

  protected useDemo(userName: string): void {
    this.form.patchValue({ userName, password: 'FinCore*2026' });
    this.tab.set(0);
  }

  protected forgotPassword(): void {
    this.dialog.open(ForgotPasswordDialog, { width: '440px', panelClass: 'fc-dialog' });
  }

  protected async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting.set(true);
    this.error.set(null);
    try {
      const { userName, password, rememberMe } = this.form.getRawValue();
      await this.auth.login(userName.trim(), password, rememberMe);
      writeLastUser(rememberMe ? userName.trim() : null);
      const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
      await this.router.navigateByUrl(returnUrl?.startsWith('/') ? returnUrl : '/inicio');
    } catch (e) {
      this.error.set(describeError(e));
      this.form.controls.password.reset();
    } finally {
      this.submitting.set(false);
    }
  }
}
