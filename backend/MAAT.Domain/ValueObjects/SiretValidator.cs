namespace MAAT.Domain.ValueObjects;

public static class SiretValidator
{
    public static void Validate(string siret)
    {
        if (siret.Length != 14 || !siret.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("Le SIRET doit contenir exactement 14 chiffres.", nameof(siret));
        }

        if (!HasValidLuhnKey(siret))
        {
            throw new ArgumentException("La clé de Luhn du SIRET est invalide.", nameof(siret));
        }
    }

    private static bool HasValidLuhnKey(string siret)
    {
        var sum = 0;

        for (var i = 0; i < siret.Length; i++)
        {
            var digit = siret[siret.Length - 1 - i] - '0';

            if (i % 2 == 1)
            {
                digit *= 2;
                if (digit > 9)
                {
                    digit -= 9;
                }
            }

            sum += digit;
        }

        return sum % 10 == 0;
    }
}
