import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { startWith } from 'rxjs';
import { UserSummary } from '../../core/api/api.models';
import { FinCoreApi } from '../../core/api/fincore-api.service';
import { AuthService } from '../../core/auth/auth.service';
import { Notify } from '../../shared/notify.service';
import { UserFormData, UserFormDialog } from './user-form.dialog';

/** HU-02 y HU-04 · Usuarios de la empresa: crear, editar roles y sucursales, activar y desactivar. */
@Component({
  selector: 'fc-users-page',
  imports: [
    DatePipe, ReactiveFormsModule, MatTableModule, MatButtonModule, MatIconModule, MatMenuModule, MatProgressBarModule,
    MatFormFieldModule, MatInputModule, MatTooltipModule,
  ],
  templateUrl: './users.page.html',
  styleUrl: './users.page.scss',
})
export class UsersPage {
  private readonly api = inject(FinCoreApi);
  private readonly dialog = inject(MatDialog);
  private readonly notify = inject(Notify);
  private readonly auth = inject(AuthService);

  protected readonly columns = ['user', 'roles', 'branches', 'status', 'lastLogin', 'actions'];
  protected readonly users = signal<UserSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly search = new FormControl('', { nonNullable: true });
  private readonly searchValue = toSignal(this.search.valueChanges.pipe(startWith('')), { initialValue: '' });

  protected readonly filtered = computed(() => {
    const q = this.searchValue().trim().toLowerCase();
    return q
      ? this.users().filter((u) => `${u.userName} ${u.fullName} ${u.email}`.toLowerCase().includes(q))
      : this.users();
  });

  protected readonly myId = computed(() => this.auth.currentUser()?.id);
  protected readonly activeCount = computed(() => this.users().filter((u) => u.isActive).length);

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.api.users().subscribe({
      next: (u) => {
        this.users.set(u);
        this.loading.set(false);
      },
      error: (e) => {
        this.loading.set(false);
        this.notify.error(e);
      },
    });
  }

  protected initials(name: string): string {
    return name.split(' ').slice(0, 2).map((w) => w[0]).join('').toUpperCase();
  }

  protected statusOf(u: UserSummary): 'ACTIVE' | 'LOCKED' | 'INACTIVE' {
    return !u.isActive ? 'INACTIVE' : u.isLockedOut ? 'LOCKED' : 'ACTIVE';
  }

  protected openForm(user?: UserSummary): void {
    this.dialog
      .open<UserFormDialog, UserFormData, boolean>(UserFormDialog, {
        data: user ? { mode: 'edit', userId: user.id } : { mode: 'create' },
        width: '680px', maxWidth: '96vw', panelClass: 'fc-dialog',
      })
      .afterClosed()
      .subscribe((saved) => saved && this.load());
  }

  protected setActive(user: UserSummary, active: boolean): void {
    this.api.setUserActive(user.id, active).subscribe({
      next: () => {
        this.notify.success(active ? `${user.fullName} puede volver a ingresar.` : `${user.fullName} fue desactivado y sus sesiones se cerraron.`);
        this.load();
      },
      error: (e) => this.notify.error(e),
    });
  }
}
