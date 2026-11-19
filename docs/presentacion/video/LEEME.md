# Videos de FinCore

Graban la **aplicación real** (Docker, http://localhost:4300) en Microsoft Edge con subtítulos, cursor, narración y una portada por
video, y dejan un MP4 por tema en `docs/presentacion/videos/`.

| # | Video | Qué muestra | Archivo |
|---|---|---|---|
| 1 | De la operación al reporte (camino feliz) | El operador registra una consignación → la supervisora la procesa → carga el extracto del banco → concilia → la operación queda conciliada → reportes | `01-camino.mp4` |
| 2 | Operaciones con control | Filtros, doble control desde 10 millones, procesar por otra persona, rechazo con motivo, reversión con operación espejo, historial con CorrelationId | `02-operaciones.mp4` |
| 3 | Conciliación bancaria | Extracto CSV con rechazos por línea, conciliación de un período, siete tipos de resultado, bandeja de diferencias y resolución con nota | `03-conciliacion.mp4` |
| 4 | Reportes y Reporting Services | Indicadores, gráfico por día/mes, resumen por sucursal, exportación auditada, actividad por usuario e informes en SSRS | `04-reportes.mp4` |
| 5 | Seguridad y usuarios | El operador solo ve su sucursal, acceso denegado, crear usuario con roles, sucursales y cuenta de Windows, desactivar | `05-seguridad.mp4` |

## Requisitos

- `docker compose up -d` con los datos de demostración (los usuarios del modo **Demostración** del login).
- Microsoft Edge (cámbialo con `NAVEGADOR=chrome`).
- `playwright-core` y `ffmpeg-static`: en esta carpeta (`npm i playwright-core ffmpeg-static`) o en la carpeta de herramientas
  `%USERPROFILE%\OneDrive\Documentos\video` (cámbiala con `VIDEO_TOOLS`).
- Video 4: SSRS con los informes publicados (`reporting/FinCore.Reports/deploy-reports.ps1`) y tu cuenta de Windows asociada a un
  usuario con `reports.read` (pantalla Usuarios). Edge entra al portal con tu cuenta de Windows.
- Narración (opcional): `python -m venv tts` y `tts\Scripts\python -m pip install edge-tts`. Sin ella, el video queda con subtítulos.
  Voz por defecto `es-CO-SalomeNeural` (cámbiala con `VOZ_TTS`). El texto de cada escena está en `voz.js`.

## Uso

```sh
node grabar.js --prueba          # sin video: una captura por escena en ./capturas (revisión rápida del guion)
node grabar.js                   # los 5 videos
node grabar.js camino reportes   # solo algunos
```

Cada ejecución **escribe datos reales** en la base de demostración (operaciones, extractos, conciliaciones y un usuario nuevo) con
referencias únicas, así que se puede repetir. Para volver a los datos iniciales: `docker compose down -v` y `docker compose up -d`.

Si una escena falla, queda `error-<video>.png` con la pantalla en ese momento.
