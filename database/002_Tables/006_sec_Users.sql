/*
    sec.Users · Usuarios de la plataforma.
    PasswordHash lo genera la API con PasswordHasher de ASP.NET Core Identity (PBKDF2). Nunca se guarda la clave.
*/
SET NOCOUNT ON;

CREATE TABLE sec.Users
(
    Id                INT IDENTITY(1,1) NOT NULL,
    CompanyId         INT               NOT NULL,
    UserName          NVARCHAR(50)      NOT NULL,
    Email             NVARCHAR(150)     NOT NULL,
    FullName          NVARCHAR(150)     NOT NULL,
    PasswordHash      NVARCHAR(256)     NOT NULL,
    IsActive          BIT               NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),
    FailedLoginCount  INT               NOT NULL CONSTRAINT DF_Users_FailedLoginCount DEFAULT (0),
    LockoutEndUtc     DATETIME2(3)      NULL,
    LastLoginAtUtc    DATETIME2(3)      NULL,
    CreatedAtUtc      DATETIME2(3)      NOT NULL CONSTRAINT DF_Users_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    UpdatedAtUtc      DATETIME2(3)      NULL,
    RowVersion        ROWVERSION        NOT NULL,
    CONSTRAINT PK_Users PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_Users_Companies FOREIGN KEY (CompanyId) REFERENCES sec.Companies (Id),
    CONSTRAINT UQ_Users_UserName UNIQUE (UserName),
    CONSTRAINT UQ_Users_Email UNIQUE (Email),
    CONSTRAINT UQ_Users_Id_Company UNIQUE (Id, CompanyId),
    CONSTRAINT CK_Users_FailedLoginCount CHECK (FailedLoginCount >= 0)
);
