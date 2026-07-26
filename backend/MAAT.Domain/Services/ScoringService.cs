using MAAT.Domain.Enums;

namespace MAAT.Domain.Services;

public sealed record QuestionScoreInput(
    RseDomain Domain,
    decimal Weight,
    int? Value,
    bool IsActive = true);

public sealed record DomainScoreDetail(
    RseDomain Domain,
    decimal Numerator,
    decimal Denominator,
    decimal Score,
    decimal EffectiveSectorWeight);

public sealed record ScoringResult(
    decimal GlobalScore,
    IReadOnlyList<DomainScoreDetail> DomainScores);

public sealed class IncompleteDiagnosticException : Exception
{
    public IncompleteDiagnosticException()
        : base("Le diagnostic contient au moins une question active sans réponse.")
    {
    }
}

public sealed class SectorWeightNotFoundException : Exception
{
    public SectorWeightNotFoundException(RseDomain domain)
        : base($"Le domaine '{domain}' contient des questions actives mais n'a aucune pondération dans le dictionnaire sectorWeights fourni.")
    {
        Domain = domain;
    }

    public RseDomain Domain { get; }
}

public sealed class ScoringService
{
    public ScoringResult CalculateScore(
        IReadOnlyList<QuestionScoreInput> questions,
        IReadOnlyDictionary<RseDomain, decimal> sectorWeights)
    {
        foreach (var question in questions)
        {
            if (question.Weight <= 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(questions), question.Weight, "Le poids d'une question doit être strictement positif.");
            }

            if (question.Value is < 0 or > 5)
            {
                throw new ArgumentOutOfRangeException(nameof(questions), question.Value, "La réponse à une question doit être comprise entre 0 et 5.");
            }

            if (question.IsActive && question.Value is null)
            {
                throw new IncompleteDiagnosticException();
            }
        }

        var domainScores = questions
            .Where(q => q.IsActive)
            .GroupBy(q => q.Domain)
            .Select(group =>
            {
                var numerator = group.Sum(q => q.Value!.Value * q.Weight);
                var denominator = group.Sum(q => q.Weight * 5m);
                var score = numerator / denominator * 100m;
                return (Domain: group.Key, Numerator: numerator, Denominator: denominator, Score: score);
            })
            .ToList();

        foreach (var d in domainScores)
        {
            if (!sectorWeights.ContainsKey(d.Domain))
            {
                throw new SectorWeightNotFoundException(d.Domain);
            }
        }

        var sectorWeightSum = domainScores.Sum(d => sectorWeights[d.Domain]);

        var result = domainScores
            .Select(d =>
            {
                var effectiveWeight = sectorWeights[d.Domain] / sectorWeightSum;
                return new DomainScoreDetail(d.Domain, d.Numerator, d.Denominator, d.Score, effectiveWeight);
            })
            .ToList();

        var globalScore = result.Sum(d => d.Score * d.EffectiveSectorWeight);

        return new ScoringResult(globalScore, result);
    }

    public static int RoundForDisplay(decimal score) =>
        (int)Math.Round(score, 0, MidpointRounding.AwayFromZero);
}
