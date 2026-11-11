using System.Collections.Concurrent;

public sealed record AuditEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    string Action,
    string SubjectId);

public interface IAuditStore
{
    IReadOnlyCollection<AuditEvent> Events { get; }

    void Append(AuditEvent auditEvent);
}

public sealed class InMemoryAuditStore : IAuditStore
{
    private readonly ConcurrentQueue<AuditEvent> _events = new();

    public IReadOnlyCollection<AuditEvent> Events => _events.ToArray();

    public void Append(AuditEvent auditEvent)
    {
        _events.Enqueue(auditEvent);
    }
}
