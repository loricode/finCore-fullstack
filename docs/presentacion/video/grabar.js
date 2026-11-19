// Graba videos de FinCore sobre la aplicación real (Docker en http://localhost:4300) y arma un MP4 por video.
// Uso: node grabar.js [video ...] [--prueba] [--sin-voz]
//   video: camino operaciones conciliacion reportes seguridad (sin nombres = todos)
//   --prueba: no graba; deja una captura por escena en ./capturas (sirve para revisar el guion rápido)
//   --sin-voz: solo subtítulos, aunque edge-tts esté instalado
// Requisitos: docker compose arriba (con datos demo), Microsoft Edge y, para el video de reportes, SSRS publicado.
const fs = require('fs'), path = require('path'), os = require('os'), crypto = require('crypto');
const { execFileSync, spawnSync } = require('child_process');

// Dependencias aquí (npm i playwright-core ffmpeg-static) o en la carpeta de herramientas de video (VIDEO_TOOLS).
const TOOLS = process.env.VIDEO_TOOLS || path.join(os.homedir(), 'OneDrive', 'Documentos', 'video');
const req = (m) => { try { return require(m); } catch { return require(path.join(TOOLS, 'node_modules', m)); } };
const { chromium } = req('playwright-core');
const FFMPEG = req('ffmpeg-static');
const VOZ = require('./voz.js');

const args = process.argv.slice(2);
const PRUEBA = args.includes('--prueba');
const SIN_VOZ = args.includes('--sin-voz');
const VOICE = process.env.VOZ_TTS || 'es-CO-SalomeNeural';
const APP = process.env.FINCORE_URL || 'http://localhost:4300';
const SSRS = process.env.SSRS_URL || 'http://localhost/Reports';
const W = 1440, H = 900;
const SALIDA = path.join(__dirname, '..', 'videos');
const CAPTURAS = path.join(__dirname, 'capturas');
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// ---------- Datos de cada ejecución (referencias únicas: el video se puede grabar varias veces) ----------
const hoy = new Date();
const dd = String(hoy.getDate()).padStart(2, '0'), mm = String(hoy.getMonth() + 1).padStart(2, '0'), yyyy = hoy.getFullYear();
const HOY = `${dd}/${mm}/${yyyy}`, HOY_ISO = `${yyyy}-${mm}-${dd}`;
const sufijo = String(Date.now()).slice(-4);
const REF = { ingreso: `ING-7${sufijo}`.slice(0, 8), pago: `PAG-8${sufijo}`.slice(0, 8), egreso: `EGR-9${sufijo}`.slice(0, 8) };
const USUARIO_NUEVO = `pedro.${sufijo}`;

// ---------- Voz (opcional) ----------
function buscarEdge() {
  if (SIN_VOZ) return null;
  if (process.env.EDGE_TTS) return [process.env.EDGE_TTS, []];
  const local = path.join(__dirname, 'tts', 'Scripts', 'edge-tts.exe');
  if (fs.existsSync(local)) return [local, []];
  for (const [c, a] of [['edge-tts', []], ['python', ['-m', 'edge_tts']], ['py', ['-m', 'edge_tts']]]) {
    if (spawnSync(c, [...a, '--help'], { stdio: 'ignore' }).status === 0) return [c, a];
  }
  return null;
}
const EDGE = buscarEdge();
const DIRVOZ = path.join(__dirname, 'voz');
function audio(texto) {
  fs.mkdirSync(DIRVOZ, { recursive: true });
  const f = path.join(DIRVOZ, crypto.createHash('sha1').update(VOICE + texto).digest('hex').slice(0, 16) + '.mp3');
  if (!fs.existsSync(f)) execFileSync(EDGE[0], [...EDGE[1], '--voice', VOICE, '--rate', '+4%', '--text', texto, '--write-media', f], { stdio: 'ignore' });
  const r = spawnSync(FFMPEG, ['-i', f], { encoding: 'utf8' });
  const m = (r.stderr || '').match(/Duration: (\d+):(\d+):([\d.]+)/);
  return { f, dur: m ? (+m[1]) * 3600 + (+m[2]) * 60 + parseFloat(m[3]) : 3 };
}
const lectura = (txt) => Math.max(3200, String(txt).split(/\s+/).length * 330);

