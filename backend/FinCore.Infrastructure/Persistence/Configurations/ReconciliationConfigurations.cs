using FinCore.Domain.Reconciliation;
using FinCore.Infrastructure.Reconciliation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinCore.Infrastructure.Persistence.Configurations;

internal sealed class ReconciliationDetailConfiguration : IEntityTypeConfiguration<ReconciliationDetail>
{
    public void Configure(EntityTypeBuilder<ReconciliationDetail> b)
    {
        b.ToTable("ReconciliationDetails", "rec");
        b.HasKey(x => x.Id);
        b.Property(x => x.ResultType).HasColumnName("ResultTypeId").HasConversion<byte>();
        b.Property(x => x.Notes).HasMaxLength(250);
        b.Property(x => x.ResolutionNote).HasMaxLength(500);
        b.Ignore(x => x.IsResolved);
        b.Ignore(x => x.RequiresAction);
    }
}

internal sealed class ReconciliationSummaryRowConfiguration : IEntityTypeConfiguration<ReconciliationSummaryRow>
{
    public void Configure(EntityTypeBuilder<ReconciliationSummaryRow> b) => b.HasNoKey().ToView("vw_ReconciliationSummary", "rec");
}

internal sealed class EtlExecutionRowConfiguration : IEntityTypeConfiguration<EtlExecutionRow>
{
    public void Configure(EntityTypeBuilder<EtlExecutionRow> b) => b.HasNoKey().ToView("vw_ExecutionSummary", "etl");
}

internal sealed class ReconciliationDetailRowConfiguration : IEntityTypeConfiguration<ReconciliationDetailRow>
{
    public void Configure(EntityTypeBuilder<ReconciliationDetailRow> b) => b.HasNoKey().ToView(null);
}

internal sealed class OpenDifferenceRowConfiguration : IEntityTypeConfiguration<OpenDifferenceRow>
{
    public void Configure(EntityTypeBuilder<OpenDifferenceRow> b) => b.HasNoKey().ToView(null);
}

internal sealed class EtlErrorRowConfiguration : IEntityTypeConfiguration<EtlErrorRow>
{
    public void Configure(EntityTypeBuilder<EtlErrorRow> b) => b.HasNoKey().ToView(null);
}
