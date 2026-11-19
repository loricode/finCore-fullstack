/*
    Tablas de solo inserción. La auditoría y el historial de estados son evidencia: ni la aplicación
    ni un usuario de base de datos con permisos de escritura pueden modificarlos o borrarlos.
    (Un DBA podría deshabilitar el trigger, y eso queda en el log de SQL Server Audit, fuera del alcance del MVP.)
*/
CREATE OR ALTER TRIGGER audit.trg_AuditLogs_AppendOnly
ON audit.AuditLogs
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 50090, N'La auditoría es de solo inserción: no se puede modificar ni eliminar.', 1;
END;
GO

CREATE OR ALTER TRIGGER fin.trg_TransactionStatusHistory_AppendOnly
ON fin.TransactionStatusHistory
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 50091, N'El historial de estados es de solo inserción: no se puede modificar ni eliminar.', 1;
END;
