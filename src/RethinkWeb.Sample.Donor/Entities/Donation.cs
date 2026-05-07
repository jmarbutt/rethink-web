using System.ComponentModel.DataAnnotations;
using RethinkWeb.Metadata;

namespace RethinkWeb.Sample.Donor.Entities;

/// <summary>
/// Donation entity. Demonstrates the computed-field pattern: when Amount changes,
/// an event subscriber recomputes AmountDeductible. The handler logic lives in
/// Events/RecomputeDeductible.cs — it's the source of truth, regardless of whether
/// the change came from the web form, MCP, or a future workflow step.
/// </summary>
[Entity(slug: "donations", displayName: "Donations")]
public class Donation
{
    [Key]
    public Guid Id { get; set; }

    [TextBox("Donor Name", GridVisible = true, GridOrder = 1, Sample = "John Smith")]
    public string DonorName { get; set; } = string.Empty;

    [CurrencyBox("Amount", GridVisible = true, GridOrder = 2, Required = true, Sample = "100.00")]
    public decimal Amount { get; set; }

    [CurrencyBox("Amount Deductible", Disabled = true, GridVisible = true, GridOrder = 3,
        Sample = "100.00")]
    public decimal AmountDeductible { get; set; }

    [DateBox("Donation Date", GridVisible = true, GridOrder = 4)]
    public DateTime DonationDate { get; set; }

    [TextBox("Notes")]
    public string? Notes { get; set; }
}
