/*
    rpt.usp_AssertCanReadReports · Verifica que el usuario esté activo y tenga el permiso reports.read.
    La llaman todos los procedimientos rpt.*: SSRS ejecuta con una cuenta de servicio, así que el
    usuario final llega como parámetro y la autorización se valida aquí, igual que en la API.

    Errores: 50300 sin permiso para consultar reportes · 50301 período inválido.
*/
CREATE OR ALTER PROCEDURE rpt.usp_AssertCanReadReports
    @UserId    INT,
    @DateFrom  DATE = NULL,
    @DateTo    DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM sec.tvf_UserPermissions(@UserId) WHERE Code = 'reports.read')
        THROW 50300, N'El usuario no tiene permiso para consultar reportes.', 1;

    IF (@DateFrom IS NOT NULL OR @DateTo IS NOT NULL)
       AND (@DateFrom IS NULL OR @DateTo IS NULL OR @DateFrom > @DateTo)
        THROW 50301, N'El período del reporte es inválido.', 1;
END;
