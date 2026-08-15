# 0008 — Correspondance ISO 26000 et frontières entre les cinq domaines RSE

## Statut

Acceptée — 2026-08-12

## Contexte

`docs/specs/referentiel.md`, section 2, impose de consigner deux décisions
dans une ADR plutôt que de les laisser vivre uniquement en tête d'un rédacteur
de contenu : la correspondance entre les sept domaines d'action de l'ISO 26000
et les cinq domaines `RseDomain` retenus par MAAT, et les règles de frontière
entre ces cinq domaines une fois qu'ils coexistent.

C'est la première question qu'un jury connaissant la norme posera, et c'est
aussi la question qu'un rédacteur de contenu se pose à chaque nouvelle
question du référentiel : « ce sujet relève-t-il de tel domaine ou de tel
autre ? ». Sans réponse écrite, l'arbitrage varie d'une question à l'autre.

## Décision

### Correspondance des sept domaines d'action vers les cinq domaines MAAT

| Domaine MAAT | Domaines d'action ISO 26000 couverts |
| --- | --- |
| `Governance` | Gouvernance de l'organisation |
| `Social` | Droits de l'Homme · Relations et conditions de travail |
| `Environmental` | Environnement |
| `Ethics` | Loyauté des pratiques · Questions relatives aux consommateurs |
| `Procurement` | Communautés et développement local · volet achats de la loyauté des pratiques |

Les sept domaines d'action de la norme s'adressent à toute organisation, y
compris publique et associative, tandis que MAAT s'adresse à une PME dont
l'interlocuteur est un donneur d'ordres. Le découpage retenu épouse la
structure des questionnaires qu'elle reçoit — Environnement, Social,
Gouvernance, Éthique, Achats — plutôt que celle de la norme.

`Procurement` est le regroupement le plus discutable et le plus
différenciant : les achats responsables ne sont pas un domaine d'action de
l'ISO 26000, ils traversent plusieurs d'entre eux. Les isoler se justifie par
le déclencheur d'achat du produit — une PME qui reçoit un questionnaire RSE le
reçoit *parce qu'elle est fournisseur*, et sa propre chaîne
d'approvisionnement est le premier sujet sur lequel son client l'interroge.
Assumé explicitement comme un écart revendiqué vis-à-vis de la norme, pas
comme une omission.

### Six règles de frontière entre domaines MAAT

Une fois les cinq domaines posés, certains sujets restent ambigus entre deux
d'entre eux. Six règles tranchent les cas rencontrés en rédigeant le
référentiel :

1. **`Environmental` / `Procurement`.** `Environmental` couvre les
   interactions physiques entre l'entreprise et son milieu, dans les deux sens
   — ce qu'elle consomme, émet, rejette, et ce qu'elle subit. `Procurement`
   couvre les décisions portant sur un tiers : choisir, évaluer,
   contractualiser.
2. **`Governance` / domaines opérationnels.** `Governance` couvre qui décide
   et selon quel processus. La transmission des consignes opérationnelles
   reste dans le domaine dont elles relèvent.
3. **`Governance` / `Social`.** `Governance` couvre la composition de l'organe
   qui décide. `Social` couvre le traitement de l'ensemble des salariés. Une
   question sur l'égalité de traitement, la rémunération ou le recrutement
   relève de `Social`, y compris lorsqu'elle porte sur la mixité.
4. **`Ethics` / `Procurement`.** `Ethics` couvre la conduite de l'entreprise
   vis-à-vis de son marché et de ses clients. Le domaine d'action ISO 6.6.6 —
   promotion de la responsabilité sociétale dans la chaîne de valeur — relève
   de `Procurement`.
5. **`Procurement` / `Social`.** `Social` couvre les personnes que
   l'entreprise emploie. `Procurement` couvre celles qu'emploient ses
   fournisseurs et prestataires.
6. **`Ethics` / `Social` sur les signalements.** `Ethics` couvre le
   signalement d'un manquement à la conduite des affaires — fraude, conflit
   d'intérêts. `Social` couvre le recours ouvert à un salarié qui subit une
   atteinte personnelle. Deux dispositifs distincts, deux questions
   distinctes.

### Point ouvert, non tranché par cette ADR

`docs/specs/referentiel-amendements.md` signale que la portée exacte de `B2`
(VSME) — utilisé pour justifier la restriction du domaine `Ethics` à un
`vsme_ref` presque toujours vide (§5 de `referentiel.md`) — dépend d'une
lecture du guide EFRAG sur l'élément `C2` que cette ADR ne fait pas : si `C2`
couvre les pratiques et politiques sur l'ensemble des thèmes de durabilité,
conduite des affaires comprise, et que `B2` suit la même logique dans le
module de base, la restriction est à revoir. Cette vérification relève de la
rédaction de contenu (`referentiel.md`, préambule : « ne se délègue pas »), pas
d'une décision d'architecture — elle n'est donc pas traitée ici.

## Conséquences

- Toute nouvelle question du référentiel s'arbitre contre les six règles
  ci-dessus avant d'être rattachée à un domaine ; un désaccord sur une règle
  se documente en amendant cette ADR, pas en dérogeant silencieusement.
- `docs/specs/referentiel.md`, section 2, renvoie ici plutôt que de dupliquer
  ce contenu.
- Le point ouvert sur `B2`/`C2` reste à vérifier avant la rédaction finale du
  domaine `Ethics` ; si la lecture confirme que `B2` couvre la conduite des
  affaires, `referentiel.md` §5 et les `vsme_ref` déjà posés sur `Ethics`
  doivent être révisés dans la foulée.
