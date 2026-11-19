-- Execute SQL Task "Mover marca de agua" (OLE DB). Parámetro 0 Input User::MaxSequence (LARGE_INTEGER),
-- el mayor MOVEMENT_SEQ leído en el Data Flow (componente Aggregate o variable de un Script Component).
MERGE etl.Watermarks AS t
USING (SELECT N'ORACLE:LEGACY_BANK.BANK_MOVEMENTS' AS SourceName, CAST(? AS BIGINT) AS LastValue) AS s
   ON t.SourceName = s.SourceName
WHEN MATCHED AND s.LastValue > t.LastValue THEN UPDATE SET LastValue = s.LastValue, UpdatedAtUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (SourceName, LastValue) VALUES (s.SourceName, s.LastValue);
