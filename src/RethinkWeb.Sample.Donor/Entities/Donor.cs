using System.ComponentModel.DataAnnotations;
using RethinkWeb.Metadata;

namespace RethinkWeb.Sample.Donor.Entities;

/// <summary>
/// Cut-down port of CoolFocus's ViewDonor. ~15 attributes covering each FieldKind
/// the renderer supports, enough to feel real without porting 80 fields.
/// </summary>
[Entity(slug: "donors", displayName: "Donors")]
public class Donor
{
    [Key]
    public Guid Id { get; set; }

    [TextBox("First Name", GridVisible = true, GridOrder = 1, Required = true, Sample = "John")]
    public string FirstName { get; set; } = string.Empty;

    [TextBox("Last Name", GridVisible = true, GridOrder = 2, Required = true, Sample = "Smith")]
    public string LastName { get; set; } = string.Empty;

    [TextBox("Primary Email", GridVisible = true, GridOrder = 3, Sample = "john@example.com")]
    public string? PrimaryEmail { get; set; }

    [PhoneBox("Phone Number", Sample = "(555) 123-4567")]
    public string? Phone { get; set; }

    [TextBox("Address Line 1", Sample = "123 Main St")]
    public string? Address1 { get; set; }

    [TextBox("Address Line 2", Sample = "Apt 4B")]
    public string? Address2 { get; set; }

    [TextBox("City", GridVisible = true, GridOrder = 4, Sample = "Springfield")]
    public string? City { get; set; }

    [TextBox("State", Sample = "IL")]
    public string? State { get; set; }

    [TextBox("Postal Code", Sample = "62701")]
    public string? PostalCode { get; set; }

    [DateBox("Date of Birth", Sample = "1980-05-15")]
    public DateTime? DateOfBirth { get; set; }

    [CheckBox("Active Donor")]
    public bool Active { get; set; } = true;

    [CheckBox("No Solicitation")]
    public bool NoSolicit { get; set; }

    [CurrencyBox("Year-To-Date Total", Disabled = true, GridVisible = true, GridOrder = 5, Sample = "$1,500.00")]
    public decimal YearToDateTotal { get; set; }

    [TextBox("Notes", Multiline = true, MaxLength = 1000)]
    public string? Notes { get; set; }
}
