/*
    audit.AuditLogs · Quién hizo qué, cuándo y desde dónde. Solo inserción (audit.trg_AuditLogs_AppendOnly).
    La escribe la API (interceptor de EF Core y eventos de seguridad).
    OldValues/NewValues son JSON y NUNCA incluyen contraseñas, hashes ni tokens.
*/
SET NOCOUNT ON;

CREATE TABLE audit.AuditLogs
(
    Id             BIGINT IDENTITY(1,1) NOT NULL,
    OccurredAtUtc  DATETIME2(3)         NOT NULL CONSTRAINT DF_AuditLogs_OccurredAtUtc DEFAULT (SYSUTCDATETIME()),
    CompanyId      INT                  NULL,
    BranchId       INT                  NULL,
    UserId         INT                  NULL,       -- NULL para acciones anónimas (login fallido) o de sistema
    UserName       NVARCHAR(100)        NULL,
    Action         VARCHAR(30)          NOT NULL,   -- CREATE, UPDATE, STATUS_CHANGE, LOGIN, LOGIN_FAILED, EXPORT...
    EntityName     NVARCHAR(100)        NOT NULL,
    EntityId       NVARCHAR(64)         NULL,
    OldValues      NVARCHAR(MAX)        NULL,
    NewValues      NVARCHAR(MAX)        NULL,
    CorrelationId  NVARCHAR(64)         NULL,
    IpAddress      NVARCHAR(45)         NULL,
    CONSTRAINT PK_AuditLogs PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT CK_AuditLogs_OldValues CHECK (OldValues IS NULL OR ISJSON(OldValues) = 1),
    CONSTRAINT CK_AuditLogs_NewValues CHECK (NewValues IS NULL OR ISJSON(NewValues) = 1)
);
