namespace MAAT.Application.Interfaces;

public interface ICompromisedPasswordChecker
{
    Task<bool> IsCompromisedAsync(string password, CancellationToken ct);
}
