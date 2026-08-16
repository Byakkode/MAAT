# Charte MAAT — révision des tokens

Corrige trois défauts de la charte d'origine et met à jour les surfaces. Ne
change ni le couple typographique, ni le rayon des coins, ni la grille : ces
choix tiennent, et Poppins/Inter sont embarquées dans le rapport PDF, testées
par le cas 20 de `rapport-pdf.md`.

Ce document est la source des valeurs consommées par le skill `charte-maat`.
Aucune valeur hexadécimale n'est écrite en dur ailleurs dans le frontend.

---

## 1. Les trois corrections

**Le domaine « Économique » n'existe plus.** La charte d'origine listait cinq
couleurs de graphique dont une pour un domaine supprimé du modèle. La liste
correcte est celle de `dashboard.md` : Environnement, Social, Éthique, Achats
responsables, Gouvernance. La charte était, avec le rapport de projet, le dernier
artefact portant le modèle à quatre domaines.

**Gouvernance change de couleur.** `#42A5F5` et le Social `#1E88E5` sont deux
bleus moyens que rien ne distingue sur un radar à cinq axes projeté en salle.
Avec le bleu primaire `#1565FF`, la charte comptait trois bleus sur six couleurs.

**Le vert d'accent ne porte plus de texte.** `#29CC6A` offre environ 2,2:1 sur
blanc, alors que la section accessibilité de la charte exige 4,5:1. Il reste
utilisable en remplissage et en trait de graphique ; tout texte ou icône
porteuse de sens passe désormais par une variante foncée.

---

## 2. Palette

| Rôle | Token | Valeur | Usage |
| --- | --- | --- | --- |
| Primaire | `--maat-blue` | `#1565FF` | Boutons pleins, liens, éléments actifs |
| Primaire, texte petit | `--maat-blue-text` | `#0D4FD6` | Lien ou libellé sous 16 px |
| Accent | `--maat-green` | `#29CC6A` | Remplissages, jauges, états positifs |
| Accent, texte | `--maat-green-text` | `#12734A` | Texte et icônes de succès |
| Alerte | `--maat-amber` | `#B96D0A` | Priorité moyenne, points de vigilance |
| Erreur | `--maat-red` | `#C62828` | Erreurs, validations échouées |
| Fond de page | `--maat-bg` | `#F8F9FC` | Arrière-plan |
| Surface | `--maat-surface` | `#FFFFFF` | Cartes, panneaux |
| Texte principal | `--maat-ink` | `#1E1E2D` | Corps, titres |
| Texte secondaire | `--maat-ink-muted` | `#5B6472` | Sous-titres, légendes |
| Bordure | `--maat-border` | `#E5E7EB` | Contours, séparateurs, tableaux |

L'orange d'origine `#FFA64D` et le vert clair `#33D69F` sont retirés. Le premier
plafonne à 1,9:1 sur blanc, le second à 1,9:1 également — aucun des deux ne
pouvait porter le texte blanc que la charte leur associait, ni du texte sombre
sur fond blanc. Deux variantes assombries les remplacent, et le vert de succès
fusionne avec le vert d'accent : deux verts pour la même idée étaient une source
d'incohérence entre écrans.

`--maat-ink-muted` passe de `#6B7280` à `#5B6472` : l'ancien tombait à 4,4:1 sur
le fond de page `#F8F9FC`, juste sous le seuil AA, alors qu'il passait sur blanc
pur. Une légende lisible sur une carte et limite sur la page est le genre d'écart
qu'un audit relève et qu'aucun développeur ne voit.

---

## 3. Couleurs de domaine

| Domaine | Token | Valeur | Change ? |
| --- | --- | --- | --- |
| Environnement | `--domain-environmental` | `#1B9E5F` | assombri |
| Social | `--domain-social` | `#1E88E5` | inchangé |
| Éthique | `--domain-ethics` | `#7E57C2` | inchangé |
| Achats responsables | `--domain-procurement` | `#E08A1E` | assombri |
| Gouvernance | `--domain-governance` | `#4A5568` | **remplacé** |

Gouvernance devient un graphite. Le choix n'est pas seulement chromatique : la
gouvernance est le domaine qui encadre les quatre autres plutôt qu'un thème de
même nature, et un neutre soutenu le dit mieux qu'un troisième bleu. Il tranche
avec les quatre teintes sans entrer en concurrence avec elles.

Environnement et Achats sont assombris pour deux raisons : ils atteignent 3:1 sur
blanc, seuil AA des éléments graphiques, ce que les valeurs d'origine ne
faisaient pas ; et l'écart de clarté entre les cinq teintes devient le second
canal d'information, en plus de la teinte.

**Limite à assumer.** Aucune palette de cinq couleurs catégorielles n'est
pleinement lisible sous deutéranopie : le vert et l'ambre restent proches, comme
le violet et le bleu. L'écart de clarté réduit la collision sans la supprimer.
La couleur n'est donc jamais le seul porteur d'information :

- le tableau des cinq scores sous le radar reste obligatoire
  (`dashboard.md` section 3) ;
- chaque axe porte son libellé, jamais une pastille seule ;
- la barre pondérée de composition du score est monochrome, et c'est la lecture
  principale.

---

## 4. Typographie

