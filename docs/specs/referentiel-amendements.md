# `referentiel.md` — amendements, commit unique

**Quatorze modifications**, à passer en un commit unique, maintenant. Les
quatre premiers domaines — `Environmental`, `Governance`, `Ethics`,
`Procurement` — sont rédigés selon ces règles, et `Social` le sera. Le texte
ci-dessous est prêt à coller ; les sections non citées ne changent pas.

**Un amendement dépend d'une lecture non faite.** L'amendement 8 restreint `B2`
aux pratiques de transition vers une économie plus durable, à l'exclusion de la
conduite des affaires. Or le guide EFRAG de l'élément `C2` couvre les pratiques
et politiques sur l'ensemble des thèmes de durabilité, conduite des affaires
comprise. Si `B2` suit la même logique dans le module de base, l'amendement 8 est
à revoir et `Ethics` retrouve des renvois.

**Liste de confrontation au texte final**, dans cet ordre, en une seule passe sur
la colonne `vsme_ref` :

1. **`B2` et la conduite des affaires** — de la réponse dépend la validité de
   l'amendement 8 et huit cases du domaine `Ethics`.
2. **Périmètre de `C6`** — le renvoi est aujourd'hui porté par PRO-04 seule, au
   titre des travailleurs de la chaîne amont. Si le périmètre premier de `C6` est
   le personnel propre, la chaîne de valeur venant en extension, alors SOC-05 et
   SOC-07 — critères de non-discrimination, recours en cas de harcèlement — le
   portent à plus forte raison, et leurs cases vides sont fausses.
3. **Codes du module de base et du module complet** — la VSME est en cours
   d'élargissement en « VS », avec un acte délégué attendu courant 2026. La
   colonne se confronte à ce texte, pas à la version de décembre 2024.

Une seule passe : ces trois points touchent la même colonne, et une confrontation
partielle imposerait un troisième tour dessus.

---

## § 5 — Ancrage normatif

**Ajouter, après la liste de règles existante :**

> **Granularité de `gri_ref`.** Le renvoi désigne une série de normes
> thématiques, sans le préfixe `GRI` — la colonne le porte déjà. Les éléments
> d'information de niveau inférieur sont des indicateurs chiffrés, alors que les
> questions portent sur l'existence de dispositifs : un renvoi à cette
> granularité serait un faux rattachement. Les normes universelles `2` et `3`
> sont admises lorsque le sujet relève de la gouvernance ou de la hiérarchisation
> des enjeux.
>
> **Spécificité de `vsme_ref`.** Le renvoi désigne l'élément thématique le plus
> spécifique qui couvre le sujet. `B2` — pratiques, politiques et initiatives de
> transition vers une économie plus durable — n'est utilisé qu'à défaut d'élément
> thématique, **et seulement pour les sujets qui relèvent de cette transition**.
> Il ne couvre ni la conduite des affaires ni la relation client, qui n'ont pas
> d'élément correspondant dans le module de base : les questions du domaine
> `Ethics` ont donc un `vsme_ref` vide, sauf `B11` pour la corruption.
>
> **Modules `B` et `C`.** `vsme_ref` peut renvoyer au module de base (`B`) ou au
> module complet (`C`). Le préfixe est autodocumenté ; aucune distinction n'est
> portée dans le rapport à ce stade. Un renvoi `C` signale un sujet situé au-delà
> du socle qu'un donneur d'ordres peut exiger au titre du plafond de chaîne de
> valeur — cette lecture est notée en feuille de route, pas implémentée.

---

## § 6 — Règles de rédaction

**Remplacer** « Le `help_text` est obligatoire et vaut deux à trois phrases »
**par :**

> Le `help_text` est obligatoire et compte 60 à 90 mots. Il comprend ce que la
> question recouvre, au moins un exemple concret, et **trois repères sur
> l'échelle de réponse**, au format `0 — … 3 — … 5 — …`.
>
> Les niveaux 1, 2 et 4 ne sont pas décrits : les libellés fixes de l'échelle
> s'en chargent. Le `help_text` dit ce que la question recouvre aux trois états
> structurels — rien, engagé mais incomplet, en place et suivi.
>
> **Le repère 0 décrit une absence, jamais un état partiel.** Un repère 0 qui
> présuppose l'existence de l'objet laisse sans place le répondant qui n'a rien,
> et le pousse vers 1 ou 2 par défaut.
>
> Le repère 3 est rédigé comme **une pratique réelle mais incomplète**, jamais
> comme une demi-mesure floue : action ponctuelle sur un seul poste,
> justificatifs existants mais dispersés, consignes transmises à l'embauche puis
> jamais rappelées. C'est ce qui le rend inconfortable à cocher par réflexe.

