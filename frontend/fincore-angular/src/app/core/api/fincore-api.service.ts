import { HttpClient, HttpHeaders, HttpParams, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AuthorizedBranch } from '../auth/auth.models';
import {
  Account,
  BranchSummaryRow,
  FinancialIndicators,
  OperationsByPeriodRow,
  ReportName,
  UserActivityRow,
  EtlError,
  EtlExecution,
  ImportResult,
  OpenDifference,
  PagedResult,
  ReconciliationSummary,
  ReconciliationWithDetails,
  RegisterTransactionRequest,
  Role,
  Transaction,
  TransactionDetail,
  TransactionFilter,
  TransactionSummary,
  UpdateTransactionRequest,
  UserDetail,
  UserSummary,
} from './api.models';

/** Cliente tipado de la API de FinCore. Los componentes no arman URLs. */
@Injectable({ providedIn: 'root' })
export class FinCoreApi {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1';

  // ------------------------------------------------------------------ organización
  branches(): Observable<AuthorizedBranch[]> {
    return this.http.get<AuthorizedBranch[]>(`${this.base}/branches`);
  }

  accounts(): Observable<Account[]> {
    return this.http.get<Account[]>(`${this.base}/accounts`);
  }

  // ------------------------------------------------------------------ operaciones
  transactions(filter: TransactionFilter): Observable<PagedResult<Transaction>> {
    let params = new HttpParams().set('page', filter.page).set('pageSize', filter.pageSize);
    for (const key of ['branchId', 'status', 'type', 'from', 'to', 'search'] as const) {
      const value = filter[key];
      if (value !== null && value !== undefined && value !== '') params = params.set(key, value);
    }
    return this.http.get<PagedResult<Transaction>>(`${this.base}/transactions`, { params });
  }

  transaction(publicId: string): Observable<TransactionDetail> {
    return this.http.get<TransactionDetail>(`${this.base}/transactions/${publicId}`);
  }

  summary(): Observable<TransactionSummary> {
    return this.http.get<TransactionSummary>(`${this.base}/transactions/summary`);
  }

  /** La Idempotency-Key evita duplicar la operación si el usuario reintenta (doble clic, red lenta). */
  registerTransaction(body: RegisterTransactionRequest, idempotencyKey: string): Observable<TransactionDetail> {
    return this.http.post<TransactionDetail>(`${this.base}/transactions`, body, {
      headers: new HttpHeaders({ 'Idempotency-Key': idempotencyKey }),
    });
  }

  updateTransaction(publicId: string, body: UpdateTransactionRequest): Observable<TransactionDetail> {
    return this.http.put<TransactionDetail>(`${this.base}/transactions/${publicId}`, body);
  }

  processTransaction(publicId: string): Observable<TransactionDetail> {
    return this.http.post<TransactionDetail>(`${this.base}/transactions/${publicId}/process`, {});
  }

  rejectTransaction(publicId: string, reason: string): Observable<TransactionDetail> {
    return this.http.post<TransactionDetail>(`${this.base}/transactions/${publicId}/reject`, { reason });
  }

  reverseTransaction(publicId: string, reason: string): Observable<TransactionDetail> {
    return this.http.post<TransactionDetail>(`${this.base}/transactions/${publicId}/reverse`, { reason });
  }

  // ------------------------------------------------------------------ conciliación
  importStatement(file: File): Observable<ImportResult> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<ImportResult>(`${this.base}/bank-statements/import`, form);
  }

  etlExecutions(page = 1, pageSize = 10): Observable<PagedResult<EtlExecution>> {
    return this.http.get<PagedResult<EtlExecution>>(`${this.base}/etl-executions`, { params: { page, pageSize } });
  }

  etlErrors(id: number): Observable<EtlError[]> {
    return this.http.get<EtlError[]>(`${this.base}/etl-executions/${id}/errors`);
  }

  runReconciliation(body: { accountId: number; periodFrom: string; periodTo: string; dateToleranceDays: number }) {
    return this.http.post<ReconciliationSummary>(`${this.base}/reconciliations`, body);
  }

  reconciliations(page = 1, pageSize = 10): Observable<PagedResult<ReconciliationSummary>> {
    return this.http.get<PagedResult<ReconciliationSummary>>(`${this.base}/reconciliations`, { params: { page, pageSize } });
  }

  reconciliation(id: number): Observable<ReconciliationWithDetails> {
    return this.http.get<ReconciliationWithDetails>(`${this.base}/reconciliations/${id}`);
  }

  openDifferences(): Observable<OpenDifference[]> {
    return this.http.get<OpenDifference[]>(`${this.base}/reconciliations/open-differences`);
  }

  resolveDifference(detailId: number, note: string): Observable<void> {
    return this.http.post<void>(`${this.base}/reconciliation-details/${detailId}/resolve`, { note });
  }

  // ------------------------------------------------------------------ reportes
  indicators(from: string, to: string): Observable<FinancialIndicators> {
    return this.http.get<FinancialIndicators>(`${this.base}/reports/indicators`, { params: { from, to } });
  }

  branchSummary(from: string, to: string): Observable<BranchSummaryRow[]> {
    return this.http.get<BranchSummaryRow[]>(`${this.base}/reports/branch-summary`, { params: { from, to } });
  }

  operationsByPeriod(from: string, to: string, granularity: 'DAY' | 'MONTH'): Observable<OperationsByPeriodRow[]> {
    return this.http.get<OperationsByPeriodRow[]>(`${this.base}/reports/operations-by-period`, { params: { from, to, granularity } });
  }

  userActivity(from: string, to: string): Observable<UserActivityRow[]> {
    return this.http.get<UserActivityRow[]>(`${this.base}/reports/user-activity`, { params: { from, to } });
  }

  /** Descarga el CSV (la API audita la exportación) con el nombre que propone el servidor. */
  exportReport(name: ReportName, params: Record<string, string>): Observable<HttpResponse<Blob>> {
    return this.http.get(`${this.base}/reports/${name}`, { params: { ...params, format: 'csv' }, responseType: 'blob', observe: 'response' });
  }

  // ------------------------------------------------------------------ usuarios
  users(): Observable<UserSummary[]> {
    return this.http.get<UserSummary[]>(`${this.base}/users`);
  }

  user(id: number): Observable<UserDetail> {
    return this.http.get<UserDetail>(`${this.base}/users/${id}`);
  }

  roles(): Observable<Role[]> {
    return this.http.get<Role[]>(`${this.base}/roles`);
  }

  createUser(body: { userName: string; email: string; fullName: string; password: string; roles: string[]; branchIds: number[] }) {
    return this.http.post<UserDetail>(`${this.base}/users`, body);
  }

  updateUser(id: number, body: { email: string; fullName: string; roles: string[]; branchIds: number[]; rowVersion: string }) {
    return this.http.put<UserDetail>(`${this.base}/users/${id}`, body);
  }

  /** Etapa 6 · Cuenta de Windows para Reporting Services; null la quita. */
  setUserWindowsAccount(id: number, windowsAccount: string | null): Observable<UserDetail> {
    return this.http.put<UserDetail>(`${this.base}/users/${id}/windows-account`, { windowsAccount });
  }

  setUserActive(id: number, active: boolean): Observable<UserDetail> {
    return this.http.post<UserDetail>(`${this.base}/users/${id}/${active ? 'activate' : 'deactivate'}`, {});
  }
}