// ---------- Capa de presentación: subtítulo, cursor, clic y portada con la paleta de FinCore ----------
const OVERLAY = `(() => {
  if (document.getElementById('vx')) return;
  const css = document.createElement('style');
  css.textContent = \`
  #vx-cap{position:fixed;left:276px;right:28px;bottom:22px;z-index:2147483600;background:rgba(23,74,76,.96);color:#fff;border-radius:18px;
    padding:14px 20px 14px 16px;display:flex;gap:16px;align-items:center;box-shadow:0 14px 34px rgba(23,40,38,.35);
    font-family:'Nunito Variable',system-ui,sans-serif;transition:opacity .35s, transform .35s;pointer-events:none}
  #vx-cap.wide{left:28px}
  #vx-cap.off{opacity:0;transform:translateY(12px)}
  #vx-n{flex:none;width:46px;height:46px;border-radius:50%;background:#F6E8CF;color:#7A5A26;display:grid;place-items:center;font:900 19px 'Nunito Variable',sans-serif}
  #vx-t{font:800 20px/1.25 'Nunito Variable',sans-serif}
  #vx-d{font:600 16px/1.4 'Nunito Variable',sans-serif;color:rgba(255,255,255,.88);margin-top:2px}
  #vx-role{margin-left:auto;flex:none;font:800 13px 'Nunito Variable',sans-serif;background:#E2EBDC;color:#2F4A2A;border-radius:999px;padding:4px 12px}
  #vx-cur{position:fixed;left:0;top:0;z-index:2147483647;width:26px;height:26px;pointer-events:none;transition:left .65s cubic-bezier(.3,.7,.3,1), top .65s cubic-bezier(.3,.7,.3,1);filter:drop-shadow(0 2px 3px rgba(0,0,0,.4))}
  .vx-rip{position:fixed;z-index:2147483646;width:44px;height:44px;margin:-22px 0 0 -22px;border-radius:50%;border:3px solid #B88A47;pointer-events:none;animation:vxr .55s ease-out forwards}
  @keyframes vxr{from{transform:scale(.3);opacity:1}to{transform:scale(1.4);opacity:0}}
  #vx-card{position:fixed;inset:0;z-index:2147483645;display:grid;place-items:center;background:linear-gradient(135deg,#D6EBE8 0%,#F4EEE5 55%,#F8E0D8 100%);color:#174A4C;text-align:center;font-family:'Nunito Variable',sans-serif;transition:opacity .6s}
  #vx-card.off{opacity:0;pointer-events:none}
  #vx-card h1{font:600 60px/1.15 'Fraunces Variable',Georgia,serif;margin:0 0 16px}
  #vx-card p{font:600 23px/1.5 'Nunito Variable',sans-serif;margin:0 auto;max-width:900px;color:#3A3632}
  #vx-card small{display:inline-block;margin-top:30px;font:800 15px 'Nunito Variable',sans-serif;letter-spacing:.12em;text-transform:uppercase;background:#F6E8CF;color:#7A5A26;border-radius:999px;padding:6px 16px}
  .cdk-global-overlay-wrapper:has(.mat-mdc-snack-bar-container){align-items:flex-start!important;padding-top:84px}\`;
  document.head.appendChild(css);
  const root = document.createElement('div'); root.id = 'vx';
  root.innerHTML = '<div id="vx-cap" class="off"><div id="vx-n"></div><div><div id="vx-t"></div><div id="vx-d"></div></div><div id="vx-role"></div></div>'
    + '<svg id="vx-cur" viewBox="0 0 24 24"><path d="M4 2l16 9-7 2-3 7z" fill="#fff" stroke="#174A4C" stroke-width="1.6" stroke-linejoin="round"/></svg>'
    + '<div id="vx-card" class="off"><div><h1 id="vx-ch"></h1><p id="vx-cp"></p><small id="vx-cs"></small></div></div>';
  document.body.appendChild(root);
  const s = window.__vx || {};
  const cur = document.getElementById('vx-cur'); cur.style.left = (s.x ?? 720) + 'px'; cur.style.top = (s.y ?? 450) + 'px';
  if (s.cap) { const c = document.getElementById('vx-cap'); c.classList.remove('off'); c.classList.toggle('wide', !!s.cap.wide); for (const k of ['n','t','d','role']) document.getElementById('vx-' + k).textContent = s.cap[k] || ''; }
})()`;

