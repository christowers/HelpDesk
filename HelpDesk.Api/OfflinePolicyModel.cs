namespace HelpDesk.Api;

internal sealed class OfflinePolicyModel : IPolicyModel
{
    public Task<PolicyAnswer> AnswerAsync(
        string question, PolicyExcerpt excerpt, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (excerpt.SoftwareCatalogEntry is { } entry)
        {
            var answer = entry.IsApproved
                ? $"{entry.Product} is approved in the software catalog. You may install it on your work computer."
                : $"The software catalog lists {entry.Product} as requiring IT approval. Get approval before installing it.";
            return Task.FromResult(new PolicyAnswer(answer, excerpt.Citation, InsufficientInformation: false));
        }

        // A keyword match can locate a policy without establishing that it fully answers a paraphrase.
        var isExample = string.Equals(question, excerpt.ExampleQuestion, StringComparison.OrdinalIgnoreCase);
        return Task.FromResult(new PolicyAnswer(
            excerpt.Text, excerpt.Citation, excerpt.InsufficientInformation || !isExample));
    }
}
