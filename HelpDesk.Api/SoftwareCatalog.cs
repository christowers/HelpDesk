using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Api;

public sealed record SoftwareCatalogEntry(string Product, bool IsApproved);

internal sealed class SoftwareCatalog(PolicyDbContext database)
{
    private static readonly Regex ProductQuestion = new(
        @"\A(?:(?:can|may|could) I|am I allowed to)\s+install\s+(?<product>.+?)(?:\s+on\s+(?:my|a|the)\s+(?:work|company)\s+(?:laptop|computer|desktop|device))?[?!.]*\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static string? GetRequestedProduct(string question)
    {
        var match = ProductQuestion.Match(question);
        return match.Success
            ? Regex.Replace(match.Groups["product"].Value.Trim(), @"\s+", " ").ToLowerInvariant()
            : null;
    }

    public async Task<SoftwareCatalogEntry?> FindAsync(string product, CancellationToken cancellationToken)
    {
        var item = await database.SoftwareCatalog
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.CatalogKey == product, cancellationToken);

        return item is not null && item.CatalogKey == product
            ? new SoftwareCatalogEntry(item.Product, item.IsApproved)
            : null;
    }
}

internal sealed class SoftwareCatalogItem
{
    [Key, MaxLength(160)]
    public required string CatalogKey { get; init; }

    [MaxLength(160)]
    public required string Product { get; init; }

    public bool IsApproved { get; init; }
}
