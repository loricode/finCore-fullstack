/*
    fin.tvf_AccountBalance · Saldo de una cuenta a una fecha.
    Cuentan las operaciones que movieron dinero: procesadas (2), conciliadas (3) y revertidas (5).
    Una reversión (tipo 5) tiene el signo contrario al de su original, así que el par suma cero.
    No cuentan las pendientes (1) ni las rechazadas (4).
*/
CREATE OR ALTER FUNCTION fin.tvf_AccountBalance (@AccountId INT, @AsOfDate DATE)
RETURNS TABLE
AS
RETURN
(
    SELECT
        @AccountId                                             AS AccountId,
        @AsOfDate                                              AS AsOfDate,
        CAST(ISNULL(SUM(t.SignedAmount), 0) AS DECIMAL(18,2))  AS Balance,
        COUNT_BIG(t.Id)                                        AS OperationCount
    FROM fin.Transactions t
    WHERE t.AccountId = @AccountId
      AND t.OperationDate <= @AsOfDate
      AND t.StatusId IN (2, 3, 5)
);
