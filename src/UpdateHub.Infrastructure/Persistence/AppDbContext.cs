using Microsoft.EntityFrameworkCore;
using UpdateHub.Domain.Entities;

namespace UpdateHub.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<App>          Apps          => Set<App>();
    public DbSet<Release>      Releases      => Set<Release>();
    public DbSet<Artifact>     Artifacts     => Set<Artifact>();
    public DbSet<AppSetting>   Settings      => Set<AppSetting>();
    public DbSet<LoginAttempt> LoginAttempts => Set<LoginAttempt>();
    public DbSet<AuditEntry>   AuditEntries  => Set<AuditEntry>();
    public DbSet<User>                Users                => Set<User>();
    public DbSet<PasswordResetToken>  PasswordResetTokens  => Set<PasswordResetToken>();
    public DbSet<DownloadEvent>       DownloadEvents       => Set<DownloadEvent>();
    public DbSet<PersonalAccessToken> PersonalAccessTokens => Set<PersonalAccessToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppSetting>()
            .HasKey(s => s.Key);

        modelBuilder.Entity<LoginAttempt>()
            .HasKey(a => a.IpAddress);

        modelBuilder.Entity<App>()
            .HasIndex(a => a.Slug).IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username).IsUnique();

        modelBuilder.Entity<PasswordResetToken>()
            .HasIndex(t => t.TokenHash).IsUnique();

        modelBuilder.Entity<DownloadEvent>()
            .HasIndex(e => e.At);
        modelBuilder.Entity<DownloadEvent>()
            .HasOne(e => e.Artifact)
            .WithMany()
            .HasForeignKey(e => e.ArtifactId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PersonalAccessToken>()
            .HasIndex(t => t.TokenHash).IsUnique();
        modelBuilder.Entity<PersonalAccessToken>()
            .HasOne(t => t.User)
            .WithMany(u => u.Tokens)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

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
