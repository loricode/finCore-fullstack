#!/usr/bin/env node
/**
 * Genera los informes SSRS (.rdl, esquema 2016) de FinCore a partir de definiciones en código.
 *
 *   node build-rdl.mjs            → escribe Reports/*.rdl
 *
 * ¿Por qué un generador y no Report Builder? Los .rdl son XML muy verboso: definirlos aquí mantiene
 * la paleta, la tipografía y las filas intercaladas iguales en todos los informes y deja diffs legibles.
 * Los .rdl generados se versionan y se pueden abrir/ajustar en Report Builder o Visual Studio.
 *
 * Todos los conjuntos de datos llaman procedimientos rpt.* (nunca tablas): el login de reportes solo
 * tiene EXECUTE en el esquema rpt y cada procedimiento valida permisos y sucursales del usuario.
 */
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const OUT = join(dirname(fileURLToPath(import.meta.url)), 'Reports');
export const DATA_SOURCE_PATH = '/FinCore/Datos/FinCore';

// ── Paleta "sala acogedora" (mismos valores que los tokens --fc-* de la aplicación, modo claro) ──
const C = {
  teal: '#1F6264', tealDeep: '#174A4C', tealSoft: '#D6EBE8',
  linen: '#F4EEE5', cream: '#FBF8F2', ochre: '#B88A47', ochreSoft: '#F6E8CF',
  blush: '#C8735F', blushSoft: '#F8E0D8', sage: '#5F7D58', sageSoft: '#E2EBDC',
  lavender: '#7A65A6', lavenderSoft: '#EBE5F5', oak: '#D8B98F',
  text: '#3A3632', muted: '#7A7068', border: '#E8DFD2', white: '#FFFFFF',
};
const FONT = 'Segoe UI';
const DISPLAY = 'Georgia';
const ZEBRA = `=IIf(RowNumber(Nothing) Mod 2 = 0, "${C.linen}", "${C.white}")`;
const MONEY = '"$ "#,0;-"$ "#,0;"$ "0';

// ── Utilidades XML ──
const esc = (s) => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
const el = (tag, content) => (content === undefined || content === null || content === '' ? '' : `<${tag}>${content}</${tag}>`);
const style = (s = {}) => `<Style>${Object.entries(s).filter(([, v]) => v !== undefined).map(([k, v]) => `<${k}>${esc(v)}</${k}>`).join('')}</Style>`;
const box = ({ top = 0, left = 0, width, height }) => `<Top>${top}in</Top><Left>${left}in</Left><Height>${height}in</Height><Width>${width}in</Width>`;
const border = (color = C.border, sides = ['Bottom']) =>
  `<Border><Style>None</Style></Border>` + sides.map((s) => `<${s}Border><Color>${color}</Color><Style>Solid</Style><Width>0.75pt</Width></${s}Border>`).join('');

let seq = 0;
const uid = (p) => `${p}${++seq}`;

/** Nombre válido para exportaciones CSV/XML: "Valor efectivo" → ValorEfectivo. */
const dataName = (s) => s.normalize('NFD').replace(/[^A-Za-z0-9 ]/g, '').replace(/(?:^|\s+)(\w)/g, (_, c) => c.toUpperCase());

/** Cuadro de texto. `runs` puede ser un valor o una lista de { value, ...estiloDeTexto }. */
function textbox({ name = uid('Txt'), value, runs, pos, font = {}, box: boxStyle = {}, align = 'Left', canGrow = true, dataName, noOutput = false }) {
  const list = runs ?? [{ value }];
  const textRuns = list.map((r) => {
    const { value: v, ...rs } = r;
    const f = { FontFamily: FONT, FontSize: '9pt', Color: C.text, ...font, ...rs };
    return `<TextRun><Value>${esc(v)}</Value>${style(f)}</TextRun>`;
  }).join('');
  const { borderColor, borderSides, ...bs } = boxStyle;
  const boxXml = `<Style>${borderSides ? border(borderColor, borderSides) : '<Border><Style>None</Style></Border>'}` +
    Object.entries({ PaddingLeft: '4pt', PaddingRight: '4pt', PaddingTop: '2pt', PaddingBottom: '2pt', VerticalAlign: 'Middle', ...bs })
      .map(([k, v]) => `<${k}>${esc(v)}</${k}>`).join('') + '</Style>';
  return `<Textbox Name="${name}"><CanGrow>${canGrow}</CanGrow><KeepTogether>true</KeepTogether>` +
    `<Paragraphs><Paragraph><TextRuns>${textRuns}</TextRuns><Style><TextAlign>${align}</TextAlign></Style></Paragraph></Paragraphs>` +
    (pos ? box(pos) : '') + boxXml +
    (dataName ? `<DataElementName>${dataName}</DataElementName>` : '') + (noOutput ? '<DataElementOutput>NoOutput</DataElementOutput>' : '') + `</Textbox>`;
}

/**
 * Tabla con encabezado, detalle con filas intercaladas y fila de totales opcional.
 * columns: [{ header, value, width, format?, align?, total?, color?, bold? }]
 */
