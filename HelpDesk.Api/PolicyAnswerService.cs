namespace HelpDesk.Api;

public sealed record AskRequest(string? Question);
public sealed record PolicyAnswer(string Answer, string? Citation, bool InsufficientInformation);
public sealed record PolicyExcerpt(string Citation, string Text, string ExampleQuestion, bool InsufficientInformation);

public interface IPolicyModel
{
    Task<PolicyAnswer> AnswerAsync(string question, PolicyExcerpt excerpt, CancellationToken cancellationToken);
}

internal sealed class PolicyAnswerService(PolicyStore store, IPolicyModel model)
{
    public async Task<PolicyAnswer> AnswerAsync(string question, CancellationToken cancellationToken)
    {
        var excerpt = await store.FindBestMatchAsync(question, cancellationToken);
        if (excerpt is null)
        {
            return new PolicyAnswer(
                "I couldn't find a clear policy match for that question. Try adding more detail.",
                Citation: null,
                InsufficientInformation: true);
        }

        var answer = await model.AnswerAsync(question, excerpt, cancellationToken);
        var citationIsValid = answer.Citation == excerpt.Citation
            || (answer.InsufficientInformation && answer.Citation is null);

        if (string.IsNullOrWhiteSpace(answer.Answer) || !citationIsValid)
        {
            throw new InvalidModelResponseException();
        }

        // A seeded example can explicitly record that a fact is missing from its source.
        if (excerpt.InsufficientInformation
            && string.Equals(question, excerpt.ExampleQuestion, StringComparison.OrdinalIgnoreCase))
        {
            answer = answer with { InsufficientInformation = true };
        }

        return answer;
    }
}

public sealed class InvalidModelResponseException : Exception { }
