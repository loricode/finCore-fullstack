// Narración por escena: la clave es el título del subtítulo o de la portada de cada video (grabar.js).
module.exports = {
  // 1 · Camino feliz
  'De la operación al reporte': 'Video uno: de la operación al reporte. Seguimos una consignación desde que se registra hasta que aparece en los reportes.',
  'Bienvenidos a FinCore': 'FinCore es una plataforma de operaciones y conciliación financiera para una empresa con varias sucursales.',
  'Entra el operador de Montería': 'Cada persona entra con su usuario y su rol. Empezamos con Carlos, el operador de la sucursal Montería.',
  'Registrar una consignación': 'Carlos registra una consignación: el tipo, la cuenta, la referencia, el valor y el cliente. La API valida cada campo antes de guardar.',
  'Queda pendiente': 'La operación queda pendiente. Nadie la da por buena sola: falta que alguien con permiso la procese.',
  'La supervisora la revisa': 'Ahora entra Laura, la supervisora. Ella ve las operaciones de todas las sucursales y busca la de Carlos.',
  'Procesar': 'Con un clic la procesa. Cada cambio de estado queda en el historial, con el usuario, la fecha y el identificador de correlación.',
  'El banco reporta el movimiento': 'Del otro lado está el banco. Laura carga el extracto en CSV, y cada línea se valida antes de entrar.',
  'Conciliar la cuenta': 'Ahora concilia la cuenta de Montería. El motor compara lo registrado en FinCore con lo que reportó el banco: referencia, valor y fecha.',
  'Operación conciliada': 'La operación de Carlos pasó a conciliada. El banco confirmó que la plata llegó, y el historial muestra los tres pasos.',
  'Y se ve en los reportes': 'Y todo se refleja en los reportes: ingresos, neto y cobertura de conciliación, con los mismos números que Reporting Services.',

  // 2 · Operaciones
  'Operaciones con control': 'Video dos: operaciones con control. Doble control, rechazos, reversiones y trazabilidad.',
  'Bandeja de operaciones': 'La bandeja de operaciones filtra por sucursal, estado, tipo, fechas y texto, con paginación desde el servidor.',
  'Doble control desde 10 millones': 'Desde diez millones de pesos aplica el doble control: quien registra un pago grande no puede procesarlo. Lo tiene que procesar otra persona.',
  'Un egreso que no corresponde': 'Laura registra también un egreso pequeño, que más adelante se rechazará.',
  'Otra persona lo procesa': 'Entra el administrador. Como es otra persona, sí puede procesar el pago grande.',
  'Rechazar con motivo': 'El egreso no tiene soporte, así que lo rechaza. El motivo es obligatorio y queda en el historial.',
  'Revertir, nunca borrar': 'Una operación procesada nunca se borra. Si hubo un error, se revierte con una operación espejo por el mismo valor.',
  'Trazabilidad completa': 'Cada paso queda con su usuario, su fecha, su motivo y el identificador de correlación, que lo une con los logs y la auditoría. Desde aquí se llega a la operación espejo.',

  // 3 · Conciliación
  'Conciliación bancaria': 'Video tres: conciliación bancaria. Extractos validados, motor de conciliación y bandeja de diferencias.',
  'Cargar el extracto del banco': 'El extracto del banco se carga en CSV. Hay una plantilla para descargar, y se aceptan varios formatos de fecha y de valor.',
  'Cada línea se valida': 'Cada línea se valida por separado. Las buenas entran, y las malas se rechazan con su motivo, sin detener la carga.',
  'Conciliar un período': 'Laura concilia Montería en la primera quincena de noviembre. Se puede repetir las veces que haga falta sin duplicar resultados.',
  'Siete tipos de resultado': 'El resultado se clasifica en siete tipos: conciliadas, diferencias de valor o de fecha, movimientos que faltan en el banco o en el sistema, duplicados y pendientes de revisión.',
  'Bandeja de diferencias': 'La bandeja muestra solo lo que requiere acción, y se puede filtrar por tipo.',
  'Resolver con nota': 'Cada diferencia se resuelve con una nota. Queda auditado quién la resolvió, cuándo y por qué.',

  // 4 · Reportes
  'Reportes y Reporting Services': 'Video cuatro: reportes. Indicadores en la aplicación e informes empresariales en Reporting Services.',
  'Indicadores del período': 'Marta, la auditora, ve los indicadores del período: ingresos, egresos, neto, cobertura de conciliación y calidad de las cargas.',
  'Entradas y salidas': 'El gráfico muestra entradas y salidas por día o por mes. Las reversiones no inflan los totales.',
  'Resumen por sucursal': 'El resumen por sucursal muestra el flujo, el neto y los estados de cada sucursal autorizada.',
  'Exportar queda auditado': 'Puede exportar a CSV, listo para Excel. Cada exportación queda registrada en la auditoría.',
  'Actividad por usuario': 'La actividad por usuario solo la ve quien tiene permiso de auditoría.',
  'Reporting Services': 'Los mismos procedimientos almacenados alimentan los informes de SQL Server Reporting Services, en PDF, Excel o Word.',
  'Cada quien ve lo suyo': 'Reporting Services reconoce la cuenta de Windows de quien abre el informe, y solo le ofrece ver como su propio usuario. El administrador puede ver como cualquiera.',
  'Indicadores financieros': 'Los indicadores financieros, con la misma paleta de la aplicación, listos para imprimir o enviar.',
  'Informe de conciliación': 'Y el detalle de una conciliación, con su cabecera y cada resultado.',

  // 5 · Seguridad
  'Seguridad y usuarios': 'Video cinco: seguridad y usuarios. Cada persona ve solo lo que le corresponde.',
  'El operador ve su sucursal': 'Carlos es operador de Montería. Su menú y sus datos se limitan a su sucursal, y eso lo decide la API, no la pantalla.',
  'Sin permiso, sin acceso': 'Si intenta entrar a la administración de usuarios, la aplicación se lo impide.',
  'Administrar usuarios': 'El administrador ve a todos los usuarios con sus roles, sus sucursales, su estado y su último ingreso.',
  'Crear un usuario': 'Para crear un usuario escribe sus datos, una contraseña segura, y elige sus roles y sus sucursales.',
  'Su cuenta de Windows para SSRS': 'También puede asociar su cuenta de Windows. Con ella abre los informes de Reporting Services y ve solo lo que su rol permite.',
  'Desactivar un acceso': 'Si alguien sale de la empresa, se desactiva su acceso: no puede entrar y sus sesiones se cierran. Todo queda en la auditoría. Gracias por su tiempo.',
};
