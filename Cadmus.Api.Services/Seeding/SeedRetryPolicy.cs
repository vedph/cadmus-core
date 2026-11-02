using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Npgsql;
using Polly;
using Polly.Retry;
using System;
using System.Net.Sockets;

namespace Cadmus.Api.Services.Seeding;

/// <summary>
/// Retry policy for database initialization at startup. In a containerized
/// stack the API can start before its database services are ready to accept
/// connections, so transient connection errors are retried with increasing
/// delays (10, 30, 60 seconds). Other errors (e.g. invalid configuration or
/// SQL) are not retried, as retrying would not fix them.
/// </summary>
public static class SeedRetryPolicy
{
    private static readonly TimeSpan[] _delays =
    [
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(60)
    ];

    /// <summary>
    /// Determines whether the specified exception is a transient
    /// connection error worth retrying.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns>True if transient.</returns>
    public static bool IsTransient(Exception? exception)
    {
        return exception switch
        {
            null => false,
            // includes connection refused and server starting up (57P03)
            NpgsqlException npgsql => npgsql.IsTransient,
            // wrong credentials are not going to change
            MongoAuthenticationException => false,
            MongoConnectionException => true,
            // MongoDB server selection timeout
            TimeoutException => true,
            SocketException => true,
            _ => IsTransient(exception.InnerException)
        };
    }

    /// <summary>
    /// Creates the retry policy.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="operation">The name of the operation being retried,
    /// used in log messages.</param>
    /// <returns>Policy.</returns>
    /// <exception cref="ArgumentNullException">logger or operation</exception>
    public static AsyncRetryPolicy Create(ILogger logger, string operation)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(operation);

        return Policy.Handle<Exception>(IsTransient)
            .WaitAndRetryAsync(_delays, (exception, delay, attempt, _) =>
            {
                logger.LogWarning(exception,
                    "{Operation}: database not reachable " +
                    "(attempt {Attempt}/{Max}), retrying in {Delay}: {Error}",
                    operation, attempt, _delays.Length, delay,
                    exception.Message);
            });
    }
}
