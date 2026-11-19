/*
    etl.Watermarks · Marca de agua de cada extracción incremental (Etapa 4).
    Guarda el último valor leído del origen (p. ej. MOVEMENT_SEQ de Oracle) para que la siguiente ejecución
    lea solo lo nuevo. La usan el FinCore.Worker y el paquete SSIS (misma fila, mismo significado).

    Si una ejecución falla después de promover pero antes de mover la marca, la siguiente vuelve a leer esas filas
    y el procedimiento de promoción las rechaza como ALREADY_LOADED: no hay duplicados, solo rechazos informativos.
*/
SET NOCOUNT ON;

CREATE TABLE etl.Watermarks
(
    SourceName    NVARCHAR(100)  NOT NULL,   -- p. ej. ORACLE:LEGACY_BANK.BANK_MOVEMENTS
    LastValue     BIGINT         NOT NULL CONSTRAINT DF_Watermarks_LastValue DEFAULT (0),
    UpdatedAtUtc  DATETIME2(3)   NOT NULL CONSTRAINT DF_Watermarks_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Watermarks PRIMARY KEY CLUSTERED (SourceName),
    CONSTRAINT CK_Watermarks_LastValue CHECK (LastValue >= 0)
);
