/*
    Defensa en profundidad de las reglas irreversibles de fin.Transactions.
    Las reglas completas de transición viven en el dominio (.NET); estos triggers solo impiden que un error
    de código o un UPDATE manual en producción rompa lo que nunca debe romperse:

      50011  Las operaciones no se eliminan (se rechazan o se revierten).
             INSTEAD OF: se evalúa antes que las llaves foráneas, así el error es el de negocio y no un 547.
      50010  Después de Pendiente, los datos financieros de la operación no cambian
             (cuenta, sucursal, tipo, referencia, fecha, valor, sentido).
      50012  Rechazada y Revertida son estados finales; Conciliada solo puede pasar a Revertida.
*/
CREATE OR ALTER TRIGGER fin.trg_Transactions_PreventDelete
ON fin.Transactions
INSTEAD OF DELETE
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM deleted) RETURN;
    THROW 50011, N'Las operaciones financieras no se eliminan: se rechazan o se revierten.', 1;
END;
GO

CREATE OR ALTER TRIGGER fin.trg_Transactions_Guard
ON fin.Transactions
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM inserted) RETURN;

    IF EXISTS (
        SELECT 1
        FROM deleted d
        JOIN inserted i ON i.Id = d.Id
        WHERE d.StatusId <> 1
          AND (   i.AccountId         <> d.AccountId
               OR i.BranchId          <> d.BranchId
               OR i.TransactionTypeId <> d.TransactionTypeId
               OR i.Reference COLLATE Latin1_General_BIN2 <> d.Reference COLLATE Latin1_General_BIN2
               OR i.OperationDate     <> d.OperationDate
               OR i.Amount            <> d.Amount
               OR i.Direction         <> d.Direction))
        THROW 50010, N'La operación ya fue procesada: sus datos financieros no se pueden modificar. Use una reversión.', 1;

    IF EXISTS (
        SELECT 1
        FROM deleted d
        JOIN inserted i ON i.Id = d.Id
        WHERE i.StatusId <> d.StatusId
          AND (d.StatusId IN (4, 5) OR (d.StatusId = 3 AND i.StatusId <> 5)))
        THROW 50012, N'Transición de estado no permitida: la operación está cerrada.', 1;
END;
