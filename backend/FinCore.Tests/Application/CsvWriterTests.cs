using System.Text;
using FinCore.Application.Reports;

namespace FinCore.Tests.Application;

public sealed class CsvWriterTests
{
    private sealed record Row(string Name, decimal Amount, DateOnly Date);

    private static string Csv(params Row[] rows)
    {
        var bytes = CsvWriter.Write<Row>([("Nombre", r => r.Name), ("Valor", r => r.Amount), ("Fecha", r => r.Date)], rows);
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]); // BOM: Excel muestra bien las tildes
        return Encoding.UTF8.GetString(bytes[3..]);
    }

    [Fact]
    public void Usa_punto_y_coma_coma_decimal_y_fechas_ISO()
    {
        var csv = Csv(new Row("Montería", -1250000.5m, new DateOnly(2026, 11, 19)));
        Assert.Equal("Nombre;Valor;Fecha\r\nMontería;-1250000,5;2026-11-19\r\n", csv);
    }

    [Fact]
    public void Escapa_separadores_y_comillas()
    {
        var csv = Csv(new Row("Pago; \"urgente\"", 1m, new DateOnly(2026, 1, 1)));
        Assert.Contains("\"Pago; \"\"urgente\"\"\"", csv);
    }

    [Fact]
    public void Neutraliza_formulas_pero_no_numeros_negativos()
    {
        var csv = Csv(new Row("=HYPERLINK(\"http://x\")", -5m, new DateOnly(2026, 1, 1)));
        Assert.Contains("'=HYPERLINK", csv);
        Assert.Contains(";-5;", csv);
    }
}
