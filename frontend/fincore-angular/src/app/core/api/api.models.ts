/** Contratos de la API (espejo de los DTO de FinCore.Application). */

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export type StatusCode = 'PENDING' | 'PROCESSED' | 'RECONCILED' | 'REJECTED' | 'REVERSED';
export type TypeCode = 'ING' | 'EGR' | 'TRF' | 'PAG' | 'REV';

export interface Transaction {
  publicId: string;
  reference: string;
  operationDate: string;
  branchId: number;
  branchCode: string;
  branchName: string;
  accountId: number;
  accountCode: string;
  typeCode: TypeCode;
  typeName: string;
  statusCode: StatusCode;
  statusName: string;
  amount: number;
  direction: number;
  signedAmount: number;
  description: string | null;
  counterpartyName: string | null;
  counterpartyAccount: string | null;
  rejectionReason: string | null;
  createdByUserId: number;
  createdByUserName: string;
  createdAtUtc: string;
  processedAtUtc: string | null;
  reconciledAtUtc: string | null;
  originalPublicId: string | null;
  reversalPublicId: string | null;
  rowVersion: string;
}

export interface TransactionHistory {
  fromStatus: string | null;
  toStatus: string;
  changedAtUtc: string;
  changedBy: string | null;
  correlationId: string | null;
  reason: string | null;
}

export interface TransactionActions {
  canEdit: boolean;
  canProcess: boolean;
  canReject: boolean;
  canReverse: boolean;
}

export interface TransactionDetail {
  transaction: Transaction;
  history: TransactionHistory[];
  actions: TransactionActions;
}

export interface TransactionFilter {
  branchId?: number | null;
  status?: string | null;
  type?: string | null;
  from?: string | null;
  to?: string | null;
  search?: string | null;
  page: number;
  pageSize: number;
}

export interface TransactionSummary {
  pendingCount: number;
  processedCount: number;
  reconciledCount: number;
  rejectedCount: number;
  reversedCount: number;
  monthIncome: number;
  monthExpense: number;
  monthNet: number;
  monthStart: string;
}

export interface Account {
  id: number;
  code: string;
  name: string;
  bankName: string;
  bankAccountNumberMasked: string;
  branchId: number;
  branchCode: string;
  branchName: string;
  balance: number;
}

export interface RegisterTransactionRequest {
  accountId: number;
  type: TypeCode;
  reference: string;
  operationDate: string;
  amount: number;
  description?: string | null;
  counterpartyName?: string | null;
  counterpartyAccount?: string | null;
}

export interface UpdateTransactionRequest {
  operationDate: string;
  amount: number;
  description?: string | null;
  counterpartyName?: string | null;
  counterpartyAccount?: string | null;
  rowVersion: string;
}

export interface UserSummary {
  id: number;
  userName: string;
  fullName: string;
  email: string;
  isActive: boolean;
  isLockedOut: boolean;
  lastLoginAtUtc: string | null;
  roles: string[];
  branchCodes: string[];
  /** Cuenta de Windows (DOMINIO\usuario) con la que abre los informes de Reporting Services. */
  windowsAccount: string | null;
}

export interface UserDetail {
  id: number;
  userName: string;
  fullName: string;
  email: string;
  isActive: boolean;
  isLockedOut: boolean;
  lastLoginAtUtc: string | null;
  createdAtUtc: string;
  roles: string[];
  branchIds: number[];
  rowVersion: string;
  windowsAccount: string | null;
}

export interface Role {
  code: string;
  name: string;
  description: string | null;
  permissions: string[];
  grantsAllBranches: boolean;
}

export const TRANSACTION_TYPES: { code: Exclude<TypeCode, 'REV'>; name: string; icon: string; hint: string }[] = [
  { code: 'ING', name: 'Ingreso', icon: 'south_west', hint: 'Entra dinero a la cuenta' },
  { code: 'EGR', name: 'Egreso', icon: 'north_east', hint: 'Sale dinero de la cuenta' },
  { code: 'TRF', name: 'Transferencia', icon: 'swap_horiz', hint: 'Hacia otra cuenta: requiere destino' },
  { code: 'PAG', name: 'Pago', icon: 'payments', hint: 'A un tercero: requiere contraparte' },
];

export const STATUSES: { code: StatusCode; name: string }[] = [
  { code: 'PENDING', name: 'Pendiente' },
  { code: 'PROCESSED', name: 'Procesada' },
  { code: 'RECONCILED', name: 'Conciliada' },
  { code: 'REJECTED', name: 'Rechazada' },
  { code: 'REVERSED', name: 'Revertida' },
];

