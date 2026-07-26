using MAAT.Domain.Enums;

namespace MAAT.Domain.Entities;

public class Question
{
    public Guid Id { get; private set; }
    public string Code { get; set; } = default!;
    public string Text { get; set; } = default!;
    public string? HelpText { get; set; }
    public RseDomain Domain { get; set; }
    public decimal Weight { get; set; }
    public int DisplayOrder { get; set; }
    public string? VsmeRef { get; set; }
    public string? IsoRef { get; set; }
    public string? GriRef { get; set; }
    public string? EcovadisRef { get; set; }
    public bool IsActive { get; set; } = true;

    private Question()
    {
    }

    public Question(string code, string text, RseDomain domain, decimal weight, int displayOrder)
    {
        if (weight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(weight), weight, "Le poids d'une question doit être strictement positif.");
        }

        Id = Guid.NewGuid();
        Code = code;
        Text = text;
        Domain = domain;
        Weight = weight;
        DisplayOrder = displayOrder;
    }
}
