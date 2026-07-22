using Microsoft.EntityFrameworkCore;
using Momentum.Models;

namespace Momentum.Data;

public class MomentumDbContext : DbContext
{
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Habit> Habits { get; set; } = null!;
    public DbSet<HabitLog> HabitLogs { get; set; } = null!;

    public MomentumDbContext(DbContextOptions<MomentumDbContext> options)
        : base(options)
    {
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Habit>()
            .HasMany(habit => habit.Logs)
            .WithOne(log => log.Habit)
            .HasForeignKey(log => log.HabitId)
            .OnDelete(DeleteBehavior.Cascade);
        base.OnModelCreating(modelBuilder);
    }
}
