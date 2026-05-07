using Microsoft.EntityFrameworkCore;
using RethinkWeb.Sample.Donor.Entities;

namespace RethinkWeb.Sample.Donor;

public sealed class SampleContext(DbContextOptions<SampleContext> options) : DbContext(options)
{
    public DbSet<Entities.Donor> Donors => Set<Entities.Donor>();
    public DbSet<Donation> Donations => Set<Donation>();
}
