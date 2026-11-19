/* sec.Roles · Catálogo de roles. Ids fijos: se reflejan como enum en FinCore.Domain. */
SET NOCOUNT ON;

CREATE TABLE sec.Roles
(
    Id           SMALLINT      NOT NULL,
    Code         VARCHAR(30)   NOT NULL,
    Name         NVARCHAR(80)  NOT NULL,
    Description  NVARCHAR(250) NULL,
    CONSTRAINT PK_Roles PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Roles_Code UNIQUE (Code)
);
