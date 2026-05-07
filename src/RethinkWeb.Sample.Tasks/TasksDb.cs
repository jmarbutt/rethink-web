using Microsoft.EntityFrameworkCore;
using RethinkWeb.Sample.Tasks.Entities;

namespace RethinkWeb.Sample.Tasks;

public sealed class TasksDb(DbContextOptions<TasksDb> options) : DbContext(options)
{
    public DbSet<Todo> Todos => Set<Todo>();
}