function table({ name = uid('Table'), dataSet, columns, pos, sort, noRows = 'No hay datos para los filtros seleccionados.' }) {
  const hasTotal = columns.some((c) => c.total !== undefined);
  const cell = (inner) => `<TablixCell><CellContents>${inner}</CellContents></TablixCell>`;
  const header = columns.map((c) => cell(textbox({
    value: c.header, align: c.align ?? 'Left',
    font: { FontWeight: 'Bold', Color: C.white, FontSize: '8.5pt' },
    box: { BackgroundColor: C.teal, PaddingTop: '5pt', PaddingBottom: '5pt' },
  }))).join('');
  const detail = columns.map((c) => cell(textbox({
    value: c.value, dataName: dataName(c.header), align: c.align ?? 'Left',
    font: { Format: c.format, Color: c.color ?? C.text, FontWeight: c.bold ? 'Bold' : undefined },
    box: { BackgroundColor: ZEBRA, borderColor: C.border, borderSides: ['Bottom'], PaddingTop: '4pt', PaddingBottom: '4pt' },
  }))).join('');
  const total = columns.map((c) => cell(textbox({
    value: c.total ?? '', noOutput: true, align: c.align ?? 'Left',
    font: { Format: c.format, FontWeight: 'Bold', Color: C.tealDeep },
    box: { BackgroundColor: C.tealSoft, borderColor: C.teal, borderSides: ['Top'], PaddingTop: '5pt', PaddingBottom: '5pt' },
  }))).join('');
  const width = columns.reduce((s, c) => s + c.width, 0);
  const sortXml = sort ? `<SortExpressions>${sort.map((s) => `<SortExpression><Value>${esc(s)}</Value></SortExpression>`).join('')}</SortExpressions>` : '';
  return `<Tablix Name="${name}"><TablixBody>` +
    `<TablixColumns>${columns.map((c) => `<TablixColumn><Width>${c.width}in</Width></TablixColumn>`).join('')}</TablixColumns>` +
    `<TablixRows><TablixRow><Height>0.3in</Height><TablixCells>${header}</TablixCells></TablixRow>` +
    `<TablixRow><Height>0.27in</Height><TablixCells>${detail}</TablixCells></TablixRow>` +
    (hasTotal ? `<TablixRow><Height>0.3in</Height><TablixCells>${total}</TablixCells></TablixRow>` : '') +
    `</TablixRows></TablixBody>` +
    `<TablixColumnHierarchy><TablixMembers>${columns.map(() => '<TablixMember />').join('')}</TablixMembers></TablixColumnHierarchy>` +
    `<TablixRowHierarchy><TablixMembers>` +
    `<TablixMember><KeepWithGroup>After</KeepWithGroup><RepeatOnNewPage>true</RepeatOnNewPage></TablixMember>` +
    `<TablixMember><Group Name="${name}_Details" />${sortXml}</TablixMember>` +
    (hasTotal ? '<TablixMember><KeepWithGroup>Before</KeepWithGroup></TablixMember>' : '') +
    `</TablixMembers></TablixRowHierarchy>` +
    `<RepeatColumnHeaders>true</RepeatColumnHeaders><DataSetName>${dataSet}</DataSetName>` +
    `<NoRowsMessage>${esc(noRows)}</NoRowsMessage>` +
    box({ ...pos, width, height: hasTotal ? 0.87 : 0.57 }) +
    style({ FontFamily: FONT, FontSize: '9pt', Color: C.muted }) + `</Tablix>`;
}

const axis = (name, { format, title } = {}) =>
  `<ChartAxis Name="${name}">${style({ FontFamily: FONT, FontSize: '8pt', Color: C.muted, Format: format })}` +
  `<ChartAxisTitle><Caption>${esc(title ?? '')}</Caption>${style({ FontSize: '8pt' })}</ChartAxisTitle>` +
  `<ChartMajorGridLines><Enabled>${name === 'Value' ? 'True' : 'False'}</Enabled><Style><Border><Color>${C.border}</Color><Style>Dashed</Style></Border></Style></ChartMajorGridLines>` +
  `<ChartMinorGridLines><Style><Border><Style>None</Style></Border></Style></ChartMinorGridLines>` +
  `<ChartMajorTickMarks><Enabled>False</Enabled></ChartMajorTickMarks>` +
  `<ChartMinorTickMarks><Length>0.5</Length></ChartMinorTickMarks>` +
  `<CrossAt>NaN</CrossAt><Minimum>NaN</Minimum><Maximum>NaN</Maximum>` +
  (name === 'Category' ? '<Margin>True</Margin><Interval>1</Interval>' : '') +
  `<ChartAxisScaleBreak>${style()}</ChartAxisScaleBreak></ChartAxis>`;

/**
 * Gráfico de columnas. Series estáticas (`series: [{ label, y, color }]`) o una serie dinámica por grupo
 * (`seriesGroup: { expr, y, colors }`).
 */
