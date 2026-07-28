using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace MAAT.IntegrationTests;

// docs/specs/auth-securite-rgpd.md, section 5 : la clé de signature JWT doit faire au
// moins 32 octets. Ces tests vérifient que Program.cs refuse de démarrer sinon, sur le
// même modèle que le garde-fou IEmailSender (cf. EmailSenderStartupTests).
public class JwtSigningKeyStartupTests
{
    private static Dictionary<string, string?> ConfigWithSigningKey(string signingKey) => new()
    {
        ["ConnectionStrings:Default"] = "Host=localhost;Port=5433;Database=maat;Username=maat;Password=unused",
        ["Jwt:SigningKey"] = signingKey,
    };

    [Fact]
    public void Demarrage_avec_une_cle_de_signature_de_moins_de_32_octets_echoue()
    {
        Exception? startupFailure = null;

        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureAppConfiguration((_, config) =>
                    config.AddInMemoryCollection(ConfigWithSigningKey("cle-trop-courte")));
            });

            _ = factory.Services;
        }
        catch (Exception ex)
        {
            startupFailure = ex;
        }

        Assert.NotNull(startupFailure);
        Assert.Contains("Jwt:SigningKey", FlattenMessages(startupFailure!), StringComparison.Ordinal);
    }

    [Fact]
    public void Demarrage_avec_une_cle_de_signature_de_32_octets_reussit()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(ConfigWithSigningKey(new string('a', 32))));
        });

        Assert.NotNull(factory.Services);
    }

    private static string FlattenMessages(Exception exception)
    {
        var messages = new List<string>();
        var current = exception;
        while (current is not null)
        {
            messages.Add(current.Message);
            current = current.InnerException;
        }

        return string.Join(" | ", messages);
    }
}