// ---------- Guiones ----------
const VIDEOS = [
  {
    id: 'camino', n: 1, titulo: 'De la operación al reporte',
    sub: 'El camino feliz: una consignación se registra, se aprueba, se concilia con el banco y aparece en los reportes',
    run: async (v) => {
      await v.cap(1, 'Bienvenidos a FinCore', 'Operaciones y conciliación financiera para una empresa con varias sucursales.', '', true);
      await v.apuntar('mat-card, .login-card, form'); await v.hold(1600);
      await v.login('Carlos', 2, 'Entra el operador de Montería', 'Cada persona entra con su rol; aquí usamos los usuarios de demostración.', 'Operador · Montería');
      await v.cap(3, 'Registrar una consignación', 'Tipo, cuenta, referencia, fecha y valor. La API valida cada campo.', 'Operador · Montería');
      await v.nav('Operaciones');
      await v.clic('button:has-text("Nueva operación")', 1200);
      await v.clic('mat-button-toggle:has-text("Ingreso")', 600);
      await v.elegir('accountId', 'Recaudo Montería');
      await v.escribir('input[formcontrolname="reference"]', REF.ingreso);
      await v.escribir('input[formcontrolname="amount"]', '4250000');
      await v.escribir('textarea[formcontrolname="description"]', 'Consignación de Ganadería La Esperanza, factura 1187', 600, 30);
      await v.clic('mat-dialog-actions button:has-text("Registrar")', 2200);
      await v.cap(4, 'Queda pendiente', 'Nadie la da por buena sola: falta que alguien con permiso la procese.', 'Operador · Montería');
      await v.apuntar(`tr:has-text("${REF.ingreso}")`); await v.hold(1800);

      await v.login('Laura', 5, 'La supervisora la revisa', 'Ve las operaciones de todas las sucursales.', 'Supervisora');
      await v.nav('Operaciones');
      await v.escribir('input[formcontrolname="search"]', REF.ingreso, 1600);
      await v.clic(`tr:has-text("${REF.ingreso}")`, 1800);
      await v.cap(6, 'Procesar', 'Con un clic queda procesada. Cada cambio de estado queda en el historial con su usuario.', 'Supervisora');
      await v.clic('mat-dialog-actions button:has-text("Procesar")', 2000);
      await v.apuntar('.timeline'); await v.hold(1800);
      await v.clic('mat-dialog-actions button:has-text("Cerrar")', 900);

      await v.cap(7, 'El banco reporta el movimiento', 'Se carga el extracto del banco en CSV; cada línea se valida antes de entrar.', 'Supervisora');
      await v.nav('Conciliación');
      await v.clic('[role=tab]:has-text("Extractos")', 1200);
      await v.subirCsv('button:has-text("Elegir archivo")', 'extracto-monteria-hoy.csv',
        'account_number;movement_id;reference;date;amount;description\n' +
        `123-456789-01;MON-${sufijo}-0;${REF.ingreso};${HOY_ISO};4250000.00;CONSIGNACION GANADERIA LA ESPERANZA\n`, 2400);
      await v.apuntar('.stats'); await v.hold(1600);

      await v.cap(8, 'Conciliar la cuenta', 'El motor compara FinCore con el banco: referencia, valor y fecha.', 'Supervisora');
      await v.clic('[role=tab]:has-text("Conciliaciones")', 1200);
      await v.clic('button:has-text("Nueva conciliación")', 1200);
      await v.elegir('accountId', 'Recaudo Montería');
      await v.fecha('input[formcontrolname="from"]', HOY);
      await v.fecha('input[formcontrolname="to"]', HOY);
      await v.clic('mat-dialog-actions button:has-text("Conciliar")', 2600);
      await v.apuntar('.counts'); await v.hold(2200);
      await v.page.keyboard.press('Escape'); await v.hold(800);

      await v.cap(9, 'Operación conciliada', 'La operación pasó a conciliada: el banco confirmó la plata.', 'Supervisora');
      await v.nav('Operaciones');
      await v.escribir('input[formcontrolname="search"]', REF.ingreso, 1600);
      await v.clic(`tr:has-text("${REF.ingreso}")`, 1800);
      await v.apuntar('.timeline'); await v.hold(2200);
      await v.clic('mat-dialog-actions button:has-text("Cerrar")', 900);

      await v.cap(10, 'Y se ve en los reportes', 'Ingresos, neto y cobertura de conciliación, con los mismos números que Reporting Services.', 'Supervisora');
      await v.nav('Reportes');
      await v.apuntar('.kpi--ring'); await v.hold(2600);
    },
  },

  {
    id: 'operaciones', n: 2, titulo: 'Operaciones con control',
    sub: 'Doble control para montos altos, rechazo con motivo, reversión y trazabilidad completa',
    run: async (v) => {
      await v.login('Laura', 1, 'Bandeja de operaciones', 'Filtros por sucursal, estado, tipo, fechas y texto, con paginación del servidor.', 'Supervisora');
      await v.nav('Operaciones');
      await v.elegir('status', 'Pendiente', 1600);
      await v.clic('button:has-text("Limpiar")', 1200);

      await v.cap(2, 'Doble control desde 10 millones', 'Quien registra un pago grande no puede procesarlo: lo procesa otra persona.', 'Supervisora');
      await v.nuevaOperacion('Pago', 'Recaudo Montería', REF.pago, '12800000', 'Arrendamientos del Sinú S.A.S.', 'Canon de arrendamiento noviembre');
      await v.clic(`tr:has-text("${REF.pago}")`, 1800);
      await v.apuntar('.note'); await v.hold(2200);
      await v.clic('mat-dialog-actions button:has-text("Cerrar")', 700);
      await v.cap(3, 'Un egreso que no corresponde', 'La supervisora registra un egreso pequeño que luego se rechazará.', 'Supervisora');
      await v.nuevaOperacion('Egreso', 'Recaudo Montería', REF.egreso, '350000', 'Papelería El Centro', 'Compra de papelería');

      await v.login('Administrador', 4, 'Otra persona lo procesa', 'El administrador revisa el pago grande y lo procesa.', 'Administrador');
      await v.nav('Operaciones');
      await v.escribir('input[formcontrolname="search"]', REF.pago, 1600);
      await v.clic(`tr:has-text("${REF.pago}")`, 1600);
      await v.clic('mat-dialog-actions button:has-text("Procesar")', 2200);
      await v.clic('mat-dialog-actions button:has-text("Cerrar")', 700);

      await v.cap(5, 'Rechazar con motivo', 'El motivo es obligatorio y queda en el historial.', 'Administrador');
      await v.escribir('input[formcontrolname="search"]', REF.egreso, 1600);
      await v.clic(`tr:has-text("${REF.egreso}")`, 1600);
      await v.clic('mat-dialog-actions button:has-text("Rechazar")', 900);
      await v.escribir('mat-dialog-container textarea', 'No hay soporte de la compra. Registrar con la factura.', 700, 25);
      await v.clic('mat-dialog-actions button:has-text("Rechazar") >> nth=-1', 2000);
      await v.clic('mat-dialog-actions button:has-text("Cerrar")', 700);

      await v.cap(6, 'Revertir, nunca borrar', 'Una operación procesada no se borra: se revierte con una operación espejo.', 'Administrador');
      await v.escribir('input[formcontrolname="search"]', REF.pago, 1600);
      await v.clic(`tr:has-text("${REF.pago}")`, 1600);
      await v.clic('mat-dialog-actions button:has-text("Revertir")', 900);
      await v.escribir('mat-dialog-container textarea', 'El canon se pagó dos veces; se revierte este registro.', 700, 25);
      await v.clic('mat-dialog-actions button:has-text("Revertir") >> nth=-1', 2400);

      await v.cap(7, 'Trazabilidad completa', 'Cada paso con usuario, fecha, motivo y el id de correlación que lo une con los logs y la auditoría.', 'Administrador');
      await v.apuntar('.timeline .corr'); await v.hold(2400);
      await v.clic('.link-card', 2000);
      await v.apuntar('.timeline'); await v.hold(1800);
      await v.page.keyboard.press('Escape'); await v.hold(600);
    },
  },

  {
    id: 'conciliacion', n: 3, titulo: 'Conciliación bancaria',
    sub: 'Extractos validados línea por línea, motor de conciliación y bandeja de diferencias',
    run: async (v) => {
      await v.login('Laura', 1, 'Cargar el extracto del banco', 'CSV con plantilla descargable; acepta varios formatos de fecha y de valor.', 'Supervisora');
      await v.nav('Conciliación');
      await v.clic('[role=tab]:has-text("Extractos")', 1200);
      await v.apuntar('button:has-text("Plantilla")'); await v.hold(900);
      await v.cap(2, 'Cada línea se valida', 'Las buenas entran; las malas se rechazan con su motivo, sin detener la carga.', 'Supervisora');
      await v.subirCsv('button:has-text("Elegir archivo")', 'extracto-monteria.csv',
        'account_number;movement_id;reference;date;amount;description\n' +
        `123-456789-01;MON-${sufijo}-1;ING-${sufijo};${HOY_ISO};980000.00;CONSIGNACION\n` +
        `123-456789-01;MON-${sufijo}-2;EGR-${sufijo};31/02/2026;-120000;FECHA QUE NO EXISTE\n` +
        `123-456789-01;MON-${sufijo}-3;PAG-${sufijo};${HOY_ISO};diez mil;VALOR NO NUMERICO\n` +
        `999-999999-99;MON-${sufijo}-4;ING-${sufijo}9;${HOY_ISO};50000;CUENTA QUE NO EXISTE\n`, 2400);
      await v.apuntar('.stats'); await v.hold(1400);
      await v.clic('tr.fc-row-clickable >> nth=0', 1800);
      await v.apuntar('.errors, h3:has-text("Rechazos")'); await v.hold(2400);

      await v.cap(3, 'Conciliar un período', 'Montería, primera quincena de noviembre. Se puede repetir sin duplicar resultados.', 'Supervisora');
      await v.clic('[role=tab]:has-text("Conciliaciones")', 1200);
      await v.clic('button:has-text("Nueva conciliación")', 1200);
      await v.elegir('accountId', 'Recaudo Montería');
      await v.fecha('input[formcontrolname="from"]', '01/11/2026');
      await v.fecha('input[formcontrolname="to"]', '15/11/2026');
      await v.clic('mat-dialog-actions button:has-text("Conciliar")', 2600);
      await v.cap(4, 'Siete tipos de resultado', 'Conciliadas, diferencias de valor y de fecha, faltantes de cada lado, duplicados y pendientes.', 'Supervisora');
      await v.apuntar('.counts'); await v.hold(2600);
      await v.page.keyboard.press('Escape'); await v.hold(800);

      await v.cap(5, 'Bandeja de diferencias', 'Solo lo que requiere acción, filtrable por tipo.', 'Supervisora');
      await v.clic('[role=tab]:has-text("Bandeja")', 1400);
      await v.apuntar('table.fc-table'); await v.hold(1400);
      await v.cap(6, 'Resolver con nota', 'La resolución queda auditada: quién, cuándo y por qué.', 'Supervisora');
      await v.clic('button:has-text("Resolver") >> nth=0', 900);
      await v.escribir('mat-dialog-container textarea', 'Revisado con el banco: comisión cobrada el día 10. Se registra el ajuste.', 700, 22);
      await v.clic('mat-dialog-actions button:has-text("Marcar como resuelta")', 2200);
    },
  },

  {
    id: 'reportes', n: 4, titulo: 'Reportes y Reporting Services',
    sub: 'Indicadores en la aplicación, exportación auditada e informes empresariales en SSRS',
    run: async (v) => {
      await v.login('Marta', 1, 'Indicadores del período', 'Ingresos, egresos, neto, cobertura de conciliación y calidad de las cargas.', 'Auditora');
      await v.nav('Reportes');
      await v.apuntar('.kpis'); await v.hold(1600);
      await v.cap(2, 'Entradas y salidas', 'Por día o por mes; las reversiones no inflan los totales.', 'Auditora');
      await v.apuntar('fc-period-chart'); await v.hold(1000);
      await v.clic('mat-button-toggle:has-text("Por mes")', 1800);
      await v.clic('mat-button-toggle:has-text("Por día")', 1000);
      await v.cap(3, 'Resumen por sucursal', 'Flujo, neto y estados de cada sucursal autorizada.', 'Auditora');
      await v.apuntar('fc-reports-page .fc-table-card >> nth=0'); await v.hold(1600);
      await v.cap(4, 'Exportar queda auditado', 'CSV listo para Excel; cada exportación se registra con su id de correlación.', 'Auditora');
      await v.clic('button:has-text("Exportar CSV") >> nth=0', 2400);
      await v.cap(5, 'Actividad por usuario', 'Solo quien tiene permiso de auditoría la ve.', 'Auditora');
      await v.apuntar('fc-reports-page .fc-table-card >> nth=1'); await v.hold(2000);

      await v.cap(6, 'Reporting Services', 'Los mismos procedimientos alimentan los informes empresariales en PDF, Excel y Word.', 'Administrador', true);
      await v.ir(`${SSRS}/browse/FinCore`, 4000);
      await v.cap(7, 'Cada quien ve lo suyo', 'SSRS reconoce la cuenta de Windows y solo ofrece "Ver como" ese usuario, salvo al administrador.', 'Administrador', true);
      await v.ir(`${SSRS}/report/FinCore/ResumenPorSucursal`, 4500, 'Generado el');
      await v.cap(8, 'Indicadores financieros', 'Tarjetas con la paleta de la aplicación, listas para imprimir o enviar.', 'Administrador', true);
      await v.ir(`${SSRS}/report/FinCore/IndicadoresFinancieros`, 4500, 'Generado el');
      await v.cap(9, 'Informe de conciliación', 'El detalle de una conciliación, con su cabecera y cada resultado.', 'Administrador', true);
      await v.ir(`${SSRS}/report/FinCore/Conciliacion`, 4500, 'Generado el');
    },
  },

  {
    id: 'seguridad', n: 5, titulo: 'Seguridad y usuarios',
    sub: 'Roles, permisos y alcance por sucursal: cada persona ve solo lo que le corresponde',
    run: async (v) => {
      await v.login('Carlos', 1, 'El operador ve su sucursal', 'Su menú y sus datos se limitan a Montería; la API lo decide, no la pantalla.', 'Operador · Montería');
      await v.nav('Operaciones');
      await v.apuntar('table.fc-table'); await v.hold(1600);
      await v.cap(2, 'Sin permiso, sin acceso', 'Si intenta entrar a la administración de usuarios, la aplicación se lo impide.', 'Operador · Montería');
      await v.ir(`${APP}/usuarios`, 2600);

      await v.login('Administrador', 3, 'Administrar usuarios', 'Roles, sucursales, estado y último ingreso de cada persona.', 'Administrador');
      await v.nav('Usuarios');
      await v.apuntar('table.fc-table'); await v.hold(1400);
      await v.cap(4, 'Crear un usuario', 'Usuario, correo, contraseña segura, roles y sucursales.', 'Administrador');
      await v.clic('button:has-text("Nuevo usuario")', 1200);
      await v.escribir('input[formcontrolname="userName"]', USUARIO_NUEVO);
      await v.escribir('input[formcontrolname="fullName"]', 'Pedro Contador');
      await v.escribir('input[formcontrolname="email"]', `${USUARIO_NUEVO}@fincore.demo`);
      await v.escribir('input[formcontrolname="password"]', 'Contador*2026x', 500, 40);
      await v.clic('button.role:has-text("Operador de sucursal")', 700);
      await v.clic('mat-chip-option:has-text("Sucursal Medellín")', 900);
      await v.cap(5, 'Su cuenta de Windows para SSRS', 'Con ella abre los informes de Reporting Services y ve solo lo que su rol permite.', 'Administrador');
      await v.escribir('input[formcontrolname="windowsAccount"]', `EMPRESA\\${USUARIO_NUEVO}`, 900);
      await v.clic('mat-dialog-actions button:has-text("Crear usuario")', 2400);
      await v.apuntar(`tr:has-text("${USUARIO_NUEVO}")`); await v.hold(1800);
      await v.cap(6, 'Desactivar un acceso', 'Al desactivarlo no puede entrar y sus sesiones se cierran. Todo queda en la auditoría.', 'Administrador');
      await v.clic(`tr:has-text("${USUARIO_NUEVO}") button >> nth=-1`, 900);
      await v.clic('[role=menuitem]:has-text("Desactivar")', 1200);
      const confirmar = v.page.locator('mat-dialog-actions button:has-text("Desactivar")');
      if (await confirmar.count()) await v.clic('mat-dialog-actions button:has-text("Desactivar")', 1800);
      await v.apuntar(`tr:has-text("${USUARIO_NUEVO}")`); await v.hold(2000);
    },
  },
];

