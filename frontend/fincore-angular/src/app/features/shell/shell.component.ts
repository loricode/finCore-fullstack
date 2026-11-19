import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { Permissions } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';

interface NavItem {
  label: string;
  path: string;
  icon: string;
  permission?: string;
}

/** Marco de la aplicación autenticada: menú lateral con el degradado del sofá, filtrado por permisos. */
@Component({
  selector: 'fc-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MatButtonModule, MatIconModule, MatMenuModule, MatTooltipModule],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
})
export class ShellComponent {
  protected readonly auth = inject(AuthService);
  protected readonly menuOpen = signal(false);

  private readonly items: NavItem[] = [
    { label: 'Inicio', path: '/inicio', icon: 'cottage' },
    { label: 'Operaciones', path: '/operaciones', icon: 'receipt_long', permission: Permissions.operationsRead },
    { label: 'Conciliación', path: '/conciliacion', icon: 'sync_alt', permission: Permissions.reconciliationRead },
    { label: 'Reportes', path: '/reportes', icon: 'monitoring', permission: Permissions.reportsRead },
    { label: 'Sucursales', path: '/sucursales', icon: 'storefront' },
    { label: 'Usuarios', path: '/usuarios', icon: 'group', permission: Permissions.usersManage },
  ];

  protected readonly nav = computed(() => {
    this.auth.currentUser(); // recalcula al cambiar el usuario
    return this.items.filter((i) => !i.permission || this.auth.hasPermission(i.permission));
  });

  protected readonly firstName = computed(() => this.auth.currentUser()?.fullName.split(' ')[0] ?? '');

  protected readonly initials = computed(() =>
    (this.auth.currentUser()?.fullName ?? '?')
      .split(' ')
      .slice(0, 2)
      .map((w) => w[0])
      .join('')
      .toUpperCase(),
  );

  protected readonly greeting = (() => {
    const hour = new Date().getHours();
    return hour < 12 ? 'Buenos días' : hour < 19 ? 'Buenas tardes' : 'Buenas noches';
  })();

  protected logout(): void {
    void this.auth.logout();
  }
}
