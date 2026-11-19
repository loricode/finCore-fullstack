/*
    Bitácora de despliegue (equivalente a la tabla de historial de Flyway).
    La crea deploy.ps1 antes de cualquier otro script. No forma parte del modelo de negocio.

    ScriptType:
      VERSIONED  -> se ejecuta una sola vez. Si su checksum cambia después de aplicado, el despliegue falla.
      REPEATABLE -> CREATE OR ALTER / MERGE. Se vuelve a ejecutar cada vez que cambia su checksum.
*/
SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.SchemaVersions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SchemaVersions
    (
        Id              INT IDENTITY(1,1) NOT NULL,
        ScriptPath      NVARCHAR(400)     NOT NULL,
        ScriptType      VARCHAR(10)       NOT NULL,
        Checksum        CHAR(64)          NOT NULL,
        AppliedAtUtc    DATETIME2(3)      NOT NULL CONSTRAINT DF_SchemaVersions_AppliedAtUtc DEFAULT (SYSUTCDATETIME()),
        AppliedBy       NVARCHAR(128)     NOT NULL CONSTRAINT DF_SchemaVersions_AppliedBy DEFAULT (SUSER_SNAME()),
        DurationMs      INT               NOT NULL,
        ExecutionCount  INT               NOT NULL CONSTRAINT DF_SchemaVersions_ExecutionCount DEFAULT (1),
        CONSTRAINT PK_SchemaVersions PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_SchemaVersions_ScriptPath UNIQUE (ScriptPath),
        CONSTRAINT CK_SchemaVersions_ScriptType CHECK (ScriptType IN ('VERSIONED', 'REPEATABLE'))
    );
END;
