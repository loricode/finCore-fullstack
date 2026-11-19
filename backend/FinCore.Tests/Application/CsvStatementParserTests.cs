using System.Text;
using FinCore.Application.Reconciliation;
using FinCore.Domain.Common;
using FinCore.Domain.Reconciliation;

namespace FinCore.Tests.Application;

public sealed class CsvStatementParserTests
{
    private static Task<CsvStatementParser.ParseResult> Parse(string csv) =>
        CsvStatementParser.ParseAsync(new MemoryStream(Encoding.UTF8.GetBytes(csv)), default);

    [Fact]
    public async Task Lee_filas_con_punto_y_coma_numero_de_linea_y_campos_vacios_como_null()
    {
        var result = await Parse("account_number;movement_id;reference;date;amount;description\n" +
                                 "123-01;M1;TRF-1;2026-11-03;-500000.00;Pago\n\n" +
                                 "123-01;M2;;2026-11-04;100;\n");

        Assert.True(result.IsValid);
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(2, result.Rows[0].RowNumber);
        Assert.Equal("-500000.00", result.Rows[0].Amount);
        Assert.Equal(4, result.Rows[1].RowNumber); // la línea vacía no cuenta como fila pero sí como línea
        Assert.Null(result.Rows[1].Reference);
        Assert.Null(result.Rows[1].Description);
    }

    [Fact]
    public async Task Detecta_coma_columnas_en_otro_orden_y_comillas()
    {
        var result = await Parse("date,amount,account_number,movement_id,reference,description\n" +
                                 "2026-11-03,1200,123-01,M1,ING-1,\"Consignación, sucursal \"\"centro\"\"\"\n");

        var row = Assert.Single(result.Rows);
        Assert.Equal("123-01", row.AccountNumber);
        Assert.Equal("2026-11-03", row.Date);
        Assert.Equal("Consignación, sucursal \"centro\"", row.Description);
    }

    [Theory]
    [InlineData("", "vacío")]
    [InlineData("cuenta;valor\n1;2\n", "Faltan columnas")]
    [InlineData("account_number;movement_id;reference;date;amount\n", "no tiene movimientos")]
    public async Task Rechaza_archivos_que_no_se_pueden_leer(string csv, string expected)
    {
        var result = await Parse(csv);
        Assert.False(result.IsValid);
        Assert.Contains(expected, result.Error);
    }

    [Fact]
    public async Task No_valida_valores_eso_lo_hace_el_procedimiento_de_promocion()
    {
        var result = await Parse("account_number;movement_id;reference;date;amount\nX;M1;R;06/11/2026;1.200.000\n");
        Assert.True(result.IsValid);
        Assert.Equal("1.200.000", Assert.Single(result.Rows).Amount);
    }
}

public sealed class ReconciliationDetailTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 15, 0, 0, DateTimeKind.Utc);

    private static ReconciliationDetail New(ReconciliationResultType type)
    {
        var detail = (ReconciliationDetail)Activator.CreateInstance(typeof(ReconciliationDetail), nonPublic: true)!;
        typeof(ReconciliationDetail).GetProperty(nameof(ReconciliationDetail.ResultType))!.SetValue(detail, type);
        return detail;
    }

    [Fact]
    public void Resolver_guarda_nota_usuario_y_fecha_una_sola_vez()
    {
        var d = New(ReconciliationResultType.MissingInternal);
        d.Resolve("  Comisión del banco, se registrará como egreso  ", 2, Now);

        Assert.True(d.IsResolved);
        Assert.Equal("Comisión del banco, se registrará como egreso", d.ResolutionNote);
        Assert.Equal(2, d.ResolvedByUserId);
        Assert.Equal("reconciliation.already_resolved", Assert.Throws<DomainException>(() => d.Resolve("otra", 2, Now)).Code);
    }

    [Fact]
    public void Una_conciliada_no_se_resuelve_y_la_nota_es_obligatoria()
    {
        Assert.Equal("reconciliation.matched_not_resolvable",
            Assert.Throws<DomainException>(() => New(ReconciliationResultType.Matched).Resolve("x", 2, Now)).Code);
        Assert.Equal("reconciliation.note_required",
            Assert.Throws<DomainException>(() => New(ReconciliationResultType.AmountMismatch).Resolve(" ", 2, Now)).Code);
    }
}
