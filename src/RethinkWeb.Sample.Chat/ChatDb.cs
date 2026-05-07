using Microsoft.EntityFrameworkCore;
using RethinkWeb.Sample.Chat.Entities;

namespace RethinkWeb.Sample.Chat;

public sealed class ChatDb(DbContextOptions<ChatDb> options) : DbContext(options)
{
    public DbSet<Channel> Channels => Set<Channel>();
    public DbSet<Message> Messages => Set<Message>();
}
