using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Api;

internal sealed class PolicyStore(PolicyDbContext database)
{
    public async Task<PolicyExcerpt?> FindBestMatchAsync(string question, CancellationToken cancellationToken)
    {
        var normalizedQuestion = question.ToLowerInvariant();
        var terms = Regex.Matches(normalizedQuestion, "[a-z0-9]{3,}")
            .Select(match => match.Value)
            .Distinct()
            .ToArray();
        var normalizeKeywords = database.Database.IsSqlite();

        var matches = await database.Policies
            .Select(policy => new
            {
                policy.Citation,
                policy.Question,
                policy.Answer,
                policy.InsufficientInformation,
                Score = policy.Question.ToLower() == normalizedQuestion
                    ? 1000
                    : EF.Parameter(terms).Count(term =>
                        (" " + (normalizeKeywords ? policy.Keywords.ToLower() : policy.Keywords) + " ")
                            .IndexOf(" " + term + " ") >= 0)
            })
            .Where(match => match.Score > 0)
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.Citation)
            .Take(2)
            .ToListAsync(cancellationToken);

        if (matches.Count == 0 || (matches.Count == 2 && matches[0].Score == matches[1].Score))
        {
            return null;
        }

        var bestMatch = matches[0];
        return new PolicyExcerpt(bestMatch.Citation, bestMatch.Answer, bestMatch.Question,
            bestMatch.InsufficientInformation);
    }
}
