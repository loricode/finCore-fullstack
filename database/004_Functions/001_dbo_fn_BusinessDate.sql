/*
    dbo.fn_BusinessDate · Convierte una fecha UTC en la fecha de negocio de Colombia.
    Colombia está en UTC-5 todo el año (sin horario de verano), por eso basta un DATEADD:
    es determinista y SQL Server puede incluirla en línea (scalar UDF inlining) en las consultas.
    Ejemplo: 2026-11-20 03:00 UTC  ->  2026-11-19.
*/
CREATE OR ALTER FUNCTION dbo.fn_BusinessDate (@Utc DATETIME2(3))
RETURNS DATE
WITH SCHEMABINDING
AS
BEGIN
    RETURN CAST(DATEADD(HOUR, -5, @Utc) AS DATE);
END;