// ---------- Grabación de un video ----------
async function grabar(vid, browser) {
  const TMP = path.join(__dirname, 'frames', vid.id);
  fs.rmSync(TMP, { recursive: true, force: true }); fs.mkdirSync(TMP, { recursive: true });
  const conVoz = !!EDGE && !PRUEBA;
  const narracion = []; let vozFin = 0, wall0 = 0;
  const esperarVoz = async () => { if (PRUEBA) return; const ms = vozFin - Date.now(); if (ms > 0) await sleep(ms); };
  const hablar = (t, d) => {
    if (PRUEBA) return;
    const texto = VOZ[t];
    if (conVoz && texto) { const a = audio(texto); narracion.push({ f: a.f, t: Date.now() }); vozFin = Date.now() + a.dur * 1000 + 450; }
    else vozFin = Date.now() + lectura(t + ' ' + (d || ''));
  };

  const context = await browser.newContext({ viewport: { width: W, height: H }, deviceScaleFactor: 1, locale: 'es-CO', acceptDownloads: true });
  if (vid.id === 'reportes' && !PRUEBA) {
    // Precalienta SSRS en una pestaña que no se graba: el primer render de cada informe tarda varios segundos.
    const tibia = await context.newPage();
    for (const r of ['ResumenPorSucursal', 'IndicadoresFinancieros', 'Conciliacion']) {
      await tibia.goto(`${SSRS}/report/FinCore/${r}`).catch(() => {});
      for (let i = 0; i < 60 && !(await Promise.all(tibia.frames().map((f) => f.getByText('Generado el').count().catch(() => 0)))).some(Boolean); i++) await sleep(500);
    }
    await tibia.close();
  }
  const page = await context.newPage();
  const errores = [];
  page.on('pageerror', (e) => errores.push(e.message));
  page.on('load', () => page.evaluate(OVERLAY).catch(() => {}));
  await page.goto(`${APP}/login`);
  await page.waitForSelector('[role=tab]:has-text("Demostración")', { timeout: 60000 });
  await page.evaluate(() => document.fonts.ready);
  await page.evaluate(OVERLAY);
  await sleep(600);

  const frames = []; let cdp = null;
  if (!PRUEBA) {
    cdp = await context.newCDPSession(page);
    cdp.on('Page.screencastFrame', async (f) => {
      const file = path.join(TMP, `f${String(frames.length).padStart(6, '0')}.jpg`);
      fs.writeFileSync(file, Buffer.from(f.data, 'base64'));
      if (!frames.length) wall0 = Date.now();
      frames.push({ file, t: f.metadata.timestamp, w: Date.now() });
      cdp.send('Page.screencastFrameAck', { sessionId: f.sessionId }).catch(() => {});
    });
    await cdp.send('Page.startScreencast', { format: 'jpeg', quality: 88, maxWidth: W, maxHeight: H, everyNthFrame: 1 });
  }

  let shot = 0;
  fs.mkdirSync(CAPTURAS, { recursive: true });
  const snap = async (n) => { if (PRUEBA) await page.screenshot({ path: path.join(CAPTURAS, `${String(vid.n).padStart(2, '0')}-${vid.id}-${String(++shot).padStart(2, '0')}-${n}.png`) }); };
  const hold = (ms) => sleep(PRUEBA ? Math.min(ms, 400) : ms);

  const v = { page, hold };
  v.cap = async (n, t, d, role, wide = false) => {
    await esperarVoz();
    await page.evaluate(([n, t, d, role, wide]) => {
      window.__vx = { ...(window.__vx || {}), cap: { n, t, d, role, wide } };
      const c = document.getElementById('vx-cap'); c.classList.remove('off'); c.classList.toggle('wide', wide);
      document.getElementById('vx-n').textContent = n; document.getElementById('vx-t').textContent = t;
      document.getElementById('vx-d').textContent = d; const r = document.getElementById('vx-role'); r.textContent = role || ''; r.style.display = role ? '' : 'none';
    }, [n, t, d, role, wide]);
    hablar(t, d);
    await snap(t.normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/[^a-zA-Z0-9]+/g, '-').toLowerCase());
  };
  const card = async (h, p, s) => {
    await esperarVoz();
    await page.evaluate(([h, p, s]) => {
      document.getElementById('vx-ch').textContent = h; document.getElementById('vx-cp').textContent = p; document.getElementById('vx-cs').textContent = s || '';
      document.getElementById('vx-card').classList.remove('off');
    }, [h, p, s]);
    hablar(h, p);
  };
  const cardOff = async () => { await esperarVoz(); await page.evaluate(() => document.getElementById('vx-card').classList.add('off')); };
  const capOff = async () => { await esperarVoz(); await page.evaluate(() => { document.getElementById('vx-cap').classList.add('off'); if (window.__vx) window.__vx.cap = null; }); };

  v.apuntar = async (sel) => {
    const loc = page.locator(sel).first();
    await loc.waitFor({ state: 'visible', timeout: 15000 });
    let b = null;
    for (let k = 0; k < 6 && !b; k++) { try { await loc.scrollIntoViewIfNeeded({ timeout: 2000 }); b = await loc.boundingBox(); } catch { await sleep(250); } }
    if (!b) throw new Error('no pude ubicar ' + sel);
    const x = b.x + Math.min(b.width / 2, 60), y = b.y + Math.min(b.height / 2, 40);
    await page.evaluate(([x, y]) => { window.__vx = { ...(window.__vx || {}), x, y }; const c = document.getElementById('vx-cur'); if (c) { c.style.left = (x - 4) + 'px'; c.style.top = (y - 2) + 'px'; } }, [x, y]);
    await hold(700);
    return { loc, x, y };
  };
  const ripple = (x, y) => page.evaluate(([x, y]) => { const r = document.createElement('div'); r.className = 'vx-rip'; r.style.left = x + 'px'; r.style.top = y + 'px'; document.body.appendChild(r); setTimeout(() => r.remove(), 700); }, [x, y]);
  v.clic = async (sel, pausa = 700) => { const { loc, x, y } = await v.apuntar(sel); await ripple(x, y); await loc.click(); await hold(pausa); };
  // En campos de Material el rótulo flotante a veces tapa el centro del input: si el clic normal no entra, se fuerza.
  v.escribir = async (sel, txt, pausa = 500, delay = 45) => { const { loc, x, y } = await v.apuntar(sel); await ripple(x, y); await loc.click({ timeout: 2500 }).catch(() => loc.click({ force: true })); await loc.fill(''); await loc.pressSequentially(txt, { delay: PRUEBA ? 0 : delay }); await hold(pausa); };
  v.fecha = async (sel, val, pausa = 600) => { const { loc, x, y } = await v.apuntar(sel); await ripple(x, y); await loc.fill(val); await loc.blur(); await hold(pausa); };
  // mat-select: abre el campo por su formControlName y elige la opción.
  v.elegir = async (control, opcion, pausa = 800) => { await v.clic(`mat-select[formcontrolname="${control}"]`, 600); await v.clic(`mat-option:has-text("${opcion}")`, pausa); };
  // Carga de CSV: apunta al botón visible y entrega el archivo al input oculto.
  v.subirCsv = async (botonSel, nombre, contenido, pausa = 1500) => {
    const { x, y } = await v.apuntar(botonSel); await ripple(x, y);
    await page.locator('input[type=file]').first().setInputFiles({ name: nombre, mimeType: 'text/csv', buffer: Buffer.from(contenido, 'utf8') });
    await hold(pausa);
  };
  v.nav = async (texto) => { await v.clic(`nav a:has-text("${texto}")`, 1500); };
  // Navegar a otra página conserva el subtítulo y el cursor; con `texto` espera a que aparezca (p. ej. un informe de SSRS).
  v.esperarTexto = async (texto, ms = 45000) => {
    const t0 = Date.now();
    while (Date.now() - t0 < ms) {
      for (const f of page.frames()) if (await f.getByText(texto).count().catch(() => 0)) return true;
      await sleep(500);
    }
    return false;
  };
  v.ir = async (url, pausa = 2000, texto) => {
    const estado = await page.evaluate(() => window.__vx || null).catch(() => null);
    await page.goto(url);
    await page.evaluate((s) => { document.getElementById('vx')?.remove(); window.__vx = s; }, estado);
    await page.evaluate(OVERLAY);
    if (texto && !PRUEBA) await v.esperarTexto(texto);
    await hold(pausa);
  };
  v.salir = async () => {
    if (!page.url().startsWith(APP)) await v.ir(`${APP}/inicio`, 1200);
    if (await page.locator('button.user').count()) {
      await v.clic('button.user', 600);
      await v.clic('[role=menuitem]:has-text("Cerrar sesión")', 1200);
    }
    await page.waitForURL('**/login');
  };
  v.login = async (nombre, n, titulo, detalle, rol) => {
    await esperarVoz();   // termina de narrar la escena anterior antes de cambiar de usuario
    await v.salir().catch(() => {});
    await v.cap(n, titulo, detalle, rol, true);
    await v.clic('[role=tab]:has-text("Demostración")', 700);
    await v.clic(`button:has-text("${nombre}")`, 500);
    await v.clic('button:has-text("Ingresar")', 300);
    await page.waitForURL('**/inicio', { timeout: 20000 });
    await hold(1400);
  };
  v.nuevaOperacion = async (tipo, cuenta, ref, valor, contraparte, descripcion) => {
    await v.clic('button:has-text("Nueva operación")', 1200);
    await v.clic(`mat-button-toggle:has-text("${tipo}")`, 500);
    await v.elegir('accountId', cuenta, 600);
    await v.escribir('input[formcontrolname="reference"]', ref);
    await v.escribir('input[formcontrolname="amount"]', valor);
    // La contraparte solo se pide en pagos y transferencias.
    if (await page.locator('input[formcontrolname="counterpartyName"]').isVisible()) await v.escribir('input[formcontrolname="counterpartyName"]', contraparte);
    await v.escribir('textarea[formcontrolname="description"]', descripcion, 500, 25);
    await v.clic('mat-dialog-actions button:has-text("Registrar")', 2000);
  };

  // Portada, guion y cierre
  await card(vid.titulo, vid.sub, `Video ${vid.n} de ${VIDEOS.length} · FinCore`);
  await hold(2600); await snap('portada'); await cardOff(); await hold(700);
  try {
    await vid.run(v);
  } catch (e) {
    await page.screenshot({ path: path.join(__dirname, `error-${vid.id}.png`) }).catch(() => {});
    throw e;
  }
  await capOff(); await hold(900);

  const wallFin = Date.now();
  if (cdp) await cdp.send('Page.stopScreencast');
  await context.close();
  if (errores.length) console.log(`  errores de la página (${vid.id}):`, errores);
  if (PRUEBA) return null;

  // Armar el MP4: cuadros con su duración real + narración en su instante.
  fs.mkdirSync(SALIDA, { recursive: true });
  const OUT = path.join(SALIDA, `${String(vid.n).padStart(2, '0')}-${vid.id}.mp4`);
  const lista = frames.map((f, i) => { const d = i + 1 < frames.length ? frames[i + 1].t - f.t : Math.max(0.5, (wallFin - f.w) / 1000); return `file '${f.file.replace(/\\/g, '/')}'\nduration ${Math.max(d, 0.001).toFixed(4)}`; });
  lista.push(`file '${frames[frames.length - 1].file.replace(/\\/g, '/')}'`);
  fs.writeFileSync(path.join(TMP, 'lista.txt'), lista.join('\n'));
  const video = path.join(TMP, 'video.mp4');
  execFileSync(FFMPEG, ['-y', '-f', 'concat', '-safe', '0', '-i', path.join(TMP, 'lista.txt'),
    '-vf', `fps=30,scale=${W}:${H}:flags=lanczos,format=yuv420p`, '-c:v', 'libx264', '-preset', 'slow', '-crf', '20', '-movflags', '+faststart', video], { stdio: 'ignore' });
  if (narracion.length) {
    const a = ['-y', '-i', video];
    narracion.forEach((n) => a.push('-i', n.f));
    const filtros = narracion.map((n, i) => `[${i + 1}:a]adelay=${Math.max(0, Math.round(n.t - wall0))}:all=1[a${i}]`);
    filtros.push(narracion.map((_, i) => `[a${i}]`).join('') + `amix=inputs=${narracion.length}:normalize=0:dropout_transition=0,loudnorm=I=-16:TP=-1.5[a]`);
    a.push('-filter_complex', filtros.join(';'), '-map', '0:v', '-map', '[a]', '-c:v', 'copy', '-c:a', 'aac', '-ar', '48000', '-b:a', '160k', '-movflags', '+faststart', OUT);
    execFileSync(FFMPEG, a, { stdio: 'ignore' });
  } else {
    fs.copyFileSync(video, OUT);
  }
  fs.rmSync(TMP, { recursive: true, force: true });
  return { OUT, seg: Math.round(frames[frames.length - 1].t - frames[0].t), voz: narracion.length };
}

