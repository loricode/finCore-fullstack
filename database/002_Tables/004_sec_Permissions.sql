/* sec.Permissions · Catálogo de permisos. El código (p. ej. operations.create) es el que usa la API en sus políticas. */
SET NOCOUNT ON;

CREATE TABLE sec.Permissions
(
    Id           SMALLINT      NOT NULL,
    Code         VARCHAR(50)   NOT NULL,
    Description  NVARCHAR(250) NOT NULL,
    CONSTRAINT PK_Permissions PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Permissions_Code UNIQUE (Code)
);