function columnChart({ name = uid('Chart'), dataSet, category, categorySort, series, seriesGroup, pos, valueFormat = MONEY, type = 'Column' }) {
  const point = (y, color) =>
    `<ChartDataPoint><ChartDataPointValues><Y>${esc(y)}</Y></ChartDataPointValues>` +
    `<ChartDataLabel>${style()}</ChartDataLabel>` +
    `${color ? style({ Color: color }) : style()}<ChartMarker>${style()}</ChartMarker>` +
    `<DataElementOutput>Output</DataElementOutput></ChartDataPoint>`;
  const seriesXml = (sname, y, color) =>
    `<ChartSeries Name="${sname}"><ChartDataPoints>${point(y, color)}</ChartDataPoints>` +
    `<Type>${type}</Type>${style()}` +
    `<ChartEmptyPoints>${style()}<ChartMarker>${style()}</ChartMarker><ChartDataLabel>${style()}</ChartDataLabel></ChartEmptyPoints>` +
    `<CustomProperties><CustomProperty><Name>PointWidth</Name><Value>0.7</Value></CustomProperty></CustomProperties>` +
    `<ValueAxisName>Value</ValueAxisName><CategoryAxisName>Category</CategoryAxisName>` +
    `<ChartSmartLabel><CalloutLineColor>Black</CalloutLineColor><MinMovingDistance>0pt</MinMovingDistance></ChartSmartLabel></ChartSeries>`;

  const seriesMembers = seriesGroup
    ? `<ChartMember><Group Name="${name}_Series"><GroupExpressions><GroupExpression>${esc(seriesGroup.expr)}</GroupExpression></GroupExpressions></Group>` +
      `<SortExpressions><SortExpression><Value>${esc(seriesGroup.sort ?? seriesGroup.expr)}</Value></SortExpression></SortExpressions><Label>${esc(seriesGroup.expr)}</Label></ChartMember>`
    : series.map((s) => `<ChartMember><Label>${esc(s.label)}</Label></ChartMember>`).join('');
  const seriesCollection = seriesGroup
    ? seriesXml(`${name}_S`, seriesGroup.y, seriesGroup.colorExpr)
    : series.map((s, i) => seriesXml(`${name}_S${i}`, s.y, s.color)).join('');
  const colors = seriesGroup ? seriesGroup.colors : series.map((s) => s.color);

  return `<Chart Name="${name}">` +
    `<ChartCategoryHierarchy><ChartMembers><ChartMember><Group Name="${name}_Category"><GroupExpressions><GroupExpression>${esc(category)}</GroupExpression></GroupExpressions></Group>` +
    `<SortExpressions><SortExpression><Value>${esc(categorySort ?? category)}</Value></SortExpression></SortExpressions><Label>${esc(category)}</Label></ChartMember></ChartMembers></ChartCategoryHierarchy>` +
    `<ChartSeriesHierarchy><ChartMembers>${seriesMembers}</ChartMembers></ChartSeriesHierarchy>` +
    `<ChartData><ChartSeriesCollection>${seriesCollection}</ChartSeriesCollection></ChartData>` +
    `<ChartAreas><ChartArea Name="Default"><ChartCategoryAxes>${axis('Category')}</ChartCategoryAxes>` +
    `<ChartValueAxes>${axis('Value', { format: valueFormat })}</ChartValueAxes>${style({ BackgroundColor: C.white })}</ChartArea></ChartAreas>` +
    `<ChartLegends><ChartLegend Name="Default">${style({ FontFamily: FONT, FontSize: '8pt', Color: C.muted })}<Position>TopRight</Position>` +
    `<ChartLegendTitle><Caption />${style({ FontSize: '8pt' })}</ChartLegendTitle><HeaderSeparatorColor>Black</HeaderSeparatorColor><ColumnSeparatorColor>Black</ColumnSeparatorColor></ChartLegend></ChartLegends>` +
    `<Palette>Custom</Palette><ChartCustomPaletteColors>${colors.map((c) => `<ChartCustomPaletteColor>${c}</ChartCustomPaletteColor>`).join('')}</ChartCustomPaletteColors>` +
    `<ChartBorderSkin><Style><BackgroundColor>Gray</BackgroundColor><BackgroundGradientType>None</BackgroundGradientType><Color>White</Color></Style></ChartBorderSkin>` +
    `<ChartNoDataMessage Name="NoData"><Caption>Sin datos en el período</Caption>${style({ FontFamily: FONT, Color: C.muted })}</ChartNoDataMessage>` +
    `<DataSetName>${dataSet}</DataSetName>${box(pos)}<DataElementOutput>NoOutput</DataElementOutput>` +
    `<Style><Border><Color>${C.border}</Color><Style>Solid</Style></Border><BackgroundColor>${C.white}</BackgroundColor></Style></Chart>`;
}

/** Tarjeta de indicador: rectángulo de color suave con etiqueta, valor grande y pista. */
function kpi({ label, value, hint, format, color, soft, pos }) {
  const { top, left, width } = pos;
  return `<Rectangle Name="${uid('Kpi')}"><ReportItems>` +
    textbox({ value: label, pos: { top: 0.08, left: 0.1, width: width - 0.2, height: 0.25 }, font: { FontSize: '8.5pt', FontWeight: 'Bold', Color: color } }) +
    textbox({ value, pos: { top: 0.33, left: 0.1, width: width - 0.2, height: 0.42 }, font: { FontFamily: DISPLAY, FontSize: '17pt', Color: C.text, Format: format } }) +
    textbox({ value: hint, pos: { top: 0.75, left: 0.1, width: width - 0.2, height: 0.3 }, font: { FontSize: '7.5pt', Color: C.muted } }) +
    `</ReportItems><KeepTogether>true</KeepTogether>${box({ top, left, width, height: 1.1 })}` +
    `<Style><Border><Color>${soft}</Color><Style>Solid</Style></Border><LeftBorder><Color>${color}</Color><Style>Solid</Style><Width>4pt</Width></LeftBorder><BackgroundColor>${soft}</BackgroundColor></Style></Rectangle>`;
}

