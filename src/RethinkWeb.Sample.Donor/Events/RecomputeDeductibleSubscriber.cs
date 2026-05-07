using RethinkWeb.Events;
using RethinkWeb.Sample.Donor.Entities;
using RethinkWeb.Storage;

namespace RethinkWeb.Sample.Donor.Events;

/// <summary>
/// The Amount → AmountDeductible rule. Currently 1:1, but lives here so it can grow
/// (premium portion, fair-market-value subtractions, etc.) without touching the form
/// handler or the action class.
///
/// Subscribes to the framework-published <c>EntitySaved&lt;Donation&gt;</c> — fires
/// regardless of which write path produced the change (web form today, MCP tool,
/// or a future workflow step).
/// </summary>
public sealed class RecomputeDeductibleSubscriber(IEntityStore<Donation> store)
    : IEventSubscriber<EntitySaved<Donation>>
{
    public async Task HandleAsync(EntitySaved<Donation> evt, IEventContext context, CancellationToken ct)
    {
        var donation = evt.Entity;
        if (donation.AmountDeductible == donation.Amount) return; // idempotent — avoid re-publishing

        donation.AmountDeductible = donation.Amount;
        await store.SaveAsync(donation, ct);
    }
}
