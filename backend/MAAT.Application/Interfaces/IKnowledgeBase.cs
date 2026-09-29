using MAAT.Domain.Knowledge;

namespace MAAT.Application.Interfaces;

// docs/specs/documentation.md, section 2 : les articles de la base documentaire, chargés une
// fois depuis les fichiers Markdown embarqués (MAAT.Infrastructure/Knowledge). Toujours
// renvoyés dans le même ordre : l'ordre des résultats ne dépend jamais du chargement.
public interface IKnowledgeBase
{
    IReadOnlyList<KnowledgeArticle> Articles { get; }
}
