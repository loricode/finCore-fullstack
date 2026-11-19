/*
    fin.trg_Transactions_StatusHistory · Registra la creación y cada cambio de estado de una operación.

    Quién: SESSION_CONTEXT('UserId') si la API lo fijó en la conexión; si no, CreatedByUserId (alta)
           o UpdatedByUserId (cambio).
    Correlación: SESSION_CONTEXT('CorrelationId'), que fija la API (interceptor de conexión de EF Core)
           o rec.usp_RunReconciliation, para unir este historial con los logs de la petición.
*/
CREATE OR ALTER TRIGGER fin.trg_Transactions_StatusHistory
ON fin.Transactions
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM inserted) RETURN;

    INSERT fin.TransactionStatusHistory (TransactionId, FromStatusId, ToStatusId, ChangedAtUtc, ChangedByUserId, CorrelationId, Reason)
    SELECT
        i.Id,
        d.StatusId,
        i.StatusId,
        SYSUTCDATETIME(),
        COALESCE(TRY_CAST(SESSION_CONTEXT(N'UserId') AS INT),
                 CASE WHEN d.Id IS NULL THEN i.CreatedByUserId ELSE i.UpdatedByUserId END),
        TRY_CAST(SESSION_CONTEXT(N'CorrelationId') AS NVARCHAR(64)),
        CASE WHEN i.StatusId = 4 THEN i.RejectionReason END
    FROM inserted i
    LEFT JOIN deleted d ON d.Id = i.Id
    WHERE d.Id IS NULL OR d.StatusId <> i.StatusId;
END;
