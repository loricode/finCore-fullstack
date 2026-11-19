using FinCore.Application.Common;
using FinCore.Application.Common.Abstractions;
using FinCore.Application.Operations;
using FluentValidation;

namespace FinCore.Application.Reconciliation;

/// <summary>
/// HU-13 · Importar un extracto CSV. El archivo se lee aquí (solo estructura); la validación de cada fila la hace
/// stg.usp_PromoteBankMovements, la misma que usa la carga desde Oracle. Respuesta: leídas, cargadas, rechazadas y por qué.
/// statements.import es un permiso de alcance empresa (lo tienen roles con branches.all).
/// </summary>
public sealed class ImportStatementHandler(
    ICurrentUser currentUser, IRequestContext request, IStatementImporter importer, IAuditLog auditLog, IUnitOfWork unitOfWork)
{
    public const long MaxFileBytes = 5 * 1024 * 1024;

    public async Task<Result<ImportResultDto>> HandleAsync(string fileName, Stream content, CancellationToken ct)
    {
        if (!currentUser.TryGetSession(out _, out var companyId)) return Guard.NotAuthenticated;

        var name = Path.GetFileName(fileName ?? "");
        if (!name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            return new Error("validation.file", "El archivo debe ser .csv.", ErrorType.Validation);

        var parsed = await CsvStatementParser.ParseAsync(content, ct);
        if (!parsed.IsValid)
            return new Error("validation.file", parsed.Error!, ErrorType.Validation);

        var result = await importer.ImportAsync(companyId, "CSV", "FinCore.Api · importación CSV", name.Length <= 260 ? name : name[..260], parsed.Rows,
            currentUser.UserName ?? "api", request.CorrelationId, ct);

        auditLog.Record(new AuditEntry(AuditActions.StatementImported, "EtlExecution", result.EtlExecutionId.ToString(),
            currentUser.UserId, currentUser.UserName, companyId,
            new { fileName = name, result.Status, result.RowsRead, result.RowsLoaded, result.RowsRejected }));
        await unitOfWork.SaveChangesAsync(ct);
        return result;
    }
}

public sealed class ListEtlExecutionsHandler(ICurrentUser currentUser, IReconciliationQueries queries)
{
    public async Task<Result<PagedResult<EtlExecutionDto>>> HandleAsync(int page, int pageSize, CancellationToken ct)
    {
        if (!currentUser.TryGetSession(out _, out var companyId)) return Guard.NotAuthenticated;
        return await queries.ListEtlExecutionsAsync(companyId, Math.Max(page, 1), Math.Clamp(pageSize, 1, 100), ct);
    }
}

public sealed class GetEtlErrorsHandler(ICurrentUser currentUser, IReconciliationQueries queries)
{
    public async Task<Result<IReadOnlyList<EtlErrorDto>>> HandleAsync(long etlExecutionId, CancellationToken ct)
    {
        if (!currentUser.TryGetSession(out _, out var companyId)) return Guard.NotAuthenticated;
        return Result<IReadOnlyList<EtlErrorDto>>.From(await queries.GetEtlErrorsAsync(companyId, etlExecutionId, 1000, ct));
    }
}

internal sealed class RunReconciliationCommandValidator : AbstractValidator<RunReconciliationCommand>
{
    public RunReconciliationCommandValidator()
    {
        RuleFor(x => x.AccountId).GreaterThan(0).WithMessage("Selecciona la cuenta.");
        RuleFor(x => x.PeriodTo).GreaterThanOrEqualTo(x => x.PeriodFrom).WithMessage("La fecha final debe ser mayor o igual a la inicial.");
        RuleFor(x => x).Must(x => x.PeriodTo.DayNumber - x.PeriodFrom.DayNumber <= 92)
            .WithName("periodTo").WithMessage("El período no puede superar 93 días.");
        RuleFor(x => x.DateToleranceDays).LessThanOrEqualTo((byte)5).WithMessage("La tolerancia debe estar entre 0 y 5 días.");
    }
}

/// <summary>
/// HU-14 · Conciliar una cuenta en un período. Valida permiso y alcance aquí; el emparejamiento lo hace SQL (ADR-003).
/// Concurrencia (HU-16): una ejecución en curso por cuenta → error SQL 50104 → 409.
/// </summary>
public sealed class RunReconciliationHandler(
    IValidator<RunReconciliationCommand> validator,
    ICurrentUser currentUser,
    IRequestContext request,
    OperationScope scope,
    IAccountRepository accounts,
    IReconciliationEngine engine,
    IReconciliationQueries queries,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<ReconciliationSummaryDto>> HandleAsync(RunReconciliationCommand command, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(command, ct);
        if (!currentUser.TryGetSession(out var userId, out var companyId)) return Guard.NotAuthenticated;

        var branches = await scope.AuthorizedBranchIdsAsync(ct);
        var account = await accounts.GetByIdAsync(command.AccountId, ct);
        if (account is null || account.CompanyId != companyId || !branches.Contains(account.BranchId))
            return Error.NotFound("account.not_found", "La cuenta no existe.");

        var id = await engine.RunAsync(command, userId, request.CorrelationId, ct);
        var summary = await queries.GetSummaryAsync(id, branches, ct);
        auditLog.Record(new AuditEntry(AuditActions.ReconciliationRun, "Reconciliation", id.ToString(), userId, currentUser.UserName,
            companyId, new { command.AccountId, command.PeriodFrom, command.PeriodTo, summary?.MatchedCount, summary?.DifferenceCount },
            BranchId: account.BranchId));
        await unitOfWork.SaveChangesAsync(ct);
        return summary is null ? Error.NotFound("reconciliation.not_found", "La conciliación no existe.") : summary;
    }
}

public sealed class ListReconciliationsHandler(OperationScope scope, IReconciliationQueries queries)
{
    public async Task<Result<PagedResult<ReconciliationSummaryDto>>> HandleAsync(int? accountId, int page, int pageSize, CancellationToken ct) =>
        await queries.ListAsync(await scope.AuthorizedBranchIdsAsync(ct), accountId, Math.Max(page, 1), Math.Clamp(pageSize, 1, 100), ct);
}

public sealed class GetReconciliationHandler(OperationScope scope, IReconciliationQueries queries)
{
    public async Task<Result<ReconciliationWithDetailsDto>> HandleAsync(int reconciliationId, CancellationToken ct)
    {
        var summary = await queries.GetSummaryAsync(reconciliationId, await scope.AuthorizedBranchIdsAsync(ct), ct);
        if (summary is null) return Error.NotFound("reconciliation.not_found", "La conciliación no existe.");
        return new ReconciliationWithDetailsDto(summary, await queries.GetDetailsAsync(reconciliationId, ct));
    }
}

/// <summary>HU-17 · Bandeja: diferencias sin resolver de la última conciliación de cada cuenta autorizada.</summary>
public sealed class GetOpenDifferencesHandler(OperationScope scope, IReconciliationQueries queries)
{
    public async Task<Result<IReadOnlyList<OpenDifferenceDto>>> HandleAsync(CancellationToken ct) =>
        Result<IReadOnlyList<OpenDifferenceDto>>.From(await queries.GetOpenDifferencesAsync(await scope.AuthorizedBranchIdsAsync(ct), ct));
}

/// <summary>HU-18 · Resolver una diferencia con nota (RC-07). Audita RECONCILIATION_RESOLVED.</summary>
public sealed class ResolveDifferenceHandler(
    ICurrentUser currentUser,
    OperationScope scope,
    IReconciliationDetailRepository details,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<Result<Unit>> HandleAsync(ResolveDifferenceCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Note))
            return new Error("validation.note", "La nota de resolución es obligatoria.", ErrorType.Validation);
        if (!currentUser.TryGetSession(out var userId, out var companyId)) return Guard.NotAuthenticated;

        var found = await details.GetWithBranchAsync(command.DetailId, ct);
        if (found is null || !(await scope.AuthorizedBranchIdsAsync(ct)).Contains(found.Value.BranchId))
            return Error.NotFound("reconciliation_detail.not_found", "La diferencia no existe.");

        var detail = found.Value.Detail;
        detail.Resolve(command.Note, userId, clock.GetUtcNow().UtcDateTime);
        auditLog.Record(new AuditEntry(AuditActions.ReconciliationResolved, "ReconciliationDetail", detail.Id.ToString(), userId,
            currentUser.UserName, companyId, new { resultType = detail.ResultType.ToString(), note = detail.ResolutionNote },
            BranchId: found.Value.BranchId));
        await unitOfWork.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
