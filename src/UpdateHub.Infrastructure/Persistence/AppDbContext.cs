using Microsoft.EntityFrameworkCore;
using UpdateHub.Domain.Entities;

namespace UpdateHub.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<App> Apps => Set<App>();
    public DbSet<Release> Releases => Set<Release>();
    public DbSet<Artifact> Artifacts => Set<Artifact>();
    public DbSet<AppSetting> Settings => Set<AppSetting>();
    public DbSet<LoginAttempt> LoginAttempts => Set<LoginAttempt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppSetting>()
            .HasKey(s => s.Key);

        modelBuilder.Entity<LoginAttempt>()
            .HasKey(a => a.IpAddress);

        modelBuilder.Entity<App>()
            .HasIndex(a => a.Slug).IsUnique();

        modelBuilder.Entity<Release>()
            .HasOne(r => r.App)
            .WithMany(a => a.Releases)
            .HasForeignKey(r => r.AppId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Artifact>()
            .HasOne(a => a.Release)
            .WithMany(r => r.Artifacts)
            .HasForeignKey(a => a.ReleaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
