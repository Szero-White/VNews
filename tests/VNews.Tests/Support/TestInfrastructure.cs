using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using VNews.Application.Interfaces;
using VNews.Infrastructure.Services;

namespace VNews.Tests.Support;

public sealed record CapturedLog(string Category, LogLevel Level, string Message, Exception? Exception, string State);

/// <summary>In-memory log sink so tests can assert what the application actually logged.</summary>
public sealed class LogSink : ILoggerProvider
{
    private readonly ConcurrentQueue<CapturedLog> _entries = new();

    public IReadOnlyList<CapturedLog> Entries => _entries.ToArray();

    public ILogger CreateLogger(string categoryName) => new SinkLogger(categoryName, _entries);

    public void Dispose() { }

    private sealed class SinkLogger : ILogger
    {
        private readonly string _category;
        private readonly ConcurrentQueue<CapturedLog> _entries;

        public SinkLogger(string category, ConcurrentQueue<CapturedLog> entries)
        {
            _category = category;
            _entries = entries;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var structured = state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? string.Join(";", pairs.Select(p => $"{p.Key}={p.Value}"))
                : string.Empty;
            _entries.Enqueue(new CapturedLog(_category, logLevel, formatter(state, exception), exception, structured));
        }
    }
}

/// <summary>Lets a test make the audit-log write fail, to prove the business change rolls back with it.</summary>
public sealed class AuditFailureSwitch
{
    public volatile bool Fail;
}

public sealed class SwitchableAuditLogService : IAuditLogService
{
    private readonly AuditLogService _inner;
    private readonly AuditFailureSwitch _switch;

    public SwitchableAuditLogService(AuditLogService inner, AuditFailureSwitch failureSwitch)
    {
        _inner = inner;
        _switch = failureSwitch;
    }

    public Task LogAsync(string userId, string userDisplayName, string action, string entityType, string entityId, string? detail = null, CancellationToken cancellationToken = default)
    {
        if (_switch.Fail)
        {
            throw new InvalidOperationException("Simulated audit-log failure (test).");
        }

        return _inner.LogAsync(userId, userDisplayName, action, entityType, entityId, detail, cancellationToken);
    }
}

/// <summary>Simple typed logger that records into a list, for unit tests that construct services directly.</summary>
public sealed class ListLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => Entries.Add((logLevel, formatter(state, exception)));
}