// Al cerrar el navegador pueden quedar navegaciones pendientes (descargas, portal de SSRS): no son errores del guion.
process.on('unhandledRejection', (e) => { if (!/Target page, context or browser has been closed/.test(String(e?.message))) { console.error('FALLO:', e?.message ?? e); process.exit(1); } });

(async () => {
  const pedidos = args.filter((a) => !a.startsWith('--'));
  const lista = pedidos.length ? VIDEOS.filter((m) => pedidos.includes(m.id)) : VIDEOS;
  if (!lista.length) { console.error('Videos válidos:', VIDEOS.map((m) => m.id).join(', ')); process.exit(1); }
  console.log(PRUEBA ? 'Modo prueba (capturas, sin video)' : `Narración: ${EDGE ? 'sí (' + VOICE + ')' : 'no, solo subtítulos'}`);

  // Edge con autenticación de Windows hacia localhost: el portal de SSRS reconoce la cuenta de quien graba.
  const browser = await chromium.launch({ channel: process.env.NAVEGADOR || 'msedge', headless: true, args: ['--auth-server-allowlist=localhost'] });
  try {
    for (const m of lista) {
      process.stdout.write(`Video ${m.n} · ${m.titulo}… `);
      const r = await grabar(m, browser);
      console.log(r ? `${path.basename(r.OUT)} · ${r.seg} s${r.voz ? ' · con voz' : ''}` : 'capturas listas');
    }
  } finally {
    await browser.close();
  }
})().catch((e) => { console.error('FALLO:', e.message); process.exit(1); });