Couple inchangé. Ce qui change est l'échelle et le réglage.

| Élément | Police | Graisse | Taille | Interlettrage |
| --- | --- | --- | --- | --- |
| Score global | Poppins | 700 | 64 px | `-0.02em` |
| Titre de page | Poppins | 700 | 30 px | `-0.01em` |
| Titre de section | Poppins | 600 | 20 px | normal |
| Corps | Inter | 400 | 15 px | normal |
| Légende | Inter | 400 | 13 px | normal |
| Bouton, menu | Poppins | 500 | 15 px | normal |

Poppins est une géométrique : elle ne devient intéressante qu'en grand et
resserrée. Le score affiché à 32 px comme un titre ordinaire gaspille le seul
chiffre que l'utilisateur retient.

`Inter Light 300` est retiré. Une graisse 300 à 13 px sur fond clair perd en
lisibilité ce qu'elle gagne en élégance, et la charte d'origine la destinait
précisément aux légendes.

**Chiffres tabulaires, partout où une valeur s'affiche** — scores, pondérations,
points d'impact, dates :

```css
font-variant-numeric: tabular-nums lining-nums;
```

Sans cela, les chiffres de largeurs inégales font sautiller une colonne de scores
entre deux rendus. C'est le détail qui distingue un produit de données d'un
tableau de bord générique.

**État d'application — reste à faire.** Le passage de Corps et Légende à cette
échelle (15 px / 13 px) n'a pas encore été appliqué au code : la majorité du
texte de l'application (`text-sm`, `text-xs`) sert aujourd'hui à la fois de
corps de paragraphe, de libellé de formulaire et de message d'état/erreur
(`role="alert"`), et ces trois usages ne se distinguent pas dans le composant
d'origine. Trancher lequel relève de Corps et lequel de Légende demande un
rendu visuel de chaque écran — un mauvais partage réduirait un message
d'erreur à 13 px, une régression de lisibilité, pas une amélioration. Les
tokens `--text-*` pour ces deux rôles restent donc à définir et à appliquer
dans une passe dédiée, avec capture d'écran à l'appui.

---

## 5. Surfaces et élévation

L'ombre douce des cartes est supprimée. Une bordure de 1 px la remplace.

| | Avant | Après |
| --- | --- | --- |
| Carte | ombre `0 2px 8px rgba(0,0,0,.05)` | bordure `1px solid var(--maat-border)` |
| Bouton primaire | ombre légère | aucune ombre |
| Survol | élévation | fond `#F1F5FB`, bordure `#CBD5E1` |

L'ombre portée sur des cartes plates est le marqueur le plus daté de la charte
d'origine. Les ombres sont réservées à ce qui flotte réellement au-dessus de la
page : menu déroulant, boîte de dialogue, infobulle.

Rayons inchangés : 8 px pour les contrôles, 12 px pour les cartes.

**Focus clavier**, exigence de la section accessibilité :

```css
outline: 2px solid var(--maat-blue);
outline-offset: 2px;
```

Un contour, jamais une ombre : l'ombre disparaît en mode contraste élevé.

---

## 6. Tokens

```css
:root {
  --maat-blue: #1565FF;
  --maat-blue-text: #0D4FD6;
  --maat-green: #29CC6A;
  --maat-green-text: #12734A;
  --maat-amber: #B96D0A;
  --maat-red: #C62828;

  --maat-bg: #F8F9FC;
  --maat-surface: #FFFFFF;
  --maat-ink: #1E1E2D;
  --maat-ink-muted: #5B6472;
  --maat-border: #E5E7EB;
  --maat-border-strong: #CBD5E1;

  --domain-environmental: #1B9E5F;
  --domain-social: #1E88E5;
  --domain-ethics: #7E57C2;
  --domain-procurement: #E08A1E;
  --domain-governance: #4A5568;

  --radius-control: 8px;
  --radius-card: 12px;

  --font-display: 'Poppins', system-ui, sans-serif;
  --font-body: 'Inter', system-ui, sans-serif;
}
```

Le rapport PDF lit les mêmes valeurs. Une couleur de domaine qui diverge entre
l'écran et le document ferait douter du reste — c'est le sens du contrôle de
cohérence de `rapport-pdf.md` section 5.

---

## 7. Vérifications

1. Aucune valeur hexadécimale en dur dans `frontend/src`, hors ce fichier de tokens.
2. Les cinq couleurs de domaine du générateur PDF sont identiques à celles du frontend.
3. Aucune occurrence de « Économique » comme domaine, dans le code, la charte ou les specs.
4. `#29CC6A`, `#1B9E5F` et `#E08A1E` ne portent jamais de texte.
5. Contraste ≥ 4,5:1 pour tout texte, vérifié sur `--maat-bg` et non seulement sur blanc.
6. Contraste ≥ 3:1 pour les cinq couleurs de domaine et les bordures de contrôle.
7. Focus visible au clavier sur tout élément interactif, par `outline`.
8. Le tableau des cinq scores est présent sous le radar, dans le DOM et accessible au clavier.
9. Les tests `vitest-axe` existants passent sans régression.

Les points 5 et 6 se vérifient réellement, pas en asseyant qu'ils sont respectés :
c'est la même exigence que les cas 8 et 11 de `dashboard.md`.
