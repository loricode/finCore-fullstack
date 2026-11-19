/* sec.UserRoles · Roles asignados a cada usuario. */
SET NOCOUNT ON;

CREATE TABLE sec.UserRoles
(
    UserId            INT          NOT NULL,
    RoleId            SMALLINT     NOT NULL,
    AssignedAtUtc     DATETIME2(3) NOT NULL CONSTRAINT DF_UserRoles_AssignedAtUtc DEFAULT (SYSUTCDATETIME()),
    AssignedByUserId  INT          NULL,
    CONSTRAINT PK_UserRoles PRIMARY KEY CLUSTERED (UserId, RoleId),
    CONSTRAINT FK_UserRoles_Users FOREIGN KEY (UserId) REFERENCES sec.Users (Id),
    CONSTRAINT FK_UserRoles_Roles FOREIGN KEY (RoleId) REFERENCES sec.Roles (Id),
    CONSTRAINT FK_UserRoles_AssignedBy FOREIGN KEY (AssignedByUserId) REFERENCES sec.Users (Id)
);