// ── Parámetros y conjuntos de datos ──
const P = {
  // La lista depende de la cuenta de Windows de quien abre el informe (User!UserID): sin reports.viewas solo trae a ese
  // usuario, y SSRS rechaza cualquier otro valor aunque llegue por URL. El valor por defecto es la primera fila (él mismo).
  userId: { name: 'UserId', type: 'Integer', prompt: 'Ver como', dataSet: 'ReportUsers', value: 'UserId', label: 'Label', defaultFromDataSet: true },
  dateFrom: { name: 'DateFrom', type: 'DateTime', prompt: 'Desde', default: '=DateSerial(Year(Today()), Month(Today()), 1)' },
  dateTo: { name: 'DateTo', type: 'DateTime', prompt: 'Hasta', default: '=Today()' },
  granularity: { name: 'Granularity', type: 'String', prompt: 'Agrupar por', default: 'DAY', values: [['DAY', 'Día'], ['MONTH', 'Mes']] },
  branchId: { name: 'BranchId', type: 'Integer', prompt: 'Sucursal', nullable: true, dataSet: 'ReportBranches', value: 'BranchId', label: 'Label', defaultFromDataSet: true },
  reconciliationId: { name: 'ReconciliationId', type: 'Integer', prompt: 'Conciliación', dataSet: 'ReportReconciliations', value: 'ReconciliationId', label: 'Label', defaultFromDataSet: true },
};

function parameterXml(p) {
  let validValues = '';
  if (p.dataSet) validValues = `<ValidValues><DataSetReference><DataSetName>${p.dataSet}</DataSetName><ValueField>${p.value}</ValueField><LabelField>${p.label}</LabelField></DataSetReference></ValidValues>`;
  if (p.values) validValues = `<ValidValues><ParameterValues>${p.values.map(([v, l]) => `<ParameterValue><Value>${esc(v)}</Value><Label>${esc(l)}</Label></ParameterValue>`).join('')}</ParameterValues></ValidValues>`;
  let def = '';
  if (p.default) def = `<DefaultValue><Values><Value>${esc(p.default)}</Value></Values></DefaultValue>`;
  if (p.defaultFromDataSet) def = `<DefaultValue><DataSetReference><DataSetName>${p.dataSet}</DataSetName><ValueField>${p.value}</ValueField></DataSetReference></DefaultValue>`;
  return `<ReportParameter Name="${p.name}"><DataType>${p.type}</DataType>${p.nullable ? '<Nullable>true</Nullable>' : ''}${def}` +
    `<Prompt>${esc(p.prompt)}</Prompt>${validValues}</ReportParameter>`;
}

const TYPES = { String: 'System.String', Int: 'System.Int32', Long: 'System.Int64', Decimal: 'System.Decimal', Date: 'System.DateTime', Bool: 'System.Boolean' };

/** Conjunto de datos sobre un procedimiento rpt.*. fields: { Nombre: 'Tipo' }, params: { '@X': '=expr' } */
function dataSet(name, proc, params, fields) {
  const qp = Object.entries(params).map(([n, v]) => `<QueryParameter Name="${n}"><Value>${esc(v)}</Value></QueryParameter>`).join('');
  const f = Object.entries(fields).map(([n, t]) => `<Field Name="${n}"><DataField>${n}</DataField><rd:TypeName>${TYPES[t]}</rd:TypeName></Field>`).join('');
  return `<DataSet Name="${name}"><Query><DataSourceName>FinCore</DataSourceName>${qp ? `<QueryParameters>${qp}</QueryParameters>` : ''}` +
    `<CommandType>StoredProcedure</CommandType><CommandText>${proc}</CommandText></Query><Fields>${f}</Fields></DataSet>`;
}

const lookupSets = {
  ReportUsers: () => dataSet('ReportUsers', 'rpt.usp_ReportUsers', { '@WindowsAccount': '=User!UserID' }, { UserId: 'Int', Label: 'String' }),
  ReportBranches: () => dataSet('ReportBranches', 'rpt.usp_ReportBranches', { '@UserId': '=Parameters!UserId.Value' }, { BranchId: 'Int', Label: 'String', SortOrder: 'Int' }),
  ReportReconciliations: () => dataSet('ReportReconciliations', 'rpt.usp_ReportReconciliations', { '@UserId': '=Parameters!UserId.Value' }, { ReconciliationId: 'Int', Label: 'String' }),
};
const periodParams = { '@UserId': '=Parameters!UserId.Value', '@DateFrom': '=Parameters!DateFrom.Value', '@DateTo': '=Parameters!DateTo.Value' };

// ── Esqueleto común: encabezado con franja de color, cuerpo, pie con página y fecha de generación ──
const PAGE_WIDTH = 11, MARGIN = 0.5, BODY_WIDTH = PAGE_WIDTH - 2 * MARGIN;

