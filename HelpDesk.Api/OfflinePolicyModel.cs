namespace HelpDesk.Api;

internal sealed class OfflinePolicyModel : IPolicyModel
{
    public Task<PolicyAnswer> AnswerAsync(
        string question, PolicyExcerpt excerpt, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // A keyword match can locate a policy without establishing that it fully answers a paraphrase.
        var isExample = string.Equals(question, excerpt.ExampleQuestion, StringComparison.OrdinalIgnoreCase);
        return Task.FromResult(new PolicyAnswer(
            excerpt.Text, excerpt.Citation, excerpt.InsufficientInformation || !isExample));
    }
}
