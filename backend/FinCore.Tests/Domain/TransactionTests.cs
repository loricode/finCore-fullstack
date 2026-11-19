using FinCore.Domain.Common;
using FinCore.Domain.Operations;

namespace FinCore.Tests.Domain;

/// <summary>Máquina de estados y reglas de la operación (spec operaciones-financieras, RN-01 a RN-08).</summary>
public sealed class TransactionTests
{
    // 19/11/2026 15:00 UTC = 10:00 en Colombia
    private static readonly DateTime Now = new(2026, 11, 19, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 11, 19);
    private const int Operator = 3, Supervisor = 2;

    private static Account NewAccount()
    {
        var account = (Account)Activator.CreateInstance(typeof(Account), nonPublic: true)!;
        foreach (var (name, value) in new (string, object)[] { ("Id", 10), ("CompanyId", 1), ("BranchId", 1), ("IsActive", true) })
            typeof(Account).GetProperty(name)!.SetValue(account, value);
        return account;
    }

    private static Transaction Register(TransactionType type = TransactionType.Expense, decimal amount = 150_000m,
        string? counterparty = "Proveedor", string? counterpartyAccount = "123", DateOnly? date = null) =>
        Transaction.Register(new NewTransaction(NewAccount(), type, " pag-1009 ", date ?? Today, amount, "Pago",
            counterparty, counterpartyAccount, null), Operator, Now);

    [Fact]
    public void Se_registra_pendiente_con_referencia_normalizada_y_sentido_del_tipo()
    {
        var t = Register(TransactionType.Payment);

        Assert.Equal(TransactionStatus.Pending, t.Status);
        Assert.Equal("PAG-1009", t.Reference);
        Assert.Equal(-1, t.Direction);
        Assert.Equal(-150_000m, t.SignedAmount);
        Assert.Equal(1, t.BranchId);
        Assert.NotEqual(Guid.Empty, t.PublicId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10.555)]
    [InlineData(5_000_000_001)]
    public void Rechaza_valores_invalidos(decimal amount) =>
        Assert.Equal("transaction.invalid_amount", Assert.Throws<DomainException>(() => Register(amount: amount)).Code);

    [Fact]
    public void Rechaza_fechas_futuras_o_de_hace_mas_de_30_dias()
    {
        Assert.Throws<DomainException>(() => Register(date: Today.AddDays(1)));
        Assert.Throws<DomainException>(() => Register(date: Today.AddDays(-31)));
        Assert.Equal(TransactionStatus.Pending, Register(date: Today.AddDays(-30)).Status);
    }

    [Fact]
    public void Transferencia_exige_contraparte_y_cuenta_destino() =>
        Assert.Equal("transaction.counterparty_account_required",
            Assert.Throws<DomainException>(() => Register(TransactionType.Transfer, counterpartyAccount: null)).Code);

    [Fact]
    public void No_se_registra_una_reversion_directamente() =>
        Assert.Throws<DomainException>(() => Register(TransactionType.Reversal));

    [Fact]
    public void Procesar_y_no_procesar_dos_veces()
    {
        var t = Register();
        t.Process(Supervisor, Now);

        Assert.Equal(TransactionStatus.Processed, t.Status);
        Assert.Equal(Now, t.ProcessedAtUtc);
        Assert.Equal("transaction.invalid_transition", Assert.Throws<DomainException>(() => t.Process(Supervisor, Now)).Code);
    }

    [Fact]
    public void Doble_control_desde_diez_millones()
    {
        var big = Register(amount: TransactionRules.DualControlThreshold);
        Assert.Equal("transaction.dual_control", Assert.Throws<DomainException>(() => big.Process(Operator, Now)).Code);

        big.Process(Supervisor, Now);
        Assert.Equal(TransactionStatus.Processed, big.Status);

        var small = Register(amount: TransactionRules.DualControlThreshold - 0.01m);
        small.Process(Operator, Now); // debajo del umbral, quien registra puede procesar
        Assert.Equal(TransactionStatus.Processed, small.Status);
    }

    [Fact]
    public void Rechazar_exige_motivo_y_es_final()
    {
        var t = Register();
        Assert.Throws<DomainException>(() => t.Reject(" ", Supervisor, Now));

        t.Reject("Sin soporte", Supervisor, Now);
        Assert.Equal(TransactionStatus.Rejected, t.Status);
        Assert.Equal("Sin soporte", t.RejectionReason);
        Assert.Throws<DomainException>(() => t.Process(Supervisor, Now));
        Assert.Throws<DomainException>(() => t.Reverse("x", Supervisor, Now));
    }

    [Fact]
    public void Revertir_crea_la_contrapartida_procesada_y_deja_la_original_revertida()
    {
        var original = Register(TransactionType.Payment);
        original.Process(Supervisor, Now);

        var reversal = original.Reverse("Pago duplicado", Supervisor, Now);

        Assert.Equal(TransactionStatus.Reversed, original.Status);
        Assert.Equal(TransactionType.Reversal, reversal.Type);
        Assert.Equal(TransactionStatus.Processed, reversal.Status);
        Assert.Equal(original.Amount, reversal.Amount);
        Assert.Equal(-original.Direction, reversal.Direction);
        Assert.Equal(0, original.SignedAmount + reversal.SignedAmount);
        Assert.Equal("REV-PAG-1009", reversal.Reference);
        Assert.Equal(Today, reversal.OperationDate);
        Assert.Contains("Pago duplicado", reversal.Description);
    }

    [Fact]
    public void No_se_revierte_una_pendiente_ni_una_reversion_ni_dos_veces()
    {
        var pending = Register();
        Assert.Throws<DomainException>(() => pending.Reverse("x", Supervisor, Now));

        pending.Process(Supervisor, Now);
        var reversal = pending.Reverse("x", Supervisor, Now);
        Assert.Throws<DomainException>(() => pending.Reverse("x", Supervisor, Now));
        Assert.Equal("transaction.reversal_of_reversal", Assert.Throws<DomainException>(() => reversal.Reverse("x", Supervisor, Now)).Code);
    }

    [Fact]
    public void Solo_una_pendiente_se_edita_y_solo_por_su_creador_o_un_supervisor()
    {
        var t = Register();
        var changes = new PendingTransactionChanges(Today, 200_000m, "Corregido", "Proveedor", null);

        Assert.Equal("transaction.edit_forbidden",
            Assert.Throws<DomainException>(() => t.UpdatePending(changes, userId: 99, canEditOthers: false, Now)).Code);

        t.UpdatePending(changes, Operator, canEditOthers: false, Now);
        Assert.Equal(200_000m, t.Amount);

        t.Process(Supervisor, Now);
        Assert.Throws<DomainException>(() => t.UpdatePending(changes, Supervisor, canEditOthers: true, Now));
    }

    [Fact]
    public void La_referencia_de_la_reversion_no_supera_40_caracteres() =>
        Assert.Equal(40, Transaction.ReversalReference(new string('A', 40)).Length);
}
