using System.Reflection;
using MAAT.Infrastructure.Persistence;

namespace MAAT.IntegrationTests;

// Défend le principe de docs/adr/0005 : MAAT.Api ne doit jamais injecter MaatDbContext
// directement, sous peine de contourner les dépôts à portée d'entreprise (Diagnostic,
// Response, DomainScore, Report) et leur filtrage par company_id. Seul Program.cs a le
// droit de connaître MaatDbContext, pour l'enregistrer dans le conteneur DI
// (AddDbContext<MaatDbContext>) — jamais pour le consommer lui-même.
public class ArchitectureTests
{
    private const BindingFlags AllDeclaredMembers =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    [Fact]
    public void Aucun_type_de_MAAT_Api_ne_depend_de_MaatDbContext_hors_Program()
    {
        var apiAssembly = typeof(Program).Assembly;
        var dbContextType = typeof(MaatDbContext);

        var violations = new List<string>();

        foreach (var type in apiAssembly.GetTypes())
        {
            if (IsProgramOrNestedInProgram(type))
            {
                continue;
            }

            foreach (var constructor in type.GetConstructors(AllDeclaredMembers))
            {
                if (constructor.GetParameters().Any(p => p.ParameterType == dbContextType))
                {
                    violations.Add($"{type.FullName} : constructeur dépendant de MaatDbContext");
                }
            }

            foreach (var property in type.GetProperties(AllDeclaredMembers))
            {
                if (property.PropertyType == dbContextType)
                {
                    violations.Add($"{type.FullName}.{property.Name} : propriété de type MaatDbContext");
                }
            }

            foreach (var method in type.GetMethods(AllDeclaredMembers))
            {
                // Exclut get_X/set_X : les propriétés sont déjà couvertes ci-dessus.
                if (method.IsSpecialName)
                {
                    continue;
                }

                if (method.GetParameters().Any(p => p.ParameterType == dbContextType))
                {
                    violations.Add($"{type.FullName}.{method.Name} : paramètre de méthode de type MaatDbContext");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "Dépendance directe à MaatDbContext détectée dans MAAT.Api en dehors de Program.cs " +
            "(docs/adr/0005 — passer par un dépôt à portée d'entreprise à la place) :" +
            Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    // Couvre aussi les types que le compilateur imbrique dans Program (fermetures des
    // lambdas des top-level statements, ex. Program+<>c__DisplayClass...) : ils font
    // partie de Program.cs autant que le type Program lui-même.
    private static bool IsProgramOrNestedInProgram(Type type)
    {
        for (var current = type; current is not null; current = current.DeclaringType)
        {
            if (current == typeof(Program))
            {
                return true;
            }
        }

        return false;
    }
}
