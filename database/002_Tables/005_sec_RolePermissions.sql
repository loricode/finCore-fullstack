/* sec.RolePermissions · Permisos concedidos a cada rol. */
SET NOCOUNT ON;

CREATE TABLE sec.RolePermissions
(
    RoleId        SMALLINT NOT NULL,
    PermissionId  SMALLINT NOT NULL,
    CONSTRAINT PK_RolePermissions PRIMARY KEY CLUSTERED (RoleId, PermissionId),
    CONSTRAINT FK_RolePermissions_Roles FOREIGN KEY (RoleId) REFERENCES sec.Roles (Id),
    CONSTRAINT FK_RolePermissions_Permissions FOREIGN KEY (PermissionId) REFERENCES sec.Permissions (Id)
);
