using RethinkWeb.Actions;
using RethinkWeb.Sample.Donor.Entities;
using RethinkWeb.Storage;

namespace RethinkWeb.Sample.Donor.Actions;

public sealed record AddressInput(string Address1, string? Address2, string City, string State, string PostalCode);

public sealed record AddressResult(Guid DonorId, string FullAddress);

[Action(name: "update-address", displayName: "Update Address", Icon = "map-pin",
    Description = "Update the postal address on a donor record.")]
public sealed class UpdateAddressAction(IEntityStore<Entities.Donor> store)
    : IAction<Entities.Donor, AddressInput, AddressResult>
{
    public async Task<AddressResult> ExecuteAsync(
        Entities.Donor entity,
        AddressInput input,
        IActionContext context,
        CancellationToken ct = default)
    {
        entity.Address1 = input.Address1;
        entity.Address2 = input.Address2;
        entity.City = input.City;
        entity.State = input.State;
        entity.PostalCode = input.PostalCode;

        await store.SaveAsync(entity, ct);

        var full = $"{input.Address1}, {input.City}, {input.State} {input.PostalCode}";
        return new AddressResult(entity.Id, full);
    }
}
