/*
    rpt.usp_UserActivity · Actividad por usuario de la empresa en un período (fechas de negocio).
    Requiere además el permiso audit.read, porque expone la actividad de otras personas.
*/
CREATE OR ALTER PROCEDURE rpt.usp_UserActivity
    @UserId    INT,
    @DateFrom  DATE,
    @DateTo    DATE
AS
BEGIN
    SET NOCOUNT ON;

    EXEC rpt.usp_AssertCanReadReports @UserId = @UserId, @DateFrom = @DateFrom, @DateTo = @DateTo;

    IF NOT EXISTS (SELECT 1 FROM sec.tvf_UserPermissions(@UserId) WHERE Code = 'audit.read')
        THROW 50304, N'El usuario no tiene permiso para consultar la actividad de otros usuarios.', 1;

    DECLARE @CompanyId INT = (SELECT CompanyId FROM sec.Users WHERE Id = @UserId);
    -- Rango UTC equivalente a las fechas de negocio de Colombia (UTC-5).
    DECLARE @FromUtc DATETIME2(3) = DATEADD(HOUR, 5, CAST(@DateFrom AS DATETIME2(3)));
    DECLARE @ToUtc   DATETIME2(3) = DATEADD(HOUR, 5, CAST(DATEADD(DAY, 1, @DateTo) AS DATETIME2(3)));

    SELECT
        u.Id                         AS UserId,
        u.UserName,
        u.FullName,
        u.IsActive,
        u.LastLoginAtUtc,
        ISNULL(ops.Created, 0)       AS OperationsCreated,
        ISNULL(ops.CreatedAmount, 0) AS AmountCreated,
        ISNULL(st.StatusChanges, 0)  AS StatusChanges,
        ISNULL(au.AuditActions, 0)   AS AuditActions,
        au.LastActionAtUtc
    FROM sec.Users u
    OUTER APPLY (
        SELECT COUNT(*) AS Created, CAST(SUM(t.Amount) AS DECIMAL(18,2)) AS CreatedAmount
        FROM fin.Transactions t
        WHERE t.CreatedByUserId = u.Id AND t.CreatedAtUtc >= @FromUtc AND t.CreatedAtUtc < @ToUtc
    ) ops
    OUTER APPLY (
        SELECT COUNT(*) AS StatusChanges
        FROM fin.TransactionStatusHistory h
        WHERE h.ChangedByUserId = u.Id AND h.FromStatusId IS NOT NULL
          AND h.ChangedAtUtc >= @FromUtc AND h.ChangedAtUtc < @ToUtc
    ) st
    OUTER APPLY (
        SELECT COUNT(*) AS AuditActions, MAX(l.OccurredAtUtc) AS LastActionAtUtc
        FROM audit.AuditLogs l
        WHERE l.UserId = u.Id AND l.OccurredAtUtc >= @FromUtc AND l.OccurredAtUtc < @ToUtc
    ) au
    WHERE u.CompanyId = @CompanyId
    ORDER BY ISNULL(ops.Created, 0) + ISNULL(au.AuditActions, 0) DESC, u.UserName;
END;
