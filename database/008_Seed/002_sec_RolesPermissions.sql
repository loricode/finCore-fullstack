/*
    Roles, permisos y su matriz. Script REPETIBLE.
    sec.RolePermissions sí se sincroniza por completo (WHEN NOT MATCHED BY SOURCE -> DELETE):
    quitar un permiso de un rol en este archivo lo quita en la base en el siguiente despliegue.
*/
SET NOCOUNT ON;

MERGE sec.Roles AS target
USING (VALUES
    (1, 'ADMIN',       N'Administrador',            N'Administra usuarios, sucursales y cuentas. Ve todo.'),
    (2, 'SUPERVISOR',  N'Supervisor de operaciones', N'Procesa, rechaza y revierte operaciones; concilia; consulta reportes.'),
    (3, 'OPERATOR',    N'Operador de sucursal',     N'Registra y consulta operaciones de sus sucursales.'),
    (4, 'AUDITOR',     N'Auditor',                  N'Solo lectura de operaciones, conciliaciones, reportes y auditoría.'),
    (5, 'INTEGRATION', N'Cuenta de integración',    N'Cuenta técnica del adaptador Node.js y del Worker.')
) AS source (Id, Code, Name, Description)
ON target.Id = source.Id
WHEN MATCHED THEN UPDATE SET Code = source.Code, Name = source.Name, Description = source.Description
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Id, Code, Name, Description) VALUES (source.Id, source.Code, source.Name, source.Description);

MERGE sec.Permissions AS target
USING (VALUES
    (1,  'users.manage',           N'Crear, editar y desactivar usuarios; asignar roles y sucursales.'),
    (2,  'branches.manage',        N'Crear y editar sucursales.'),
    (3,  'accounts.manage',        N'Crear y editar cuentas financieras.'),
    (4,  'branches.all',           N'Acceso a todas las sucursales de la empresa.'),
    (10, 'operations.create',      N'Registrar operaciones.'),
    (11, 'operations.read',        N'Consultar operaciones.'),
    (12, 'operations.process',     N'Procesar operaciones pendientes.'),
    (13, 'operations.reject',      N'Rechazar operaciones pendientes.'),
    (14, 'operations.reverse',     N'Revertir operaciones procesadas o conciliadas.'),
    (20, 'statements.import',      N'Importar extractos bancarios.'),
    (21, 'reconciliation.run',     N'Ejecutar la conciliación.'),
    (22, 'reconciliation.read',    N'Consultar conciliaciones y diferencias.'),
    (23, 'reconciliation.resolve', N'Resolver diferencias de conciliación.'),
    (30, 'reports.read',           N'Consultar reportes.'),
    (31, 'reports.export',         N'Exportar reportes.'),
    (32, 'reports.viewas',         N'En Reporting Services, ver los informes como otro usuario (soporte y demostración).'),
    (40, 'audit.read',             N'Consultar la auditoría y la actividad de usuarios.'),
    (50, 'integration.webhooks',   N'Enviar eventos de proveedores externos a la API.')
) AS source (Id, Code, Description)
ON target.Id = source.Id
WHEN MATCHED THEN UPDATE SET Code = source.Code, Description = source.Description
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Id, Code, Description) VALUES (source.Id, source.Code, source.Description);

WITH Matrix (RoleCode, PermissionCode) AS
(
    SELECT r.RoleCode, p.PermissionCode
    FROM (VALUES ('ADMIN')) r (RoleCode)
    CROSS JOIN (SELECT Code FROM sec.Permissions WHERE Code <> 'integration.webhooks') p (PermissionCode)
    UNION ALL
    SELECT RoleCode, PermissionCode
    FROM (VALUES
        ('SUPERVISOR', 'branches.all'),
        ('SUPERVISOR', 'operations.create'),
        ('SUPERVISOR', 'operations.read'),
        ('SUPERVISOR', 'operations.process'),
        ('SUPERVISOR', 'operations.reject'),
        ('SUPERVISOR', 'operations.reverse'),
        ('SUPERVISOR', 'statements.import'),
        ('SUPERVISOR', 'reconciliation.run'),
        ('SUPERVISOR', 'reconciliation.read'),
        ('SUPERVISOR', 'reconciliation.resolve'),
        ('SUPERVISOR', 'reports.read'),
        ('SUPERVISOR', 'reports.export'),
        ('OPERATOR',   'operations.create'),
        ('OPERATOR',   'operations.read'),
        ('AUDITOR',    'branches.all'),
        ('AUDITOR',    'operations.read'),
        ('AUDITOR',    'reconciliation.read'),
        ('AUDITOR',    'reports.read'),
        ('AUDITOR',    'reports.export'),
        ('AUDITOR',    'audit.read'),
        ('INTEGRATION','branches.all'),
        ('INTEGRATION','integration.webhooks'),
        ('INTEGRATION','statements.import'),
        ('INTEGRATION','reconciliation.run')
    ) x (RoleCode, PermissionCode)
)
MERGE sec.RolePermissions AS target
USING (
    SELECT r.Id AS RoleId, p.Id AS PermissionId
    FROM Matrix m
    JOIN sec.Roles r       ON r.Code = m.RoleCode
    JOIN sec.Permissions p ON p.Code = m.PermissionCode
) AS source
ON target.RoleId = source.RoleId AND target.PermissionId = source.PermissionId
WHEN NOT MATCHED BY TARGET THEN INSERT (RoleId, PermissionId) VALUES (source.RoleId, source.PermissionId)
WHEN NOT MATCHED BY SOURCE THEN DELETE;
