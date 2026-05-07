using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RethinkWeb;
using RethinkWeb.Auth;
using RethinkWeb.Events;

namespace RethinkWeb.Core.Tests;

public class EventBusTests
{
    public sealed record SomethingHappened(string What);

    public sealed class CountingSubscriber : IEventSubscriber<SomethingHappened>
    {
        public List<SomethingHappened> Received { get; } = [];

        public Task HandleAsync(SomethingHappened evt, IEventContext context, CancellationToken ct)
        {
            Received.Add(evt);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task InProcEventBus_dispatches_to_all_registered_subscribers()
    {
        var sub = new CountingSubscriber();
        var services = new ServiceCollection()
            .AddSingleton<IEventSubscriber<SomethingHappened>>(sub)
            .AddSingleton<IClock>(new FakeClock(DateTimeOffset.Parse("2026-05-06T10:00:00Z")))
            .AddSingleton<IIdGenerator>(new FakeIdGenerator(Guid.Parse("00000000-0000-0000-0000-000000000001")))
            .AddSingleton<IAuthContext, AllowAllAuthContext>()
            .AddSingleton(NullLogger<InProcEventBus>.Instance)
            .BuildServiceProvider();

        var bus = new InProcEventBus(
            services,
            services.GetRequiredService<IClock>(),
            services.GetRequiredService<IIdGenerator>(),
            services.GetRequiredService<IAuthContext>(),
            NullLogger<InProcEventBus>.Instance);

        await bus.PublishAsync(new SomethingHappened("test"));

        sub.Received.Should().ContainSingle().Which.What.Should().Be("test");
    }

    [Fact]
    public async Task InProcEventBus_publishes_event_context_with_clock_user_and_correlation_id()
    {
        IEventContext? captured = null;
        var fakeClock = new FakeClock(DateTimeOffset.Parse("2026-05-06T10:00:00Z"));
        var fakeIds = new FakeIdGenerator(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

        var sub = new DelegateSubscriber<SomethingHappened>((evt, ctx) =>
        {
            captured = ctx;
            return Task.CompletedTask;
        });

        var services = new ServiceCollection()
            .AddSingleton<IEventSubscriber<SomethingHappened>>(sub)
            .BuildServiceProvider();

        var bus = new InProcEventBus(services, fakeClock, fakeIds,
            new AllowAllAuthContext(), NullLogger<InProcEventBus>.Instance);

        await bus.PublishAsync(new SomethingHappened("ctx"));

        captured.Should().NotBeNull();
        captured!.PublishedAt.Should().Be(fakeClock.UtcNow);
        captured.CorrelationId.Should().Be(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        captured.SourceUserId.Should().Be("anonymous");
    }

    private sealed class DelegateSubscriber<T>(Func<T, IEventContext, Task> handler) : IEventSubscriber<T>
        where T : class
    {
        public Task HandleAsync(T evt, IEventContext context, CancellationToken ct) => handler(evt, context);
    }
}
