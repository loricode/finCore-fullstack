-- Execute SQL Task "Leer marca de agua" (conexión OLE DB a FinCore, ResultSet = Single row -> User::LastSequence)
SELECT ISNULL(MAX(LastValue), 0) AS LastSequence
FROM etl.Watermarks
WHERE SourceName = N'ORACLE:LEGACY_BANK.BANK_MOVEMENTS';
