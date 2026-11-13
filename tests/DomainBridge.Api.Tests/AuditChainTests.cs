using Xunit;

namespace DomainBridge.Api.Tests;

public sealed class AuditChainTests
{
    private static AuditEvent NewEvent(string action, string subject = "SPARE01") =>
        new(Guid.NewGuid(), DateTimeOffset.UtcNow, action, subject, AuditActors.Agent(subject));

    private static InMemoryAuditStore StoreWith(params string[] actions)
    {
        var store = new InMemoryAuditStore();
        foreach (var action in actions)
        {
            store.Append(NewEvent(action));
        }

        return store;
    }

    [Fact]
    public void Appended_events_form_an_intact_chain()
    {
        var events = StoreWith("a", "b", "c").Events.ToArray();

        Assert.Null(events[0].PreviousHash);
        Assert.Equal(events[0].Hash, events[1].PreviousHash);
        Assert.Equal(events[1].Hash, events[2].PreviousHash);
        Assert.Equal(-1, AuditChain.FindFirstBrokenLink(events));
    }

    [Fact]
    public void Editing_an_event_breaks_the_chain_at_that_event()
    {
        var events = StoreWith("a", "b", "c").Events.ToArray();
        events[1] = events[1] with { Actor = "agent:SOMEONE-ELSE" };

        Assert.Equal(1, AuditChain.FindFirstBrokenLink(events));
    }

    [Fact]
    public void Removing_an_event_breaks_the_chain_at_its_successor()
    {
        var events = StoreWith("a", "b", "c").Events.ToList();
        events.RemoveAt(1);

        Assert.Equal(1, AuditChain.FindFirstBrokenLink(events));
    }

    [Fact]
    public void Reordering_events_breaks_the_chain()
    {
        var events = StoreWith("a", "b", "c").Events.ToArray();
        (events[1], events[2]) = (events[2], events[1]);

        Assert.Equal(1, AuditChain.FindFirstBrokenLink(events));
    }

    [Fact]
    public void A_caller_cannot_supply_its_own_hash()
    {
        var store = new InMemoryAuditStore();
        store.Append(NewEvent("a") with { Hash = new string('0', 64), PreviousHash = new string('1', 64) });

        var stored = store.Events.Single();
        Assert.Null(stored.PreviousHash);
        Assert.Equal(AuditChain.ComputeHash(stored), stored.Hash);
    }

    [Fact]
    public void Field_boundaries_cannot_be_forged()
    {
        var first = AuditChain.Seal(new AuditEvent(Guid.Empty, DateTimeOffset.UnixEpoch, "ab", "c"), null);
        var second = AuditChain.Seal(new AuditEvent(Guid.Empty, DateTimeOffset.UnixEpoch, "a", "bc"), null);

        Assert.NotEqual(first.Hash, second.Hash);
    }

    [Fact]
    public void Hash_survives_storage_at_microsecond_precision()
    {
        var precise = new DateTimeOffset(2026, 11, 13, 10, 0, 0, TimeSpan.Zero).AddTicks(1234567);
        var sealedEvent = AuditChain.Seal(new AuditEvent(Guid.NewGuid(), precise, "a", "s"), null);
        var roundTripped = sealedEvent with { OccurredAtUtc = new DateTimeOffset(precise.Ticks / 10 * 10, TimeSpan.Zero) };

        Assert.Equal(sealedEvent.Hash, AuditChain.ComputeHash(roundTripped));
    }

    [Fact]
    public void Recent_events_are_returned_newest_first()
    {
        var recent = StoreWith("a", "b", "c").GetRecent(2).Select(auditEvent => auditEvent.Action).ToArray();

        Assert.Equal(["c", "b"], recent);
    }

    [Fact]
    public async Task Concurrent_appends_keep_the_chain_intact()
    {
        var store = new InMemoryAuditStore();

        await Task.WhenAll(Enumerable.Range(0, 200).Select(index => Task.Run(() => store.Append(NewEvent($"a{index}")))));

        Assert.Equal(200, store.Count);
        Assert.Equal(-1, AuditChain.FindFirstBrokenLink(store.Events.ToArray()));
    }
}
