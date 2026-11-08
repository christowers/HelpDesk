using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Api;

internal sealed class PolicyDbContext(DbContextOptions<PolicyDbContext> options) : DbContext(options)
{
    public DbSet<Policy> Policies => Set<Policy>();
    public DbSet<SoftwareCatalogItem> SoftwareCatalog => Set<SoftwareCatalogItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var schema = Database.IsSqlServer() ? "dbo" : null;
        var policies = modelBuilder.Entity<Policy>();
        policies.ToTable("Policies", schema);
        modelBuilder.Entity<SoftwareCatalogItem>().ToTable("SoftwareCatalog", schema);
        policies.HasIndex(policy => policy.Question).IsUnique();
    }

    public async Task InitializeSqliteAsync()
    {
        await using var script = typeof(PolicyDbContext).Assembly
            .GetManifestResourceStream("HelpDesk.Api.Data.Seed.Sqlite.sql")
            ?? throw new InvalidOperationException("The SQLite setup script is missing.");
        using var reader = new StreamReader(script);
        var sql = await reader.ReadToEndAsync();

        await using var transaction = await Database.BeginTransactionAsync();
        await Database.ExecuteSqlRawAsync(sql);
        await transaction.CommitAsync();
    }
}

internal sealed class Policy
{
    [Key, MaxLength(40)]
    public required string Citation { get; init; }

    [MaxLength(240)]
    public required string Question { get; init; }

    [MaxLength(1000)]
    public required string Answer { get; init; }

    [MaxLength(400)]
    public required string Keywords { get; init; }

    public bool InsufficientInformation { get; init; }
}
