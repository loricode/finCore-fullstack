/*
    Listas para los parámetros de los informes SSRS (valores válidos de los menús desplegables).
    El login de reportes solo tiene EXECUTE en el esquema rpt: no lee tablas directamente.
*/

/*
    Usuario con el que se ejecuta un informe (parámetro UserId, "Ver como").
    SSRS pasa la cuenta de Windows de quien abre el informe (User!UserID). La primera fila es siempre ese mismo usuario,
    así que es el valor por defecto. Con reports.viewas se agregan los demás usuarios de la empresa con reports.read.
    SSRS rechaza un UserId que no esté en esta lista, incluso si llega por URL: sin reports.viewas nadie ve como otro.
    Errores: 50305 cuenta sin asociar o usuario inactivo · 50300 el usuario asociado no tiene reports.read.
*/
CREATE OR ALTER PROCEDURE rpt.usp_ReportUsers
    @WindowsAccount NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @UserId INT, @CompanyId INT;
    SELECT @UserId = u.Id, @CompanyId = u.CompanyId
    FROM sec.UserWindowsAccounts w
    JOIN sec.Users u ON u.Id = w.UserId
    WHERE w.WindowsAccount = LTRIM(RTRIM(@WindowsAccount)) AND u.IsActive = 1;

    IF @UserId IS NULL
    BEGIN
        DECLARE @Message NVARCHAR(400) = CONCAT(N'La cuenta de Windows ', ISNULL(@WindowsAccount, N'(vacía)'),
            N' no está asociada a un usuario activo de FinCore. Un administrador puede asociarla en Usuarios.');
        THROW 50305, @Message, 1;
    END;

    EXEC rpt.usp_AssertCanReadReports @UserId = @UserId;

    DECLARE @CanViewAs BIT = CASE WHEN EXISTS (
        SELECT 1 FROM sec.tvf_UserPermissions(@UserId) WHERE Code = 'reports.viewas') THEN 1 ELSE 0 END;

    SELECT u.Id AS UserId,
           CONCAT(u.FullName, N' (', u.UserName, N')', CASE WHEN u.Id = @UserId THEN N' · yo' ELSE N'' END) AS Label
    FROM sec.Users u
    WHERE u.Id = @UserId
       OR (@CanViewAs = 1
           AND u.CompanyId = @CompanyId
           AND u.IsActive = 1
           AND EXISTS (SELECT 1 FROM sec.tvf_UserPermissions(u.Id) p WHERE p.Code = 'reports.read'))
    ORDER BY CASE WHEN u.Id = @UserId THEN 0 ELSE 1 END, u.FullName;
END;
GO

/* Sucursales autorizadas a un usuario (parámetro opcional "Sucursal"). */
CREATE OR ALTER PROCEDURE rpt.usp_ReportBranches
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(NULL AS INT) AS BranchId, N'Todas las sucursales' AS Label, 0 AS SortOrder
    UNION ALL
    SELECT BranchId, Name, 1 FROM sec.tvf_AuthorizedBranches(@UserId)
    ORDER BY SortOrder, Label;
END;
GO

/* Conciliaciones completadas de las cuentas autorizadas, la más reciente primero (parámetro "Conciliación"). */
CREATE OR ALTER PROCEDURE rpt.usp_ReportReconciliations
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (200)
        s.ReconciliationId,
        CONCAT(N'#', s.ReconciliationId, N' · ', s.AccountCode, N' · ',
               FORMAT(s.PeriodFrom, 'dd/MM/yyyy'), N' – ', FORMAT(s.PeriodTo, 'dd/MM/yyyy')) AS Label
    FROM rec.vw_ReconciliationSummary s
    JOIN sec.tvf_AuthorizedBranches(@UserId) ab ON ab.BranchId = s.BranchId
    WHERE s.Status = 'COMPLETED'
    ORDER BY s.ReconciliationId DESC;
END;
