---
name: charte-maat
description: Charte graphique MAAT — couleurs, typographie, espacements, styles de composants et règles d'accessibilité. À utiliser pour toute création ou modification d'interface, de composant React, de configuration Tailwind ou de graphique Recharts.
---

# Charte graphique MAAT

Design minimaliste et fonctionnel. Dominante bleu/blanc pour la confiance et la
neutralité, accent vert pour la durabilité. Ton calme, professionnel, accessible.
Priorité aux espaces blancs et à la lisibilité. Pas d'effet superflu : le focus
est sur la donnée et l'action.

## Couleurs

| Rôle | Token | HEX | Usage |
| --- | --- | --- | --- |
| Primaire | `blue-maat` | `#1565FF` | Boutons, liens, sidebar |
| Accent | `green-maat` | `#29CC6A` | Actions positives, performances |
| Alerte | `orange` | `#FFA64D` | Priorité moyenne, à surveiller |
| Erreur | `red` | `#E53935` | Alertes critiques, échecs |
| Succès | `green-light` | `#33D69F` | Confirmation d'action |
| Fond | `bg` | `#F8F9FC` | Arrière-plan des pages |
| Texte principal | `text` | `#1E1E2D` | Corps de texte, titres |
| Texte secondaire | `text-muted` | `#6B7280` | Sous-titres, légendes |
| Bordures | `border` | `#E5E7EB` | Contours, séparateurs, tableaux |

Cartes et surfaces : fond blanc `#FFFFFF`.

## Typographie

| Élément | Police | Graisse | Taille |
| --- | --- | --- | --- |
| H1, H2 | Poppins Bold | 700 | 24–32 px |
| H3, H4 | Poppins SemiBold | 600 | 18–22 px |
| Corps | Inter Regular | 400 | 14–16 px |
| Légendes | Inter Light | 300 | 12–14 px |
| Boutons, menus | Poppins Medium | 500 | 14–16 px |

## Composants

**Boutons** — rayon 8 px, ombre `0 2px 8px rgba(0,0,0,0.05)`, hover = légère
élévation + variation de luminosité.

- Primaire : fond `#1565FF`, texte blanc
- Secondaire : fond blanc, bordure et texte bleus, hover bleu clair
- Succès : fond `#33D69F`
- Alerte : fond rouge ou orange, texte blanc, hover foncé

**Cartes** — fond blanc, bordure `#E5E7EB`, rayon 12 px, ombre
`rgba(0,0,0,0.03)`, padding interne 20 px.

## Espacements et grille

- Padding global : 24 px
- Gouttières entre cartes : 16 px
- Marges latérales du contenu central : 32 px
- Grille 12 colonnes, breakpoints 1440 / 1024 / 768 / 480

## Mise en page

Sidebar gauche fixe (bleu foncé, texte blanc) · header avec recherche et menu
utilisateur à droite · contenu central en cartes modulaires.

## Iconographie

Lucide Icons, style linéaire, 24 px, traits fins. Couleur par défaut `#6B7280`,
bleu MAAT au survol.

Correspondances : Paramètres → `Settings` · Tableau de bord → `BarChart2` ·
Rapports → `FileText` · Recommandations → `Lightbulb` · Plan d'actions → `CheckCircle`

## Graphiques (Recharts)

Une couleur par domaine RSE, à ne jamais réattribuer :

- Environnement `#29CC6A`
- Social `#1E88E5`
- Éthique `#7E57C2`
- Gouvernance `#42A5F5`
- Économique `#FFB74D`

## Accessibilité

- Contraste texte/fond ≥ 4.5:1 (WCAG AA)
- Navigation clavier complète, focus visible (outline bleu clair)
- Les graphiques Recharts rendent du SVG : fournir un `<title>` et une
  alternative textuelle ou tabulaire des données pour les lecteurs d'écran
- Hiérarchie visuelle stable d'un écran à l'autre
