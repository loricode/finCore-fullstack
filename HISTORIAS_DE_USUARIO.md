# Historias de usuario

Cada historia indica la prueba automatizada que la verifica. Estado: ✅ verificada · 🟡 base de datos lista, falta API/UI · ⬜ pendiente.
Las reglas citadas (RN, RC, RI) están en el [PRD](PRD.md) y el comportamiento detallado en [`openspec/`](openspec/project.md).
El detalle de lo hecho y lo pendiente, tarea por tarea, está en [`openspec/changes/`](openspec/project.md#tareas) (`[x]` / `[ ]`).

| Etapa | Historias |
|---|---|
| 1 · Fundamentos | HU-01 a HU-05 |
| 2 · Operaciones | HU-06 a HU-12 |
| 3 · Conciliación | HU-13 a HU-18 ✅ |
| 4 · Integraciones | HU-19 a HU-22 ✅ (falta el `.dtsx` de SSIS) |
| 5 · Reportería | HU-23 a HU-26 ✅ |
| 6 · Calidad y producción | HU-27 a HU-31 (HU-31 ✅ identidad en SSRS) |

---

## Módulo 1 · Seguridad y administración

**HU-01 · Iniciar sesión** ✅
Como usuario quiero iniciar sesión con usuario y contraseña para acceder solo a lo que me corresponde.
- *Dado* credenciales válidas, *cuando* inicio sesión, *entonces* recibo un access token de 15 min y un refresh token en una cookie HttpOnly, y se audita `LOGIN`.
- *Dado* una contraseña incorrecta, *entonces* recibo 401 sin indicar si el usuario existe y se audita `LOGIN_FAILED`.
- *Dado* 5 intentos fallidos seguidos, *entonces* la cuenta se bloquea 15 minutos (423).
- *Cuando* uso un refresh token, *entonces* se rota: el anterior queda revocado y reutilizarlo revoca toda la cadena.
- *Cuando* cierro sesión, *entonces* el refresh token queda revocado.
- *Cuando* recargo la página, *entonces* Angular continúa la sesión con la cookie sin pedir contraseña.
- Verifica: `UserTests`, `LoginHandlerTests`, `RefreshSessionHandlerTests`, `AuthApiTests` (integración), `auth.service.spec.ts`.

**HU-02 · Administrar usuarios** ✅
Como administrador quiero crear, editar y desactivar usuarios y asignarles roles.
- *Dado* un correo o usuario repetido, *entonces* recibo 409.
- *Cuando* desactivo un usuario, *entonces* sus refresh tokens se revocan y no puede iniciar sesión.
- *Dado* una contraseña débil, *entonces* recibo 400 con el error en `password` (10+ caracteres, mayúscula, minúscula, número y símbolo).
- *Dado* que edito con una versión vieja del registro, *entonces* recibo 409.
- *Dado* que intento desactivarme o quitarme el permiso de administrar usuarios, *entonces* recibo 409.
- *Entonces* cada cambio se audita con valores antes y después (sin contraseñas).
- Verifica: `UsersApiTests`, `UserAssignmentPolicyTests`; pantalla Usuarios.

**HU-03 · Administrar sucursales y cuentas** 🟡
Como administrador quiero registrar sucursales y sus cuentas bancarias.
- *Entonces* el código de sucursal y el número de cuenta bancaria son únicos por empresa.
- *Entonces* una cuenta pertenece a una sucursal de la misma empresa (llave foránea compuesta).
- Verifica: `test_00_static_checks.sql` (llaves confiables), `UQ_Accounts_Company_BankAccount`.

**HU-04 · Asignar sucursales a un usuario** ✅
Como administrador quiero asignar sucursales a un operador para limitar lo que ve y registra (RN-09).
- *Dado* el operador de Montería, *cuando* consulta, *entonces* solo ve Montería.
- *Dado* un usuario con `branches.all`, *entonces* ve todas las sucursales de **su** empresa y ninguna de otra.
- *Dado* un usuario inactivo o sin roles, *entonces* no ve ninguna sucursal.
- *Dado* un operador, *cuando* llama un endpoint de administración, *entonces* recibe 403 `application/problem+json`.
- *Dado* un rol sin `branches.all` y ninguna sucursal, *entonces* recibo 400 en `branchIds`.
- Verifica: `test_06_authorization_and_reports.sql`, `AuthApiTests.El_operador_de_Monteria_solo_ve_su_sucursal_y_no_administra_usuarios`, `UsersApiTests.Cambiar_sucursales_cambia_lo_que_ve_el_usuario_y_una_version_vieja_responde_409`.

**HU-05 · Auditoría de acciones** ✅
Como auditor quiero saber quién hizo cada cambio, cuándo, desde qué IP y con qué valores antes y después.
- *Entonces* cada alta, cambio, cambio de estado, login y exportación genera un registro en `audit.AuditLogs`.
- *Entonces* la auditoría no se puede modificar ni borrar, tampoco desde SQL.
- *Entonces* nunca contiene contraseñas, hashes ni tokens.
- Verifica: `test_05_append_only.sql`; los casos de uso registran la auditoría en la misma transacción (`TransactionsApiTests` comprueba `TRANSACTION_CREATED`, `AuthApiTests` comprueba `LOGIN`, `ReportsApiTests` comprueba `EXPORT` con su CorrelationId).

## Módulo 2 · Operaciones financieras

**HU-06 · Registrar una operación** ✅
Como operador quiero registrar ingresos, egresos, transferencias y pagos de mi sucursal.
- *Dado* datos que violan RN-01 a RN-04, *entonces* recibo 400 `application/problem+json` con el error de cada campo.
- *Cuando* se registra, *entonces* queda Pendiente, con `PublicId`, usuario responsable y fecha, y la respuesta es 201 con `Location`.
- *Dado* una sucursal no autorizada, *entonces* recibo 403.
- Verifica: `TransactionTests` (dominio), `TransactionsApiTests` (registro, 400 por campo, 403 por sucursal); pantalla Operaciones.

**HU-07 · No duplicar operaciones por reintentos** ✅
Como operador quiero que un doble clic o un corte de red no registre dos veces la misma operación (RN-11).
- *Dado* una `Idempotency-Key` ya usada con el mismo contenido, *entonces* recibo 200 con la operación original.
- *Dado* la misma clave con otro contenido, *entonces* recibo 422.
- Verifica: `TransactionsApiTests.Registrar_queda_pendiente_y_el_reintento_con_la_misma_clave_no_duplica`; Angular envía una clave por cada apertura del formulario.

**HU-08 · Consultar operaciones** ✅
Como operador o supervisor quiero filtrar por sucursal, estado, tipo, período y referencia, con paginación.
- *Entonces* solo aparecen operaciones de sucursales autorizadas.
- *Entonces* la consulta de un mes de una sucursal usa `IX_Transactions_Branch_OperationDate`.
- Verifica: `TransactionsApiTests.El_operador_de_Monteria_no_registra_ni_ve_operaciones_de_Bogota`. Pendiente: documentar el plan de ejecución (Etapa 6).

**HU-09 · Procesar o rechazar una operación pendiente** ✅
Como supervisor quiero procesar o rechazar operaciones pendientes (RN-05, RN-07, RN-08).
- *Dado* una operación de $10.000.000 o más, *cuando* la procesa quien la registró, *entonces* recibo 409.
- *Dado* un rechazo sin motivo, *entonces* recibo 400 (y la base lo impide con `CK_Transactions_Rejection`).
- *Dado* una rechazada, *cuando* intento procesarla, *entonces* recibo 409 (y la base lo impide: error 50012).
- Verifica: `TransactionTests`, `TransactionsApiTests` (doble control, rechazo con motivo), `test_04_transaction_guards.sql`.

**HU-10 · Revertir una operación** ✅
Como supervisor quiero revertir una operación procesada o conciliada para corregirla sin borrar nada (RN-07).
- *Cuando* revierto, *entonces* se crea una operación de tipo Reversión con el mismo valor y sentido contrario, y la original queda Revertida.
- *Entonces* el saldo de la cuenta y los totales de reportes no cambian por el par original + reversión.
- *Dado* una operación ya revertida, *entonces* no se puede revertir otra vez (`UX_Transactions_OriginalTransactionId`).
- Verifica: `TransactionTests`, `TransactionsApiTests.Procesar_y_revertir_deja_historial_con_usuario_y_correlacion`, `test_06_authorization_and_reports.sql` (saldo).

**HU-11 · Proteger operaciones cerradas** ✅
Como responsable financiero quiero que nadie pueda alterar una operación ya procesada, ni siquiera con un UPDATE en producción.
- *Cuando* se intenta cambiar valor, referencia, fecha, cuenta o tipo de una operación no pendiente, *entonces* falla con 50010.
- *Cuando* se intenta borrar una operación, *entonces* falla con 50011.
- *Cuando* una conciliada intenta pasar a un estado distinto de Revertida, *entonces* falla con 50012.
- Verifica: `test_04_transaction_guards.sql`.

**HU-12 · Historial de estados** ✅
Como soporte quiero ver cada cambio de estado de una operación con usuario, fecha y CorrelationId.
- *Entonces* lo registra la base de datos (trigger), venga el cambio de la API o del motor de conciliación.
- *Entonces* el historial es de solo inserción.
- Verifica: `test_04_transaction_guards.sql` (recorrido `->1 1>2 2>3 3>5`), `test_05_append_only.sql`.

## Módulo 3 · Conciliación bancaria

**HU-13 · Importar un extracto CSV** ✅
Como supervisor quiero cargar el extracto del banco y saber qué filas se rechazaron y por qué (RI-01 a RI-03).
- *Entonces* la respuesta indica leídas, cargadas y rechazadas, y leídas = cargadas + rechazadas.
- *Entonces* cada rechazo tiene fila y código: `MISSING_MOVEMENT_ID`, `ACCOUNT_NOT_FOUND`, `INVALID_REFERENCE`, `INVALID_DATE`, `INVALID_AMOUNT`, `DUPLICATE_IN_FILE`, `ALREADY_LOADED`.
- *Dado* el mismo archivo cargado dos veces, *entonces* no se duplica ningún movimiento.
- *Dado* un archivo sin el encabezado esperado o que no es .csv, *entonces* recibo 400 y no se abre ninguna carga.
- Verifica: `test_03_promote_bank_movements.sql`, `CsvStatementParserTests`, `ReconciliationApiTests` (importación y recarga); pestaña Extractos (arrastrar y soltar, plantilla, rechazos por línea).

**HU-14 · Conciliar automáticamente** ✅
Como supervisor quiero conciliar una cuenta en un período y obtener el resultado de cada operación y cada movimiento (RC-01 a RC-04).
- *Dado* misma referencia, valor y fecha dentro de la tolerancia, *entonces* la operación queda Conciliada y el movimiento MATCHED.
- *Dado* misma referencia con otro valor, *entonces* Diferencia de valor con la diferencia banco − interno.
- *Dado* misma referencia y valor con fecha fuera de la tolerancia, *entonces* Diferencia de fecha.
- *Entonces* se detectan operaciones que no están en el banco, movimientos que no están en el sistema, referencias duplicadas y operaciones pendientes.
- *Dado* una operación del último día del período que el banco registra al día siguiente, *entonces* se concilia.
- Verifica: `test_01_reconciliation_matching.sql`, `ReconciliationApiTests.Conciliar_Monteria_produce_los_siete_tipos_de_resultado_y_la_bandeja`; pestaña Conciliaciones.

**HU-15 · Repetir una conciliación** ✅
Como supervisor quiero volver a conciliar después de corregir datos sin dañar lo que ya estaba conciliado (RC-05).
- *Cuando* repito, *entonces* no se concilia nada dos veces y las diferencias se reevalúan.
- *Dado* un segundo cargo con una referencia ya conciliada, *entonces* se reporta como duplicada.
- Verifica: `test_01_reconciliation_matching.sql` (conciliación repetible).

**HU-16 · Una conciliación a la vez** ✅
Como supervisor quiero que dos personas no concilien la misma cuenta al mismo tiempo (RC-06).
- *Dado* una conciliación en curso, *cuando* inicio otra para la cuenta, *entonces* recibo 409 (error 50104).
- *Dado* una ejecución colgada hace más de 30 min, *entonces* se marca FAILED y no bloquea.
- Verifica: `test_02_reconciliation_validation.sql`.

**HU-17 · Bandeja de diferencias** ✅
Como supervisor quiero ver solo las diferencias abiertas de la última conciliación de cada cuenta.
- Verifica: vista `rec.vw_OpenDifferences`, `test_01_reconciliation_matching.sql`, `ReconciliationApiTests`; pestaña Bandeja (filtro por tipo).

**HU-18 · Resolver una diferencia** ✅
Como supervisor quiero marcar una diferencia como resuelta con una nota (RC-07).
- *Entonces* queda el usuario, la fecha y la nota; sin nota recibo 400 (y la base lo impide con `CK_ReconciliationDetails_Resolution`).
- Verifica: `ReconciliationDetailTests`, `ReconciliationApiTests` (400 sin nota, 204, 409 repetida o conciliada); se audita `RECONCILIATION_RESOLVED`.

## Módulo 4 · Integración de sistemas

**HU-19 · Recibir webhooks del proveedor bancario** ✅
Como plataforma quiero recibir notificaciones de movimientos en línea.
- *Dado* una firma HMAC inválida, *entonces* el adaptador Node.js responde 401 y no llama a la API.
- *Dado* un evento válido, *entonces* Node lo transforma al contrato de FinCore y lo envía a la API con su `ExternalEventId`; responde 202 al proveedor.
- *Dado* el mismo evento reenviado, *entonces* la API no crea otro movimiento (`UQ_InboundEvents_Provider_Event`).
- *Si* la API no responde, *entonces* Node reintenta con espera exponencial (1 s, 2 s, 4 s, 8 s, 16 s).
- *Si* la API se cae a mitad del proceso, *entonces* el Worker completa el evento (`InboundEventRecovery`).
- Verifica: 18 pruebas de `integrations/fincore-node` (firma, traducción, reintentos, dead letter, HTTP), `InboundEventTests`, `IntegrationApiTests` (procesado, duplicado, 409, 422, ignorado, 403); recorrido con `bank-simulator`.

**HU-20 · Importar movimientos desde Oracle** ✅ Worker · 🟡 paquete SSIS (`.dtsx` por construir en Visual Studio)
Como plataforma quiero traer los movimientos del sistema legado sin intervención manual.
- *Entonces* el paquete abre la ejecución (`etl.usp_StartExecution`), carga staging y llama a `stg.usp_PromoteBankMovements`.
- *Si* Oracle no responde, *entonces* la ejecución queda FAILED con el error (`etl.usp_FailExecution`).
- *Entonces* la extracción es incremental: solo lee lo posterior a la marca de agua (`etl.Watermarks`).
- Verifica: `IntegrationApiTests.La_extraccion_de_Oracle_es_incremental_por_marca_de_agua`, `test_03_promote_bank_movements.sql`; en Docker el Worker carga los 6 movimientos del Oracle legado (5 cargados, 1 rechazado) y luego solo los nuevos. SQL de cada tarea SSIS validado en `etl/FinCore.Integration.SSIS/sql`.

**HU-21 · Conciliación programada** ✅
Como supervisor quiero que cada noche se concilie el día anterior de cada cuenta activa.
- *Entonces* el Worker registra `ExecutedBy = 'fincore-worker'` y un CorrelationId por cuenta.
- Verifica: `IntegrationApiTests.La_conciliacion_programada_recorre_todas_las_cuentas_activas`, `JobScheduleTests` (hora de Colombia).

**HU-22 · Depurar staging** ✅
Como DBA quiero que staging no crezca sin control.
- *Entonces* el Worker borra por lotes las filas de ejecuciones terminadas hace más de 30 días; el historial ETL se conserva.
- Verifica: procedimiento `stg.usp_PurgeStaging`; trabajo `StagingPurge` del Worker (03:00).

## Módulo 5 · Reportes financieros

**HU-23 · Totales por sucursal** ✅
Como administrador quiero ingresos, egresos, neto y conteos por estado por sucursal en un período.
- *Entonces* pendientes, rechazadas y pares de reversión no inflan ingresos ni egresos.
- *Entonces* aparecen todas las sucursales autorizadas, aunque no tengan operaciones.
- *Entonces* puedo exportarlo a CSV (con `reports.export`) y la exportación queda auditada.
- Verifica: `test_06_authorization_and_reports.sql`; `ReportsApiTests`; página **Reportes**; informe SSRS *ResumenPorSucursal*.

**HU-24 · Reporte de conciliación** ✅
Como supervisor quiero el detalle de una conciliación agrupado por tipo de resultado, exportable.
- *Dado* una conciliación de una sucursal no autorizada, *entonces* falla con 50303.
- *Entonces* lo puedo descargar en PDF, Excel o Word desde Reporting Services.
- Verifica: `test_06_authorization_and_reports.sql` (50303); informe SSRS *Conciliacion* renderizado con datos reales.

**HU-25 · Operaciones por período** ✅
Como administrador quiero ver operaciones por día o por mes y por tipo.
- *Dado* una granularidad distinta de DAY/MONTH, *entonces* responde 400.
- Verifica: `ReportsApiTests`; gráfico de la página **Reportes** (`period-chart.component.spec.ts`); informe SSRS *OperacionesPorPeriodo*.

**HU-26 · Actividad por usuario** ✅
Como auditor quiero ver cuántas operaciones registró cada usuario, sus cambios de estado y su última acción.
- *Dado* un usuario sin `audit.read`, *entonces* falla con 50304.
- Verifica: `test_06_authorization_and_reports.sql`; `ReportsApiTests` (supervisor 403, auditor 200); informe SSRS *ActividadPorUsuario*.

**Indicadores financieros** (complemento de la Etapa 5): tarjetas de ingresos, egresos, neto, cobertura de conciliación, diferencias
abiertas y calidad de las cargas. Verifica: `test_07_indicators_and_lookups.sql`; informe SSRS *IndicadoresFinancieros*.

## Módulo 6 · Soporte a producción

**HU-27 · Seguir una petición de punta a punta** ✅
Como soporte quiero buscar por CorrelationId y ver los logs, la auditoría, el historial de estados y la conciliación de esa petición.
- *Entonces* la API acepta `X-Correlation-Id` o genera uno, lo devuelve en la respuesta, lo agrega a cada log JSON y lo fija en `SESSION_CONTEXT`.
- *Entonces* Angular envía un `X-Correlation-Id` nuevo en cada petición y lo muestra como "referencia de soporte" en los errores.
- Verifica: `AuthApiTests.El_CorrelationId_de_la_peticion_llega_a_la_respuesta_y_a_la_auditoria`; `test_01` (historial);
  `TransactionsApiTests` (historial de estados en el detalle de la operación).

**HU-28 · Errores controlados** ✅
Como consumidor de la API quiero errores consistentes.
- *Entonces* todo error devuelve `application/problem+json` con `traceId` y `correlationId`, sin trazas internas.
- *Entonces* los errores de negocio de SQL (50xxx) se traducen a 400/403/404/409 según el catálogo de `database/README.md`.
- Verifica: `AuthApiTests` (400 por campo, 401, 403, 423 en problem+json); `ReportsApiTests` (SQL 50304 → 403, período inválido → 400).

**HU-29 · Salud del sistema** ✅
Como operador de TI quiero saber si la API y la base responden.
- *Entonces* `/health/live` no depende de la base y `/health/ready` sí; Docker usa `/health/ready` como healthcheck de la API.
- Verifica: `AuthApiTests.Health_checks_responden_sin_autenticacion`, healthcheck de `docker-compose.yml`.

**HU-30 · Reconstruir la base desde cero** ✅
Como desarrollador quiero crear la base completa con un comando y saber qué scripts se aplicaron.
- *Cuando* ejecuto `database/deploy.ps1 -Recreate`, *entonces* se crea todo y queda registrado en `dbo.SchemaVersions` con checksum.
- *Dado* un script versionado ya aplicado que fue editado, *entonces* el despliegue se detiene.
- Verifica: `database/deploy.ps1`, `database/run-tests.ps1`.

**HU-31 · Cada usuario ve en Reporting Services solo lo suyo** ✅
Como administrador quiero asociar la cuenta de Windows de cada usuario para que, al abrir un informe en SSRS, vea los datos que su rol
y sus sucursales permiten, sin poder elegir a otra persona.
- *Dado* una cuenta de Windows asociada a la supervisora, *cuando* abre un informe, *entonces* "Ver como" solo ofrece a la supervisora.
- *Dado* esa misma cuenta, *cuando* pide `&UserId=1` por URL, *entonces* SSRS lo rechaza (`rsInvalidReportParameter`).
- *Dado* un usuario con `reports.viewas` (ADMIN), *entonces* puede ver como cualquier usuario de su empresa con `reports.read`.
- *Dado* una cuenta sin asociar, *entonces* el informe falla con 50305 y un mensaje que dice qué hacer.
- *Dado* una cuenta ya asociada a otro usuario, *entonces* la API responde 409; un formato distinto de `DOMINIO\usuario`, 400.
- Verifica: `test_08_report_identity.sql`; `UsersApiTests.Asociar_cuenta_de_Windows_para_Reporting_Services_se_valida_audita_y_no_se_repite`;
  prueba manual contra SSRS documentada en `docs/Deployment/ssrs.md`; `reporting/FinCore.Reports/demo-windows-users.ps1`.