function report({ title, subtitle, params, dataSets, body, bodyHeight }) {
  seq = 0;
  const header =
    `<PageHeader><Height>0.95in</Height><PrintOnFirstPage>true</PrintOnFirstPage><PrintOnLastPage>true</PrintOnLastPage><ReportItems>` +
    textbox({ name: 'Brand', value: 'FinCore', pos: { top: 0, left: 0, width: 2, height: 0.25 }, font: { FontSize: '8.5pt', FontWeight: 'Bold', Color: C.ochre } }) +
    textbox({ name: 'Generated', value: '="Generado el " & Format(Globals!ExecutionTime, "dd/MM/yyyy HH:mm")', align: 'Right',
      pos: { top: 0, left: BODY_WIDTH - 3.5, width: 3.5, height: 0.25 }, font: { FontSize: '8pt', Color: C.muted } }) +
    textbox({ name: 'Title', value: title, pos: { top: 0.25, left: 0, width: BODY_WIDTH, height: 0.38 }, font: { FontFamily: DISPLAY, FontSize: '18pt', Color: C.tealDeep } }) +
    textbox({ name: 'Subtitle', value: subtitle, pos: { top: 0.63, left: 0, width: BODY_WIDTH, height: 0.25 }, font: { FontSize: '9pt', Color: C.muted } }) +
    `<Line Name="HeaderRule"><Top>0.92in</Top><Left>0in</Left><Height>0in</Height><Width>${BODY_WIDTH}in</Width><Style><Border><Color>${C.oak}</Color><Style>Solid</Style><Width>2pt</Width></Border></Style></Line>` +
    `</ReportItems><Style><Border><Style>None</Style></Border></Style></PageHeader>`;
  const footer =
    `<PageFooter><Height>0.35in</Height><PrintOnFirstPage>true</PrintOnFirstPage><PrintOnLastPage>true</PrintOnLastPage><ReportItems>` +
    textbox({ name: 'FooterNote', value: 'Información confidencial · solo sucursales autorizadas al usuario', pos: { top: 0.08, left: 0, width: 6, height: 0.25 }, font: { FontSize: '7.5pt', Color: C.muted } }) +
    textbox({ name: 'PageNo', value: '="Página " & Globals!PageNumber & " de " & Globals!TotalPages', align: 'Right',
      pos: { top: 0.08, left: BODY_WIDTH - 2.5, width: 2.5, height: 0.25 }, font: { FontSize: '7.5pt', Color: C.muted } }) +
    `</ReportItems><Style><Border><Style>None</Style></Border><TopBorder><Color>${C.border}</Color><Style>Solid</Style></TopBorder></Style></PageFooter>`;

  return `<?xml version="1.0" encoding="utf-8"?>
<Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition" xmlns:rd="http://schemas.microsoft.com/SQLServer/reporting/reportdesigner">
<AutoRefresh>0</AutoRefresh>
<DataSources><DataSource Name="FinCore"><DataSourceReference>${DATA_SOURCE_PATH}</DataSourceReference><rd:DataSourceID>6f1c9b8e-3c7a-4a51-9d0e-f1ce0de5a001</rd:DataSourceID></DataSource></DataSources>
<DataSets>${dataSets.join('')}</DataSets>
<ReportSections><ReportSection>
<Body><ReportItems>${body}</ReportItems><Height>${bodyHeight}in</Height>${style({ BackgroundColor: C.white })}</Body>
<Width>${BODY_WIDTH}in</Width>
<Page>${header}${footer}<PageHeight>8.5in</PageHeight><PageWidth>${PAGE_WIDTH}in</PageWidth>
<LeftMargin>${MARGIN}in</LeftMargin><RightMargin>${MARGIN}in</RightMargin><TopMargin>0.4in</TopMargin><BottomMargin>0.4in</BottomMargin>${style()}</Page>
</ReportSection></ReportSections>
<ReportParameters>${params.map(parameterXml).join('')}</ReportParameters>
<Language>es-CO</Language>
<ConsumeContainerWhitespace>true</ConsumeContainerWhitespace>
<rd:ReportUnitType>Inch</rd:ReportUnitType>
<rd:ReportID>${reportId(title)}</rd:ReportID>
</Report>
`;
}

function reportId(text) {
  let h = 0x811c9dc5;
  const hex = [];
  for (let round = 0; round < 4; round++) {
    for (const ch of text + round) h = Math.imul(h ^ ch.charCodeAt(0), 0x01000193) >>> 0;
    hex.push(h.toString(16).padStart(8, '0'));
  }
  const s = hex.join('');
  return `${s.slice(0, 8)}-${s.slice(8, 12)}-4${s.slice(13, 16)}-a${s.slice(17, 20)}-${s.slice(20, 32)}`;
}

const periodSubtitle = '="Del " & Format(Parameters!DateFrom.Value, "dd/MM/yyyy") & " al " & Format(Parameters!DateTo.Value, "dd/MM/yyyy") & "  ·  " & Parameters!UserId.Label';

// ── Informes ──
const reports = {};

