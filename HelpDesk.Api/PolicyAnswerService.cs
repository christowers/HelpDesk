using System.Data.Common;

namespace HelpDesk.Api;

public sealed record AskRequest(string? Question);
public sealed record PolicyAnswer(string Answer, string? Citation, bool InsufficientInformation);
public sealed record PolicyExcerpt(string Citation, string Text, string ExampleQuestion, bool InsufficientInformation)
{
    public SoftwareCatalogEntry? SoftwareCatalogEntry { get; init; }
}

public interface IPolicyModel
{
    Task<PolicyAnswer> AnswerAsync(string question, PolicyExcerpt excerpt, CancellationToken cancellationToken);
}

internal sealed class PolicyAnswerService(
    PolicyStore store, SoftwareCatalog catalog, IPolicyModel model, ILogger<PolicyAnswerService> logger)
{
    private const string InstallationPolicy = "IT Policy §4.2";
    private const string ConditionalInstallationRule =
        "If the software is listed in the approved catalog, you may install it on your work computer. Otherwise, get IT approval first.";
    public async Task<PolicyAnswer> AnswerAsync(string question, CancellationToken cancellationToken)
    {
        var product = SoftwareCatalog.GetRequestedProduct(question);
        var excerpt = product is null ? null : await store.FindByCitationAsync(InstallationPolicy, cancellationToken);
        excerpt ??= await store.FindBestMatchAsync(question, cancellationToken);
        if (excerpt is null)
        {
            return new PolicyAnswer(
                "I couldn't find a clear policy match for that question. Try adding more detail.",
                Citation: null,
                InsufficientInformation: true);
        }

        if (product is not null && excerpt.Citation == InstallationPolicy)
        {
            SoftwareCatalogEntry? entry;
            try
            {
                entry = await catalog.FindAsync(product, cancellationToken);
            }
            catch (DbException ex)
            {
                cancellationToken.ThrowIfCancellationRequested();
                logger.LogWarning(ex, "Software catalog lookup failed.");
                return new PolicyAnswer(
                    $"The software catalog is unavailable. {ConditionalInstallationRule}",
                    excerpt.Citation, InsufficientInformation: true);
            }

            if (entry is null)
            {
                return new PolicyAnswer(
                    $"I couldn't verify that product in the software catalog. {ConditionalInstallationRule}",
                    excerpt.Citation, InsufficientInformation: true);
            }

            excerpt = excerpt with { SoftwareCatalogEntry = entry };
        }

        var answer = await model.AnswerAsync(question, excerpt, cancellationToken);
        var citationIsValid = answer.Citation == excerpt.Citation
            || (excerpt.SoftwareCatalogEntry is null && answer.InsufficientInformation && answer.Citation is null);

        if (string.IsNullOrWhiteSpace(answer.Answer) || !citationIsValid)
        {
            throw new InvalidModelResponseException();
        }

        // A seeded example can explicitly record that a fact is missing from its source.
        if (excerpt.SoftwareCatalogEntry is null && excerpt.InsufficientInformation
            && string.Equals(question, excerpt.ExampleQuestion, StringComparison.OrdinalIgnoreCase))
        {
            answer = answer with { InsufficientInformation = true };
        }

        return answer;
    }
}

public sealed class InvalidModelResponseException : Exception { }
