/** Respuesta de POST /api/v1/auth/login y /refresh. El refresh token viaja en una cookie HttpOnly. */
export interface SessionResponse {
  accessToken: string;
  expiresAtUtc: string;
}

export interface AuthorizedBranch {
  branchId: number;
  code: string;
  name: string;
  city: string;
}

/** GET /api/v1/auth/me */
export interface CurrentUser {
  id: number;
  userName: string;
  fullName: string;
  email: string;
  companyId: number;
  roles: string[];
  permissions: string[];
  branches: AuthorizedBranch[];
}

/** application/problem+json (RFC 9457) con las extensiones de FinCore. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  correlationId?: string;
  errors?: Record<string, string[]>;
}

/** Permisos usados por la interfaz. Deben coincidir con sec.Permissions. */
export const Permissions = {
  usersManage: 'users.manage',
  operationsRead: 'operations.read',
  reconciliationRead: 'reconciliation.read',
  reportsRead: 'reports.read',
  auditRead: 'audit.read',
} as const;