**Ajouter :**

> **Neutralité sectorielle.** L'intitulé ne contient aucun terme propre à un
> métier ou à un secteur. Le produit ne dispose d'aucun mécanisme de
> non-applicabilité : une question à laquelle un secteur entier ne peut répondre
> que par zéro produit un score faux, et silencieusement. Le concret appartient
> au `help_text`, où les exemples couvrent au moins deux univers d'activité
> différents. Test : *une boulangerie de 20 salariés peut-elle répondre autrement
> que par zéro ?*
>
> **Variation des formulations.** Deux questions consécutives d'un même domaine
> ne reprennent pas la même tournure d'introduction. Une série de questions quasi
> identiques produit un effet d'acquiescement : le répondant cesse de lire et
> répond en série. **La variation porte sur la tournure, jamais sur le périmètre
> de la question.** Retirer un qualificatif pour éviter une répétition élargit
> l'objet et détruit le pouvoir discriminant de la question.
>
> **Autonomie de l'intitulé.** Chaque intitulé se comprend seul, hors de sa
> séquence. Aucun renvoi implicite à la question précédente — « ces sujets », « ce
> domaine », « cette démarche » — n'est admis si l'antécédent n'est pas dans
> l'intitulé lui-même. La contrainte n'est pas cosmétique : l'intitulé est repris
> dans le plan d'actions, dans le rapport PDF et dans le libellé des
> recommandations qu'il déclenche, où il apparaît sans ses voisines. C'est aussi
> ce qui empêche la règle de variation de produire des renvois implicites.

---

## § 8 — Format du CSV

**Remplacer** la règle de numérotation « sans trou » **par :**

> `code` : préfixe par domaine, numérotation à deux chiffres. La règle de
> stabilité prend effet au **premier `seed` exécuté en production**. Avant ce
> point, les codes peuvent être réattribués librement au sein d'un domaine.
> Après, un `code` ne désigne jamais un autre sujet : une question retirée est
> désactivée, une question nouvelle prend le numéro suivant, **même si cela
> laisse un trou dans la numérotation**.

La spec imposait jusqu'ici une numérotation sans trou *et* la stabilité des
codes. Les deux règles se contredisent dès la première désactivation ; l'arbitrage
va aux trous, parce que la stabilité protège les diagnostics passés et que la
contiguïté ne protège rien.

---

## § 2 — Correspondance des domaines, et ADR

**Ajouter au tableau de correspondance, sous forme de règles de frontière, et
consigner dans l'ADR :**

> **`Environmental` / `Procurement`.** `Environmental` couvre les interactions
> physiques entre l'entreprise et son milieu, dans les deux sens — ce qu'elle
> consomme, émet, rejette, et ce qu'elle subit. `Procurement` couvre les décisions
> portant sur un tiers : choisir, évaluer, contractualiser.
>
> **`Governance` / domaines opérationnels.** `Governance` couvre qui décide et
> selon quel processus. La transmission des consignes opérationnelles reste dans
> le domaine dont elles relèvent.
>
> **`Governance` / `Social`.** `Governance` couvre la composition de l'organe qui
> décide. `Social` couvre le traitement de l'ensemble des salariés. Une question
> sur l'égalité de traitement, la rémunération ou le recrutement relève de
> `Social`, y compris lorsqu'elle porte sur la mixité.
>
> **`Ethics` / `Procurement`.** `Ethics` couvre la conduite de l'entreprise
> vis-à-vis de son marché et de ses clients. Le domaine d'action ISO 6.6.6 —
> promotion de la responsabilité sociétale dans la chaîne de valeur — relève de
> `Procurement`.
>
> **`Procurement` / `Social`.** `Social` couvre les personnes que l'entreprise
> emploie. `Procurement` couvre celles qu'emploient ses fournisseurs et
> prestataires.
>
> **`Ethics` / `Social` sur les signalements.** `Ethics` couvre le signalement
> d'un manquement à la conduite des affaires — fraude, conflit d'intérêts.
> `Social` couvre le recours ouvert à un salarié qui subit une atteinte
> personnelle. Deux dispositifs distincts, deux questions distinctes.

