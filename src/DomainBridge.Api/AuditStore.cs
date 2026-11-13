using System.Globalization;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// One immutable fact: who did what, to which subject, with which outcome.
/// <see cref="PreviousHash"/> and <see cref="Hash"/> are set by the store and link
/// each event to the one before it, so that removing or editing an event is detectable.
/// </summary>
public sealed record AuditEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    string Action,
    string SubjectId,
    string Actor = AuditActors.System,
    string Outcome = AuditOutcomes.Success,
    string? TaskId = null,
    string? PreviousHash = null,
    string? Hash = null);

public static class AuditActors
{
    public const string System = "system";
    public const string UnauthenticatedAgent = "agent:unauthenticated";

    public static string Agent(string? deviceId) =>
        string.IsNullOrWhiteSpace(deviceId) ? UnauthenticatedAgent : $"agent:{deviceId}";
}

public static class AuditOutcomes
{
    public const string Success = "success";
    public const string Denied = "denied";
    public const string Failure = "failure";
}

public static class AuditChain
{
    /// <summary>Returns the event linked to its predecessor and sealed with its own hash.</summary>
    public static AuditEvent Seal(AuditEvent auditEvent, string? previousHash)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        var linked = auditEvent with { PreviousHash = previousHash, Hash = null };
        return linked with { Hash = ComputeHash(linked) };
    }

    public static string ComputeHash(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        // Length-prefixed fields: no value can imitate a field boundary.
        // Time is hashed at microsecond precision, the precision every store keeps.
        string[] fields =
        [
            auditEvent.PreviousHash ?? string.Empty,
            auditEvent.EventId.ToString("N"),
            (auditEvent.OccurredAtUtc.UtcTicks / 10).ToString(CultureInfo.InvariantCulture),
            auditEvent.Action,
            auditEvent.SubjectId,
            auditEvent.Actor,
            auditEvent.Outcome,
            auditEvent.TaskId ?? string.Empty
        ];
        var builder = new StringBuilder();
        foreach (var field in fields)
        {
            builder.Append(field.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(field);
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    /// <summary>
    /// Checks events given in append order. Returns -1 when the chain is intact,
    /// otherwise the index of the first event that was altered, reordered, or that
    /// follows a removed event.
    /// </summary>
    public static int FindFirstBrokenLink(IReadOnlyList<AuditEvent> eventsInAppendOrder)
    {
        ArgumentNullException.ThrowIfNull(eventsInAppendOrder);

        string? previousHash = null;
        for (var index = 0; index < eventsInAppendOrder.Count; index++)
        {
            var current = eventsInAppendOrder[index];
            if (!string.Equals(current.PreviousHash, previousHash, StringComparison.Ordinal) ||
                !string.Equals(current.Hash, ComputeHash(current), StringComparison.Ordinal))
            {
                return index;
            }

            previousHash = current.Hash;
        }

        return -1;
    }
}

public interface IAuditStore
{
    /// <summary>Every event, in append order.</summary>
    IReadOnlyCollection<AuditEvent> Events { get; }

    int Count { get; }

    IReadOnlyCollection<AuditEvent> GetRecent(int limit);

    void Append(AuditEvent auditEvent);
}

public sealed class InMemoryAuditStore : IAuditStore
{
    private readonly List<AuditEvent> _events = [];
    private readonly Lock _gate = new();

    public IReadOnlyCollection<AuditEvent> Events
    {
        get
        {
            lock (_gate)
            {
                return _events.ToArray();
            }
        }
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _events.Count;
            }
        }
    }

    public void Append(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        lock (_gate)
        {
            if (_events.Exists(existing => existing.EventId == auditEvent.EventId))
            {
                // Same event delivered twice: recording it once is the correct outcome.
                return;
            }

            _events.Add(AuditChain.Seal(auditEvent, _events.Count == 0 ? null : _events[^1].Hash));
        }
    }

    public IReadOnlyCollection<AuditEvent> GetRecent(int limit)
    {
        lock (_gate)
        {
            return _events.AsEnumerable().Reverse().Take(limit).ToArray();
        }
    }
}
