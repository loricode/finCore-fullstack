/**
 * Ajustes de la aplicación que dependen del entorno de despliegue.
 * SSRS corre en Windows (fuera de Docker): el portal está en el servidor de reportes, no detrás de nginx.
 */
export const APP_SETTINGS = {
  /** Portal web de SQL Server Reporting Services, carpeta de los informes de FinCore. */
  ssrsPortalUrl: 'http://localhost/Reports/browse/FinCore',
} as const;
