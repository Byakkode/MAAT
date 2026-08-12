namespace MAAT.Application.Interfaces;

// docs/specs/deploiement.md, section 6 : vérifie que la base est joignable, sans
// exposer MaatDbContext à MAAT.Api (docs/adr/0005 — seul Program.cs a le droit de le
// connaître, défendu par ArchitectureTests).
public interface IDatabaseHealthCheck
{
    Task<bool> CanConnectAsync(CancellationToken ct);
}