reports['ResumenPorSucursal'] = () => {
  const fields = { BranchId: 'Int', BranchCode: 'String', BranchName: 'String', City: 'String', TotalOperations: 'Int', TotalIncome: 'Decimal', TotalExpense: 'Decimal',
    NetAmount: 'Decimal', PendingCount: 'Int', ProcessedCount: 'Int', ReconciledCount: 'Int', RejectedCount: 'Int', ReversedCount: 'Int' };
  const body =
    columnChart({ dataSet: 'Branches', category: '=Fields!BranchName.Value', pos: { top: 0.1, left: 0, width: BODY_WIDTH, height: 2.6 },
      series: [
        { label: 'Ingresos', y: '=Sum(Fields!TotalIncome.Value)', color: C.sage },
        { label: 'Egresos', y: '=Sum(Fields!TotalExpense.Value)', color: C.blush },
      ] }) +
    table({ dataSet: 'Branches', pos: { top: 2.9, left: 0 }, columns: [
      { header: 'Código', value: '=Fields!BranchCode.Value', width: 0.8, total: 'Total' },
      { header: 'Sucursal', value: '=Fields!BranchName.Value', width: 1.9, bold: true },
      { header: 'Ciudad', value: '=Fields!City.Value', width: 1.1 },
      { header: 'Operaciones', value: '=Fields!TotalOperations.Value', width: 0.9, align: 'Right', total: '=Sum(Fields!TotalOperations.Value)' },
      { header: 'Ingresos', value: '=Fields!TotalIncome.Value', width: 1.3, align: 'Right', format: MONEY, color: C.sage, total: '=Sum(Fields!TotalIncome.Value)' },
      { header: 'Egresos', value: '=Fields!TotalExpense.Value', width: 1.3, align: 'Right', format: MONEY, color: C.blush, total: '=Sum(Fields!TotalExpense.Value)' },
      { header: 'Neto', value: '=Fields!NetAmount.Value', width: 1.3, align: 'Right', format: MONEY, bold: true, total: '=Sum(Fields!NetAmount.Value)' },
      { header: 'Pend.', value: '=Fields!PendingCount.Value', width: 0.45, align: 'Right', total: '=Sum(Fields!PendingCount.Value)' },
      { header: 'Proc.', value: '=Fields!ProcessedCount.Value', width: 0.45, align: 'Right', total: '=Sum(Fields!ProcessedCount.Value)' },
      { header: 'Conc.', value: '=Fields!ReconciledCount.Value', width: 0.5, align: 'Right', total: '=Sum(Fields!ReconciledCount.Value)' },
    ] });
  return report({
    title: 'Resumen por sucursal', subtitle: periodSubtitle, bodyHeight: 3.9,
    params: [P.userId, P.dateFrom, P.dateTo],
    dataSets: [lookupSets.ReportUsers(), dataSet('Branches', 'rpt.usp_BranchSummary', periodParams, fields)],
    body,
  });
};

reports['OperacionesPorPeriodo'] = () => {
  const fields = { PeriodStart: 'Date', TransactionTypeCode: 'String', TransactionTypeName: 'String', OperationCount: 'Int', EffectiveCount: 'Int', EffectiveAmount: 'Decimal' };
  const periodLabel = '=IIf(Parameters!Granularity.Value = "MONTH", Format(Fields!PeriodStart.Value, "MMM yyyy"), Format(Fields!PeriodStart.Value, "dd/MM"))';
  const body =
    columnChart({ dataSet: 'Periods', category: periodLabel, categorySort: '=Fields!PeriodStart.Value', pos: { top: 0.1, left: 0, width: BODY_WIDTH, height: 2.7 },
      seriesGroup: { expr: '=Fields!TransactionTypeName.Value', sort: '=Fields!TransactionTypeCode.Value', y: '=Sum(Fields!EffectiveAmount.Value)',
        // Mismo color por tipo que en la aplicación, sin depender del orden de las series.
        colorExpr: `=Switch(Fields!TransactionTypeCode.Value = "ING", "${C.sage}", Fields!TransactionTypeCode.Value = "EGR", "${C.blush}", Fields!TransactionTypeCode.Value = "PAG", "${C.ochre}", Fields!TransactionTypeCode.Value = "TRF", "${C.teal}", True, "${C.lavender}")`,
        colors: [C.lavender, C.ochre, C.sage, C.blush, C.teal, C.oak] } }) +
    table({ dataSet: 'Periods', pos: { top: 3.0, left: 0 }, sort: ['=Fields!PeriodStart.Value', '=Fields!TransactionTypeCode.Value'], columns: [
      { header: 'Período', value: periodLabel, width: 1.6, bold: true, total: 'Total' },
      { header: 'Tipo', value: '=Fields!TransactionTypeName.Value', width: 2.8 },
      { header: 'Operaciones', value: '=Fields!OperationCount.Value', width: 1.6, align: 'Right', total: '=Sum(Fields!OperationCount.Value)' },
      { header: 'Efectivas', value: '=Fields!EffectiveCount.Value', width: 1.6, align: 'Right', total: '=Sum(Fields!EffectiveCount.Value)' },
      { header: 'Valor efectivo', value: '=Fields!EffectiveAmount.Value', width: 2.4, align: 'Right', format: MONEY, bold: true, total: '=Sum(Fields!EffectiveAmount.Value)' },
    ] });
  return report({
    title: 'Operaciones por período',
    subtitle: periodSubtitle + ' & "  ·  " & Parameters!BranchId.Label',
    bodyHeight: 4.0,
    params: [P.userId, P.dateFrom, P.dateTo, P.granularity, P.branchId],
    dataSets: [lookupSets.ReportUsers(), lookupSets.ReportBranches(),
      dataSet('Periods', 'rpt.usp_OperationsByPeriod', { ...periodParams, '@Granularity': '=Parameters!Granularity.Value', '@BranchId': '=Parameters!BranchId.Value' }, fields)],
    body,
  });
};