---

## Cas de test — ajout

> 11. Chaque `help_text` contient exactement trois repères, aux valeurs 0, 3 et
>     5, au format `N — `.
> 12. Tous les CSV de seed utilisent des fins de ligne `LF`. Un `.gitattributes`
>     fixant `*.csv text eol=lf` accompagne le contrôle.
>
> L'analyseur `CsvFile` accepte déjà CRLF, LF et CR, y compris mélangés : ce
> contrôle n'a rien à voir avec la compatibilité de lecture. Sa raison est la
> lisibilité des diffs. Un changement de fin de ligne fait apparaître les 45
> lignes comme modifiées et rend illisible la revue du seul contenu que l'équipe
> relit régulièrement.

Le contrôle de forme est automatisable et entre dans `seed --validate`. La
clause « le repère 0 décrit une absence » ne l'est pas : une heuristique sur les
tournures d'absence produit des faux positifs. Elle reste une règle de rédaction
vérifiée en relecture, et le test 11 ne porte que sur la forme.

---

## Récapitulatif

| # | Amendement | Section |
| --- | --- | --- |
| 1 | Longueur du `help_text` : 60 à 90 mots | § 6 |
| 2 | Structure en trois repères 0 / 3 / 5 | § 6 |
| 3 | Le repère 0 décrit une absence | § 6 |
| 4 | Le repère 3 décrit une pratique réelle mais incomplète | § 6 |
| 5 | Neutralité sectorielle et test de la boulangerie | § 6 |
| 6 | Variation des formulations, tournure et non périmètre | § 6 |
| 6 bis | Autonomie de l'intitulé, hors de sa séquence | § 6 |
| 7 | Granularité `gri_ref` | § 5 |
| 8 | Spécificité `vsme_ref`, usage de `B2` | § 5 |
| 9 | Modules `B` / `C`, distinction non implémentée | § 5 |
| 10 | Stabilité des `code`, arbitrage en faveur des trous | § 8 |
| 11 | Six règles de frontière entre domaines | § 2 + ADR |
| 12 | Cas de test 11 | Cas de test |
| 13 | Cas de test 12 : fins de ligne `LF` et `.gitattributes` | Cas de test |

---

## Corrections à passer sur `modele-donnees.md`, même commit

Quatre écarts entre les deux documents, arbitrés.

**Préfixes de code.** La section `Question` donne `ACH-` et `GOU-`. Remplacer par
`PRO-` et `GOV-`, pour que les codes se lisent à côté de `RseDomain.Procurement`
et `RseDomain.Governance` sans traduction, et que le jeu de préfixes ne soit pas
moitié anglais moitié français. **Irréversible après le premier `seed` en
production** : `code` est la clé d'upsert.

**Ancrage ISO de `Ethics`.** Le tableau des cinq domaines donne « ISO 26000
§6.6 ». Remplacer par « §6.6 · §6.7 » : quatre questions du domaine portent sur
la relation client — information précontractuelle, sécurité du produit livré,
réclamations, données personnelles.

**Ancrage ISO de `Procurement`.** Le tableau donne « §6.6.6 ». Remplacer par
« §6.6.6 · §6.8 » : deux questions portent sur l'ancrage local.

**VSME `C8` sur `Procurement`.** Cet élément porte sur les revenus tirés de
certains secteurs et l'exclusion des indices de référence européens. Le retirer,
et le porter au point 4 de la liste de confrontation VSME plutôt que de le
remplacer à l'aveugle.

---

## Amendement à ajouter à `recommandations.md`

**Échelle d'`effort_level`, explicitée.** La colonne est le seul levier éditorial
qui distingue deux recommandations de même poids, tant que les seuils de
déclenchement sont uniformes. Elle ne se note pas à l'encouragement.

> - `Low` — une personne, moins d'une journée cumulée, sans dépense ni décision
>   engageant l'entreprise.
> - `Medium` — plusieurs demi-journées, ou une dépense modérée, ou plusieurs
>   personnes à mobiliser.
> - `High` — change une manière de travailler, engage une dépense significative
>   ou une négociation avec un tiers, s'étale sur plusieurs mois.

Relire cette colonne **seule, sans les textes**, après chaque lot de rédaction.
