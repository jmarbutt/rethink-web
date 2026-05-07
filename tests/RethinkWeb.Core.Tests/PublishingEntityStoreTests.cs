using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RethinkWeb;
using RethinkWeb.Auth;
using RethinkWeb.Events;
using RethinkWeb.Storage;

namespace RethinkWeb.Core.Tests;

public class PublishingEntityStoreTests
{
    public sealed class Widget
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
    }

    private sealed class Recorder : IEventSubscriber<EntitySaved<Widget>>
    {
        public List<Widget> Received { get; } = [];
        public Task HandleAsync(EntitySaved<Widget> evt, IEventContext context, CancellationToken ct)
        {
            Received.Add(evt.Entity);
            return Task.CompletedTask;
        }
    }

    private static (PublishingEntityStore<Widget>, Recorder) BuildStore()
    {
        var recorder = new Recorder();
        var services = new ServiceCollection()
            .AddSingleton<IEventSubscriber<EntitySaved<Widget>>>(recorder)
            .BuildServiceProvider();
        var bus = new InProcEventBus(
            services,
            new SystemClock(),
            new GuidIdGenerator(),
            new AllowAllAuthContext(),
            NullLogger<InProcEventBus>.Instance);
        var store = new PublishingEntityStore<Widget>(new InMemoryEntityStore<Widget>(), bus);
        return (store, recorder);
    }

    [Fact]
    public async Task SaveAsync_publishes_EntitySaved_for_every_call()
    {
        var (store, recorder) = BuildStore();
        var w = new Widget { Id = Guid.NewGuid(), Name = "first" };

        await store.SaveAsync(w);
        await store.SaveAsync(w);

        recorder.Received.Should().HaveCount(2,
            "every save publishes — that's how subscribers see action / form / MCP writes uniformly");
    }

    [Fact]
    public async Task Subscriber_calling_SaveAsync_does_not_recurse_infinitely()
    {
        // Subscribe a handler that itself saves the entity. Without the recursion
        // guard, this would loop forever and stack-overflow.
        var saveCount = 0;
        var resaver = new ResaveSubscriber(saveCount: c => saveCount = c);

        var services = new ServiceCollection()
            .AddSingleton<IEventSubscriber<EntitySaved<Widget>>>(resaver)
            .BuildServiceProvider();
        var bus = new InProcEventBus(services,
            new SystemClock(), new GuidIdGenerator(), new AllowAllAuthContext(),
            NullLogger<InProcEventBus>.Instance);
        var store = new PublishingEntityStore<Widget>(new InMemoryEntityStore<Widget>(), bus);
        resaver.Store = store;

        var w = new Widget { Id = Guid.NewGuid(), Name = "first" };
        await store.SaveAsync(w);

        // The subscriber wrote once; the recursion guard prevented re-publish so the
        // subscriber wasn't re-invoked. Save count = original (1) + subscriber's (1) = 2.
        saveCount.Should().Be(1, "subscriber must be invoked exactly once per outer save");
    }

    private sealed class ResaveSubscriber(Action<int> saveCount) : IEventSubscriber<EntitySaved<Widget>>
    {
        private int _count;
        public PublishingEntityStore<Widget>? Store { get; set; }
        public async Task HandleAsync(EntitySaved<Widget> evt, IEventContext context, CancellationToken ct)
        {
            _count++;
            saveCount(_count);
            // Re-save: the recursion guard must prevent this from re-firing the event.
            await Store!.SaveAsync(evt.Entity, ct);
        }
    }
}