reports['Conciliacion'] = () => {
  const fields = { ReconciliationId: 'Int', AccountCode: 'String', BankName: 'String', BankAccountNumber: 'String', BranchName: 'String', PeriodFrom: 'Date', PeriodTo: 'Date',
    Status: 'String', StartedAtUtc: 'Date', MatchedCount: 'Int', DifferenceCount: 'Int', NetDifferenceAmount: 'Decimal', ResultTypeCode: 'String', ResultTypeName: 'String',
    RequiresAction: 'Bool', InternalReference: 'String', OperationDate: 'Date', BankReference: 'String', MovementDate: 'Date', InternalAmount: 'Decimal', BankAmount: 'Decimal',
    DifferenceAmount: 'Decimal', Notes: 'String', ResolvedAtUtc: 'Date', ResolutionNote: 'String' };
  const first = (f) => `=First(Fields!${f}.Value, "Detail")`;
  const w = 2.4;
  const body =
    kpi({ label: 'Cuenta', value: first('AccountCode'), hint: `=First(Fields!BankName.Value, "Detail") & " · " & First(Fields!BankAccountNumber.Value, "Detail")`, color: C.teal, soft: C.tealSoft, pos: { top: 0.1, left: 0, width: w } }) +
    kpi({ label: 'Conciliadas', value: first('MatchedCount'), hint: 'Movimientos que cuadran con el banco', color: C.sage, soft: C.sageSoft, pos: { top: 0.1, left: w + 0.13, width: w } }) +
    kpi({ label: 'Diferencias', value: first('DifferenceCount'), hint: 'Requieren revisión o ya resueltas', color: C.ochre, soft: C.ochreSoft, pos: { top: 0.1, left: 2 * (w + 0.13), width: w } }) +
    kpi({ label: 'Diferencia neta', value: first('NetDifferenceAmount'), format: MONEY, hint: first('BranchName'), color: C.blush, soft: C.blushSoft, pos: { top: 0.1, left: 3 * (w + 0.13), width: w - 0.01 } }) +
    table({ dataSet: 'Detail', pos: { top: 1.4, left: 0 }, columns: [
      { header: 'Resultado', value: '=Fields!ResultTypeName.Value', width: 1.7, bold: true, color: `=IIf(Fields!RequiresAction.Value, "${C.blush}", "${C.sage}")` },
      { header: 'Ref. interna', value: '=Fields!InternalReference.Value', width: 1.25 },
      { header: 'Fecha', value: '=Fields!OperationDate.Value', width: 0.8, format: 'dd/MM/yyyy' },
      { header: 'Ref. banco', value: '=Fields!BankReference.Value', width: 1.25 },
      { header: 'Fecha banco', value: '=Fields!MovementDate.Value', width: 0.85, format: 'dd/MM/yyyy' },
      { header: 'Valor FinCore', value: '=Fields!InternalAmount.Value', width: 1.1, align: 'Right', format: MONEY },
      { header: 'Valor banco', value: '=Fields!BankAmount.Value', width: 1.1, align: 'Right', format: MONEY },
      { header: 'Diferencia', value: '=Fields!DifferenceAmount.Value', width: 1.0, align: 'Right', format: MONEY, bold: true },
      { header: 'Estado', value: '=IIf(Not Fields!RequiresAction.Value, "—", IIf(IsNothing(Fields!ResolvedAtUtc.Value), "Abierta", "Resuelta"))', width: 0.95 },
    ] });
  return report({
    title: 'Conciliación bancaria',
    subtitle: '="Conciliación #" & Parameters!ReconciliationId.Value & "  ·  período " & Format(First(Fields!PeriodFrom.Value, "Detail"), "dd/MM/yyyy") & " – " & Format(First(Fields!PeriodTo.Value, "Detail"), "dd/MM/yyyy") & "  ·  " & Parameters!UserId.Label',
    bodyHeight: 2.4,
    params: [P.userId, P.reconciliationId],
    dataSets: [lookupSets.ReportUsers(), lookupSets.ReportReconciliations(),
      dataSet('Detail', 'rpt.usp_ReconciliationReport', { '@UserId': '=Parameters!UserId.Value', '@ReconciliationId': '=Parameters!ReconciliationId.Value' }, fields)],
    body,
  });
};

