namespace MAAT.IntegrationTests;

// docs/specs/rapport-pdf.md, section 3 : l'horloge du générateur de rapport est injectée
// (TimeProvider) précisément pour pouvoir la figer en test — voir ReportDeterminismTests,
// cas 10 à 12, où deux générations doivent produire des octets strictement identiques.
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
