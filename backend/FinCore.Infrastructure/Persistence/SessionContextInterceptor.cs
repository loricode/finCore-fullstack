using System.Data.Common;
using FinCore.Application.Common.Abstractions;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FinCore.Infrastructure.Persistence;

/// <summary>
/// Al abrir cada conexión fija en SQL Server el usuario y el CorrelationId de la petición
/// (sp_set_session_context). El trigger fin.trg_Transactions_StatusHistory los lee para dejar en el historial
/// quién hizo el cambio y en qué petición. Se fija en cada apertura porque el pool reinicia el contexto
/// de sesión al reutilizar una conexión.
/// </summary>
internal sealed class SessionContextInterceptor(ICurrentUser currentUser, IRequestContext request) : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = CreateCommand(connection);
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await using var command = CreateCommand(connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private DbCommand CreateCommand(DbConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText =
            "EXEC sys.sp_set_session_context @key = N'UserId', @value = @userId; " +
            "EXEC sys.sp_set_session_context @key = N'CorrelationId', @value = @correlationId;";

        var userId = command.CreateParameter();
        userId.ParameterName = "@userId";
        userId.Value = (object?)currentUser.UserId ?? DBNull.Value;
        command.Parameters.Add(userId);

        var correlationId = command.CreateParameter();
        correlationId.ParameterName = "@correlationId";
        correlationId.Value = request.CorrelationId;
        command.Parameters.Add(correlationId);

        return command;
    }
}
