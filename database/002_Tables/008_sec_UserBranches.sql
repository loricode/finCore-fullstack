/*
    sec.UserBranches · Sucursales autorizadas para cada usuario.
    CompanyId participa en las dos llaves foráneas compuestas: así la base garantiza que el usuario
    y la sucursal pertenecen a la misma empresa.
*/
SET NOCOUNT ON;

CREATE TABLE sec.UserBranches
(
    UserId            INT          NOT NULL,
    BranchId          INT          NOT NULL,
    CompanyId         INT          NOT NULL,
    AssignedAtUtc     DATETIME2(3) NOT NULL CONSTRAINT DF_UserBranches_AssignedAtUtc DEFAULT (SYSUTCDATETIME()),
    AssignedByUserId  INT          NULL,
    CONSTRAINT PK_UserBranches PRIMARY KEY CLUSTERED (UserId, BranchId),
    CONSTRAINT FK_UserBranches_Users FOREIGN KEY (UserId, CompanyId) REFERENCES sec.Users (Id, CompanyId),
    CONSTRAINT FK_UserBranches_Branches FOREIGN KEY (BranchId, CompanyId) REFERENCES sec.Branches (Id, CompanyId),
    CONSTRAINT FK_UserBranches_AssignedBy FOREIGN KEY (AssignedByUserId) REFERENCES sec.Users (Id)
);
