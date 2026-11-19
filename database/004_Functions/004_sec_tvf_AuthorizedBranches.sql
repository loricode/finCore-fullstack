/*
    sec.tvf_AuthorizedBranches · Sucursales activas que un usuario puede consultar.
      - Con el permiso branches.all: todas las sucursales de su empresa.
      - Sin él: solo las asignadas en sec.UserBranches.
    La usan los procedimientos rpt.* para que los reportes (API y SSRS) respeten el mismo alcance que la API.
*/
CREATE OR ALTER FUNCTION sec.tvf_AuthorizedBranches (@UserId INT)
RETURNS TABLE
AS
RETURN
(
    SELECT b.Id AS BranchId, b.CompanyId, b.Code, b.Name, b.City
    FROM sec.Users u
    JOIN sec.Branches b ON b.CompanyId = u.CompanyId
    WHERE u.Id = @UserId
      AND u.IsActive = 1
      AND b.IsActive = 1
      AND (
            EXISTS (SELECT 1 FROM sec.tvf_UserPermissions(u.Id) p WHERE p.Code = 'branches.all')
         OR EXISTS (SELECT 1 FROM sec.UserBranches ub WHERE ub.UserId = u.Id AND ub.BranchId = b.Id)
      )
);
