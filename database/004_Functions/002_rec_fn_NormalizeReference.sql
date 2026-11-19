/*
    rec.fn_NormalizeReference · Normaliza una referencia bancaria para poder compararla:
    quita espacios, la pasa a mayúsculas, convierte la cadena vacía en NULL y la recorta a 40 caracteres.
    Las referencias internas ya se guardan normalizadas (las normaliza el dominio).
*/
CREATE OR ALTER FUNCTION rec.fn_NormalizeReference (@Reference NVARCHAR(100))
RETURNS NVARCHAR(40)
WITH SCHEMABINDING
AS
BEGIN
    RETURN LEFT(NULLIF(UPPER(TRIM(@Reference)), N''), 40);
END;
