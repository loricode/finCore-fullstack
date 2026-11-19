using FinCore.Domain.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinCore.Infrastructure.Persistence.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> b)
    {
        b.ToTable("Accounts", "fin");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(20);
        b.Property(x => x.Name).HasMaxLength(100);
        b.Property(x => x.BankName).HasMaxLength(100);
        b.Property(x => x.BankAccountNumber).HasMaxLength(40);
    }
}

internal sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> b)
    {
        // La tabla tiene triggers (historial, protección y bloqueo de borrado): EF Core no debe usar OUTPUT sin INTO.
        b.ToTable("Transactions", "fin", t =>
        {
            t.HasTrigger("trg_Transactions_StatusHistory");
            t.HasTrigger("trg_Transactions_Guard");
            t.HasTrigger("trg_Transactions_PreventDelete");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Type).HasColumnName("TransactionTypeId").HasConversion<byte>();
        b.Property(x => x.Status).HasColumnName("StatusId").HasConversion<byte>();
        b.Property(x => x.Reference).HasMaxLength(40).IsRequired();
        b.Property(x => x.Currency).HasMaxLength(3).IsFixedLength().IsUnicode(false);
        b.Property(x => x.Description).HasMaxLength(250);
        b.Property(x => x.CounterpartyName).HasMaxLength(150);
        b.Property(x => x.CounterpartyAccount).HasMaxLength(40);
        b.Property(x => x.RejectionReason).HasMaxLength(250);
        b.Property(x => x.Source).HasMaxLength(10).IsUnicode(false);
        b.Property(x => x.IdempotencyKey).HasMaxLength(64);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.Ignore(x => x.SignedAmount); // columna calculada en la base; el dominio la calcula igual
    }
}

internal sealed class TransactionViewConfiguration : IEntityTypeConfiguration<TransactionViewRow>
{
    public void Configure(EntityTypeBuilder<TransactionViewRow> b)
    {
        b.HasNoKey().ToView("vw_Transactions", "fin");
        b.Property(x => x.StatusId).HasColumnName("StatusId");
    }
}

internal sealed class HistoryRowConfiguration : IEntityTypeConfiguration<HistoryRow>
{
    public void Configure(EntityTypeBuilder<HistoryRow> b) => b.HasNoKey().ToView(null);
}

internal sealed class AccountRowConfiguration : IEntityTypeConfiguration<AccountRow>
{
    public void Configure(EntityTypeBuilder<AccountRow> b) => b.HasNoKey().ToView(null);
}
