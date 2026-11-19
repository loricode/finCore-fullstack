/*
    sec.tvf_UserPermissions · Permisos efectivos de un usuario activo (unión de los de sus roles).
    Función en línea (inline TVF): el optimizador la expande dentro de la consulta que la usa.
*/
CREATE OR ALTER FUNCTION sec.tvf_UserPermissions (@UserId INT)
RETURNS TABLE
AS
RETURN
(
    SELECT DISTINCT p.Id AS PermissionId, p.Code
    FROM sec.Users u
    JOIN sec.UserRoles ur       ON ur.UserId = u.Id
    JOIN sec.RolePermissions rp ON rp.RoleId = ur.RoleId
    JOIN sec.Permissions p      ON p.Id = rp.PermissionId
    WHERE u.Id = @UserId
      AND u.IsActive = 1
);
