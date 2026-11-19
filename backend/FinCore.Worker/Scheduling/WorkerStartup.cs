using FinCore.Application.Common.Abstractions;

namespace FinCore.Worker.Scheduling;

/// <summary>
/// Arranque del Worker: espera a que la base responda (en Docker puede tardar) y carga la cuenta de servicio
/// (Worker:ServiceUser, por defecto svc.integration). Los trabajos esperan a que esto termine.
/// </summary>
public sealed class WorkerStartup(
    IServiceScopeFactory scopes,
    WorkerExecutionContext context,
    IConfiguration configuration,
    ILogger<WorkerStartup> logger) : BackgroundService
{
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task WaitUntilReadyAsync(CancellationToken ct) => _ready.Task.WaitAsync(ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var userName = configuration["Worker:ServiceUser"] ?? "svc.integration";
        var attempt = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
                var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationReader>();

                var user = await users.GetByUserNameAsync(userName, stoppingToken)
                    ?? throw new InvalidOperationException($"No existe la cuenta de servicio {userName}.");
                if (!user.IsActive) throw new InvalidOperationException($"La cuenta de servicio {userName} está inactiva.");

                var permissions = await authorization.GetPermissionsAsync(user.Id, stoppingToken);
                context.Initialize(user.Id, user.CompanyId, user.UserName, permissions);
                logger.LogInformation("Worker listo como {ServiceUser} (empresa {CompanyId}, {Permissions} permisos)",
                    user.UserName, user.CompanyId, permissions.Count);
                _ready.TrySetResult();
                return;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                attempt++;
                var delay = TimeSpan.FromSeconds(Math.Min(60, 2 * attempt));
                logger.LogWarning("Worker aún no puede iniciar (intento {Attempt}): {Error}. Reintenta en {Delay}",
                    attempt, ex.Message, delay);
                await Task.Delay(delay, stoppingToken);
            }
        }
    }
}