reports['IndicadoresFinancieros'] = () => {
  const fields = { DateFrom: 'Date', DateTo: 'Date', OperationCount: 'Int', PendingCount: 'Int', ProcessedCount: 'Int', ReconciledCount: 'Int', RejectedCount: 'Int', ReversedCount: 'Int',
    TotalIncome: 'Decimal', TotalExpense: 'Decimal', NetAmount: 'Decimal', ReconciliationCoveragePct: 'Decimal', OpenDifferenceCount: 'Int', OpenDifferenceAmount: 'Decimal',
    EtlExecutionCount: 'Int', EtlRowsRead: 'Int', EtlRowsRejected: 'Int', EtlRejectionPct: 'Decimal' };
  const f = (n) => `=First(Fields!${n}.Value, "Indicators")`;
  const w = 3.2, gap = 0.2;
  const col = (i) => i * (w + gap);
  const body =
    kpi({ label: 'Ingresos', value: f('TotalIncome'), format: MONEY, hint: 'Operaciones efectivas (procesadas y conciliadas)', color: C.sage, soft: C.sageSoft, pos: { top: 0.1, left: col(0), width: w } }) +
    kpi({ label: 'Egresos', value: f('TotalExpense'), format: MONEY, hint: 'Pagos, transferencias y egresos', color: C.blush, soft: C.blushSoft, pos: { top: 0.1, left: col(1), width: w } }) +
    kpi({ label: 'Neto', value: f('NetAmount'), format: MONEY, hint: `=First(Fields!OperationCount.Value, "Indicators") & " operaciones en el período"`, color: C.lavender, soft: C.lavenderSoft, pos: { top: 0.1, left: col(2), width: w } }) +
    kpi({ label: 'Cobertura de conciliación', value: `=First(Fields!ReconciliationCoveragePct.Value, "Indicators") / 100`, format: '0.0 %', hint: 'Del valor efectivo que ya cuadra con el banco', color: C.teal, soft: C.tealSoft, pos: { top: 1.4, left: col(0), width: w } }) +
    kpi({ label: 'Diferencias abiertas', value: f('OpenDifferenceCount'), hint: `=Format(First(Fields!OpenDifferenceAmount.Value, "Indicators"), "$ #,0") & " por revisar"`, color: C.ochre, soft: C.ochreSoft, pos: { top: 1.4, left: col(1), width: w } }) +
    kpi({ label: 'Calidad de las cargas (ETL)', value: `=1 - First(Fields!EtlRejectionPct.Value, "Indicators") / 100`, format: '0.0 %',
      hint: `=(First(Fields!EtlRowsRead.Value, "Indicators") - First(Fields!EtlRowsRejected.Value, "Indicators")) & " de " & First(Fields!EtlRowsRead.Value, "Indicators") & " movimientos aceptados en " & First(Fields!EtlExecutionCount.Value, "Indicators") & " cargas"`,
      color: C.teal, soft: C.tealSoft, pos: { top: 1.4, left: col(2), width: w } }) +
    textbox({ value: 'Estado de las operaciones', pos: { top: 2.75, left: 0, width: 4, height: 0.3 }, font: { FontFamily: DISPLAY, FontSize: '12pt', Color: C.tealDeep } }) +
    table({ dataSet: 'Indicators', pos: { top: 3.1, left: 0 }, columns: [
      { header: 'Pendientes', value: '=Fields!PendingCount.Value', width: 1.6, align: 'Right', color: C.ochre, bold: true },
      { header: 'Procesadas', value: '=Fields!ProcessedCount.Value', width: 1.6, align: 'Right', color: C.teal, bold: true },
      { header: 'Conciliadas', value: '=Fields!ReconciledCount.Value', width: 1.6, align: 'Right', color: C.sage, bold: true },
      { header: 'Rechazadas', value: '=Fields!RejectedCount.Value', width: 1.6, align: 'Right', color: C.blush, bold: true },
      { header: 'Revertidas', value: '=Fields!ReversedCount.Value', width: 1.6, align: 'Right', color: C.lavender, bold: true },
      { header: 'Total', value: '=Fields!OperationCount.Value', width: 2.0, align: 'Right', bold: true },
    ] });
  return report({
    title: 'Indicadores financieros', subtitle: periodSubtitle, bodyHeight: 3.8,
    params: [P.userId, P.dateFrom, P.dateTo],
    dataSets: [lookupSets.ReportUsers(), dataSet('Indicators', 'rpt.usp_FinancialIndicators', periodParams, fields)],
    body,
  });
};

reports['ActividadPorUsuario'] = () => {
  const fields = { UserId: 'Int', UserName: 'String', FullName: 'String', OperationsCreated: 'Int', AmountCreated: 'Decimal', StatusChanges: 'Int', AuditActions: 'Int', LastActionAtUtc: 'Date' };
  const body = table({ dataSet: 'Activity', pos: { top: 0.1, left: 0 }, columns: [
    { header: 'Usuario', value: '=Fields!FullName.Value', width: 2.1, bold: true, total: 'Total' },
    { header: 'Login', value: '=Fields!UserName.Value', width: 1.4, color: C.muted },
    { header: 'Operaciones', value: '=Fields!OperationsCreated.Value', width: 1.1, align: 'Right', total: '=Sum(Fields!OperationsCreated.Value)' },
    { header: 'Valor registrado', value: '=Fields!AmountCreated.Value', width: 1.6, align: 'Right', format: MONEY, total: '=Sum(Fields!AmountCreated.Value)' },
    { header: 'Cambios de estado', value: '=Fields!StatusChanges.Value', width: 1.2, align: 'Right', total: '=Sum(Fields!StatusChanges.Value)' },
    { header: 'Acciones auditadas', value: '=Fields!AuditActions.Value', width: 1.2, align: 'Right', total: '=Sum(Fields!AuditActions.Value)' },
    { header: 'Última acción (UTC)', value: '=Fields!LastActionAtUtc.Value', width: 1.4, format: 'dd/MM/yyyy HH:mm' },
  ] });
  return report({
    title: 'Actividad por usuario', subtitle: periodSubtitle + ' & "  ·  requiere permiso de auditoría"', bodyHeight: 1.1,
    params: [P.userId, P.dateFrom, P.dateTo],
    dataSets: [lookupSets.ReportUsers(), dataSet('Activity', 'rpt.usp_UserActivity', periodParams, fields)],
    body,
  });
};

mkdirSync(OUT, { recursive: true });
for (const [name, build] of Object.entries(reports)) {
  writeFileSync(join(OUT, `${name}.rdl`), build(), 'utf8');
  console.log(`✔ Reports/${name}.rdl`);
}
