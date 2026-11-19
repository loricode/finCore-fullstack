/* Índices de ETL, integración, auditoría y seguridad. */
SET NOCOUNT ON;

-- ETL: errores de una ejecución.
CREATE NONCLUSTERED INDEX IX_ETLExecutionDetails_Execution
    ON etl.ETLExecutionDetails (EtlExecutionId, RowNumber);

-- ETL: últimas ejecuciones por empresa (monitoreo).
CREATE NONCLUSTERED INDEX IX_ETLExecutions_Company_StartedAtUtc
    ON etl.ETLExecutions (CompanyId, StartedAtUtc DESC)
    INCLUDE (Status, PackageName);

-- Integración: el Worker busca eventos pendientes o fallidos para reintentar.
CREATE NONCLUSTERED INDEX IX_InboundEvents_Status_ReceivedAtUtc
    ON integration.InboundEvents (Status, ReceivedAtUtc)
    WHERE Status IN ('RECEIVED', 'FAILED');

-- Auditoría: historia de una entidad.
CREATE NONCLUSTERED INDEX IX_AuditLogs_Entity
    ON audit.AuditLogs (EntityName, EntityId, OccurredAtUtc);

-- Auditoría: todo lo que ocurrió en una petición (soporte a producción).
CREATE NONCLUSTERED INDEX IX_AuditLogs_CorrelationId
    ON audit.AuditLogs (CorrelationId)
    WHERE CorrelationId IS NOT NULL;

-- Auditoría: actividad de un usuario.
CREATE NONCLUSTERED INDEX IX_AuditLogs_User_OccurredAtUtc
    ON audit.AuditLogs (UserId, OccurredAtUtc)
    INCLUDE (Action, EntityName);

-- Seguridad: llaves foráneas consultadas con frecuencia.
CREATE NONCLUSTERED INDEX IX_UserBranches_Branch ON sec.UserBranches (BranchId);
CREATE NONCLUSTERED INDEX IX_RefreshTokens_User ON sec.RefreshTokens (UserId) INCLUDE (ExpiresAtUtc, RevokedAtUtc);
CREATE NONCLUSTERED INDEX IX_Accounts_Branch ON fin.Accounts (BranchId);
