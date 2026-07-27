namespace MAAT.Infrastructure.Security;

public class JwtOptions
{
    public string SigningKey { get; set; } = default!;
    public string Issuer { get; set; } = "maat-api";
    public string Audience { get; set; } = "maat-client";
    public int AccessTokenLifetimeSeconds { get; set; } = 900;
}