/** Fecha local (hora del navegador) como yyyy-MM-dd, el formato de las fechas de negocio de la API. */
export function toIsoDate(date: Date): string {
  const p = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${p(date.getMonth() + 1)}-${p(date.getDate())}`;
}

export function fromIsoDate(value: string): Date {
  const [y, m, d] = value.split('-').map(Number);
  return new Date(y, m - 1, d);
}

// ------------------------------------------------------------------ conciliación (Etapa 3)

export type ResultTypeCode =
  | 'MATCHED' | 'AMOUNT_MISMATCH' | 'DATE_MISMATCH' | 'MISSING_IN_BANK' | 'MISSING_INTERNAL' | 'DUPLICATE_REFERENCE' | 'PENDING_REVIEW';

export const RESULT_TYPES: { code: ResultTypeCode; name: string; icon: string; hint: string }[] = [
  { code: 'AMOUNT_MISMATCH', name: 'Diferencia de valor', icon: 'price_change', hint: 'Misma referencia, otro valor en el banco' },
  { code: 'DATE_MISMATCH', name: 'Diferencia de fecha', icon: 'event_busy', hint: 'Fuera de la tolerancia de días' },
  { code: 'MISSING_IN_BANK', name: 'No está en el banco', icon: 'cloud_off', hint: 'Operación interna sin movimiento' },
  { code: 'MISSING_INTERNAL', name: 'No está en el sistema', icon: 'help', hint: 'Movimiento del banco sin operación' },
  { code: 'DUPLICATE_REFERENCE', name: 'Referencia duplicada', icon: 'content_copy', hint: 'El banco repite la referencia' },
  { code: 'PENDING_REVIEW', name: 'Pendiente de revisión', icon: 'hourglass_top', hint: 'La operación sigue pendiente' },
  { code: 'MATCHED', name: 'Conciliada', icon: 'verified', hint: 'Referencia, valor y fecha coinciden' },
];

export interface ReconciliationSummary {
  reconciliationId: number;
  accountId: number;
  accountCode: string;
  branchId: number;
  periodFrom: string;
  periodTo: string;
  dateToleranceDays: number;
  status: 'RUNNING' | 'COMPLETED' | 'FAILED';
  startedAtUtc: string;
  finishedAtUtc: string | null;
  executedBy: string | null;
  correlationId: string | null;
  internalCount: number;
  bankCount: number;
  matchedCount: number;
  differenceCount: number;
  amountMismatchCount: number;
  dateMismatchCount: number;
  missingInBankCount: number;
  missingInternalCount: number;
  duplicateReferenceCount: number;
  pendingReviewCount: number;
  openDifferenceCount: number;
  netDifferenceAmount: number;
  errorMessage: string | null;
}

export interface ReconciliationDetailRow {
  id: number;
  resultTypeCode: ResultTypeCode;
  resultTypeName: string;
  requiresAction: boolean;
  transactionPublicId: string | null;
  internalReference: string | null;
  operationDate: string | null;
  bankReference: string | null;
  movementDate: string | null;
  bankDescription: string | null;
  internalAmount: number | null;
  bankAmount: number | null;
  differenceAmount: number;
  notes: string | null;
  resolvedAtUtc: string | null;
  resolvedBy: string | null;
  resolutionNote: string | null;
}

export interface ReconciliationWithDetails {
  summary: ReconciliationSummary;
  details: ReconciliationDetailRow[];
}

export interface OpenDifference {
  reconciliationDetailId: number;
  reconciliationId: number;
  accountId: number;
  accountCode: string;
  branchId: number;
  branchName: string;
  periodFrom: string;
  periodTo: string;
  resultTypeCode: ResultTypeCode;
  resultTypeName: string;
  transactionPublicId: string | null;
  internalReference: string | null;
  operationDate: string | null;
  bankReference: string | null;
  movementDate: string | null;
  internalAmount: number | null;
  bankAmount: number | null;
  differenceAmount: number;
  notes: string | null;
}

export interface EtlError {
  rowNumber: number;
  sourceKey: string | null;
  errorCode: string;
  errorMessage: string;
}

export interface ImportResult {
  etlExecutionId: number;
  status: string;
  rowsRead: number;
  rowsLoaded: number;
  rowsRejected: number;
  errors: EtlError[];
}

export interface EtlExecution {
  etlExecutionId: number;
  packageName: string;
  sourceSystem: string;
  sourceName: string | null;
  status: 'RUNNING' | 'COMPLETED' | 'COMPLETED_WITH_ERRORS' | 'FAILED';
  startedAtUtc: string;
  finishedAtUtc: string | null;
  rowsRead: number;
  rowsLoaded: number;
  rowsRejected: number;
  errorCodes: string | null;
  errorMessage: string | null;
  triggeredBy: string;
}

// ------------------------------------------------------------------ reportes (Etapa 5)

export interface FinancialIndicators {
  dateFrom: string;
  dateTo: string;
  operationCount: number;
  pendingCount: number;
  processedCount: number;
  reconciledCount: number;
  rejectedCount: number;
  reversedCount: number;
  totalIncome: number;
  totalExpense: number;
  netAmount: number;
  reconciliationCoveragePct: number;
  openDifferenceCount: number;
  openDifferenceAmount: number;
  etlExecutionCount: number;
  etlRowsRead: number;
  etlRowsRejected: number;
  etlRejectionPct: number;
}

export interface BranchSummaryRow {
  branchId: number;
  branchCode: string;
  branchName: string;
  city: string;
  totalOperations: number;
  totalIncome: number;
  totalExpense: number;
  netAmount: number;
  pendingCount: number;
  processedCount: number;
  reconciledCount: number;
  rejectedCount: number;
  reversedCount: number;
}

export interface OperationsByPeriodRow {
  periodStart: string;
  transactionTypeCode: TypeCode;
  transactionTypeName: string;
  operationCount: number;
  effectiveCount: number;
  effectiveAmount: number;
}

export interface UserActivityRow {
  userId: number;
  userName: string;
  fullName: string;
  isActive: boolean;
  lastLoginAtUtc: string | null;
  operationsCreated: number;
  amountCreated: number;
  statusChanges: number;
  auditActions: number;
  lastActionAtUtc: string | null;
}

export type ReportName = 'branch-summary' | 'operations-by-period' | 'user-activity';
