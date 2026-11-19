/*
    Catálogos de operaciones y conciliación. Script REPETIBLE: MERGE deja la tabla igual al archivo.
    Los Id son fijos y coinciden con los enums de FinCore.Domain: no se renumeran nunca.
    Los catálogos no se borran (las filas pueden estar referenciadas); solo se insertan o actualizan.
*/
SET NOCOUNT ON;

MERGE fin.TransactionTypes AS target
USING (VALUES
    (1, 'ING', N'Ingreso',       1,    0),
    (2, 'EGR', N'Egreso',        -1,   0),
    (3, 'TRF', N'Transferencia', -1,   1),
    (4, 'PAG', N'Pago',          -1,   1),
    (5, 'REV', N'Reversión',     NULL, 0)
) AS source (Id, Code, Name, DefaultDirection, RequiresCounterparty)
ON target.Id = source.Id
WHEN MATCHED THEN UPDATE SET
    Code = source.Code, Name = source.Name,
    DefaultDirection = source.DefaultDirection, RequiresCounterparty = source.RequiresCounterparty
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Id, Code, Name, DefaultDirection, RequiresCounterparty)
    VALUES (source.Id, source.Code, source.Name, source.DefaultDirection, source.RequiresCounterparty);

MERGE fin.TransactionStatus AS target
USING (VALUES
    (1, 'PENDING',    N'Pendiente',  0),
    (2, 'PROCESSED',  N'Procesada',  0),
    (3, 'RECONCILED', N'Conciliada', 0),   -- no es final: aún puede revertirse
    (4, 'REJECTED',   N'Rechazada',  1),
    (5, 'REVERSED',   N'Revertida',  1)
) AS source (Id, Code, Name, IsFinal)
ON target.Id = source.Id
WHEN MATCHED THEN UPDATE SET Code = source.Code, Name = source.Name, IsFinal = source.IsFinal
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Id, Code, Name, IsFinal) VALUES (source.Id, source.Code, source.Name, source.IsFinal);

MERGE rec.ReconciliationResultTypes AS target
USING (VALUES
    (1, 'MATCHED',             N'Conciliada',                  N'Referencia, valor y fecha coinciden.', 0),
    (2, 'AMOUNT_MISMATCH',     N'Diferencia de valor',         N'Misma referencia con valor distinto en el banco.', 1),
    (3, 'DATE_MISMATCH',       N'Diferencia de fecha',         N'Misma referencia y valor, fecha fuera de la tolerancia.', 1),
    (4, 'MISSING_IN_BANK',     N'No está en el banco',         N'Operación interna sin movimiento bancario.', 1),
    (5, 'MISSING_INTERNAL',    N'No está en el sistema',       N'Movimiento bancario sin operación interna.', 1),
    (6, 'DUPLICATE_REFERENCE', N'Referencia duplicada',        N'La referencia aparece en más de un movimiento bancario.', 1),
    (7, 'PENDING_REVIEW',      N'Pendiente de revisión',       N'Operación aún pendiente de procesar en el período.', 1)
) AS source (Id, Code, Name, Description, RequiresAction)
ON target.Id = source.Id
WHEN MATCHED THEN UPDATE SET
    Code = source.Code, Name = source.Name, Description = source.Description, RequiresAction = source.RequiresAction
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Id, Code, Name, Description, RequiresAction)
    VALUES (source.Id, source.Code, source.Name, source.Description, source.RequiresAction);
