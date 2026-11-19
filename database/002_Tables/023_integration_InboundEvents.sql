/*
    integration.InboundEvents · Eventos de proveedores externos que el adaptador Node.js reenvía a la API.
    (Provider, ExternalEventId) es único: un webhook reenviado por el proveedor no se procesa dos veces.
    Payload: el adaptador elimina firmas y cabeceras antes de enviarlo; nunca contiene credenciales.
*/
SET NOCOUNT ON;

CREATE TABLE integration.InboundEvents
(
    Id                     BIGINT IDENTITY(1,1) NOT NULL,
    Provider               VARCHAR(50)          NOT NULL,
    ExternalEventId        NVARCHAR(100)        NOT NULL,
    EventType              VARCHAR(60)          NOT NULL,
    PayloadHash            CHAR(64)             NOT NULL,   -- SHA-256 del payload
    Payload                NVARCHAR(MAX)        NOT NULL,
    Status                 VARCHAR(12)          NOT NULL CONSTRAINT DF_InboundEvents_Status DEFAULT ('RECEIVED'),
    Attempts               INT                  NOT NULL CONSTRAINT DF_InboundEvents_Attempts DEFAULT (0),
    LastError              NVARCHAR(2000)       NULL,
    BankStatementDetailId  BIGINT               NULL,       -- movimiento creado a partir del evento
    ReceivedAtUtc          DATETIME2(3)         NOT NULL CONSTRAINT DF_InboundEvents_ReceivedAtUtc DEFAULT (SYSUTCDATETIME()),
    ProcessedAtUtc         DATETIME2(3)         NULL,
    CorrelationId          NVARCHAR(64)         NULL,
    CONSTRAINT PK_InboundEvents PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_InboundEvents_Provider_Event UNIQUE (Provider, ExternalEventId),
    CONSTRAINT FK_InboundEvents_BankDetails FOREIGN KEY (BankStatementDetailId) REFERENCES rec.BankStatementDetails (Id),
    CONSTRAINT CK_InboundEvents_Status CHECK (Status IN ('RECEIVED', 'PROCESSED', 'FAILED', 'IGNORED')),
    CONSTRAINT CK_InboundEvents_Payload CHECK (ISJSON(Payload) = 1),
    CONSTRAINT CK_InboundEvents_Attempts CHECK (Attempts >= 0)
);
