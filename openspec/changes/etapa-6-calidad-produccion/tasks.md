# Tareas · Etapa 6 · Calidad y producción

## 1. Soporte a producción
- [x] 1.1 HU-27 · CorrelationId de punta a punta: respuesta, logs, auditoría, `SESSION_CONTEXT` e historial de estados
- [x] 1.2 HU-28 · Errores en `application/problem+json`; errores SQL 50xxx traducidos a HTTP (probado con 50304 → 403)
- [x] 1.3 HU-29 · Health checks sin autenticación y healthcheck en Docker
- [x] 1.4 HU-30 · Reconstruir la base desde cero con `deploy.ps1 -Recreate` y detección de scripts versionados editados

## 2. Identidad en Reporting Services (HU-31)
- [x] 2.1 Tabla `sec.UserWindowsAccounts` y permiso `reports.viewas` (rol ADMIN)
- [x] 2.2 `rpt.usp_ReportUsers @WindowsAccount`: solo el propio usuario, o todos con `reports.viewas`; errores 50305 y 50300
- [x] 2.3 `test_08_report_identity.sql`
- [x] 2.4 `PUT /api/v1/users/{id}/windows-account` auditado (400 formato, 409 cuenta de otro usuario) y prueba de integración
- [x] 2.5 Campo "Cuenta de Windows" en el formulario de usuarios y en la tabla
- [x] 2.6 Informes con "Ver como" desde `User!UserID`; verificado que SSRS rechaza otro `UserId` por URL
- [x] 2.7 `deploy-reports.ps1 -Browsers` (rol de lectura según el idioma de SSRS)
- [x] 2.8 `demo-windows-users.ps1` para los usuarios de ejemplo (requiere administrador)
- [ ] 2.9 Ejecutar `demo-windows-users.ps1` como administrador y probar el portal como `fc.supervisora` y `fc.auditora`

## 3. Pruebas
- [ ] 3.1 Pruebas de integración con el login de la aplicación (`fincore_app`), no con `sa`: habrían detectado el DELETE que faltaba en `sec.UserWindowsAccounts`
- [ ] 3.2 Pruebas de punta a punta del frontend (login, registrar y procesar una operación, conciliar)

## 4. Integración y entrega continuas
- [ ] 4.1 Pipeline que compile y ejecute `db-tests`, `api-tests`, `web-tests` y `node-tests` en cada PR
- [ ] 4.2 Publicar las imágenes de Docker versionadas al fusionar en la rama principal
- [ ] 4.3 Verificar en el pipeline que `build-rdl.mjs` no deja diferencias en los `.rdl` versionados

## 5. Monitoreo
- [ ] 5.1 Panel con logs JSON (por ejemplo Seq) filtrable por CorrelationId
- [ ] 5.2 Alertas: health checks caídos, ejecuciones ETL fallidas, eventos en dead letter

## 6. Presentación
- [x] 6.1 Grabador de videos sobre la app real (`docs/presentacion/video/grabar.js`): subtítulos, cursor, portada y narración con edge-tts
- [x] 6.2 Cinco videos: camino feliz, operaciones con control, conciliación, reportes y SSRS, seguridad y usuarios

## 7. Optimización
- [ ] 7.1 Datos de volumen (cientos de miles de operaciones) para medir la conciliación y los reportes
- [ ] 7.2 Revisar planes de ejecución e índices con esos datos y documentar los resultados
