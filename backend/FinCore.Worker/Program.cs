using FinCore.Application;
using FinCore.Application.Common.Abstractions;
using FinCore.Infrastructure;
using FinCore.Worker;
using FinCore.Worker.Jobs;
using FinCore.Worker.Scheduling;
using Serilog;
using Serilog.Formatting.Compact;

// FinCore.Worker · procesos programados (Etapa 4).
//   Windows: se instala como servicio (ver docs/Deployment/windows-service.md); UseWindowsService integra el ciclo
//            de vida con el Administrador de servicios y el registro de eventos.
//   Docker/Linux: UseWindowsService no hace nada y el proceso corre como consola.
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options => options.ServiceName = "FinCore Worker");

builder.Services.AddSerilog((_, logger) =>
{
    logger.ReadFrom.Configuration(builder.Configuration)
          .Enrich.FromLogContext()
          .Enrich.WithProperty("Application", "FinCore.Worker");
    if (builder.Configuration.GetValue("Logging:Json", true)) logger.WriteTo.Console(new CompactJsonFormatter());
    else logger.WriteTo.Console();
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// La identidad de la cuenta de servicio reemplaza al usuario HTTP de la API.
builder.Services.AddSingleton<WorkerExecutionContext>();
builder.Services.AddSingleton<ICurrentUser>(sp => sp.GetRequiredService<WorkerExecutionContext>());
builder.Services.AddSingleton<IRequestContext>(sp => sp.GetRequiredService<WorkerExecutionContext>());

builder.Services.AddSingleton<WorkerStartup>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<WorkerStartup>());
builder.Services.AddHostedService<LegacyBankImportJob>();
builder.Services.AddHostedService<NightlyReconciliationJob>();
builder.Services.AddHostedService<InboundEventRecoveryJob>();
builder.Services.AddHostedService<StagingPurgeJob>();

// Un trabajo que falla no debe detener el servicio completo.
builder.Services.Configure<HostOptions>(o => o.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

await builder.Build().RunAsync();
