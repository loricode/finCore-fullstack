/* sec.RefreshTokens · Tokens de refresco con rotación. Se guarda el SHA-256 del token, nunca el token. */
SET NOCOUNT ON;

CREATE TABLE sec.RefreshTokens
(
    Id                   BIGINT IDENTITY(1,1) NOT NULL,
    UserId               INT                  NOT NULL,
    TokenHash            CHAR(64)             NOT NULL,
    CreatedAtUtc         DATETIME2(3)         NOT NULL CONSTRAINT DF_RefreshTokens_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    ExpiresAtUtc         DATETIME2(3)         NOT NULL,
    RevokedAtUtc         DATETIME2(3)         NULL,
    ReplacedByTokenHash  CHAR(64)             NULL,
    CreatedByIp          NVARCHAR(45)         NULL,
    CONSTRAINT PK_RefreshTokens PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_RefreshTokens_Users FOREIGN KEY (UserId) REFERENCES sec.Users (Id),
    CONSTRAINT UQ_RefreshTokens_TokenHash UNIQUE (TokenHash),
    CONSTRAINT CK_RefreshTokens_Expiration CHECK (ExpiresAtUtc > CreatedAtUtc)
);
