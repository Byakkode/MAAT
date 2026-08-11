# 0006 — Licence QuestPDF

## Statut

Acceptée — 2026-08-10

## Contexte

`docs/specs/rapport-pdf.md`, section 1, impose de trancher la question de la
licence QuestPDF avant d'écrire une ligne de code de génération de PDF, et de
relever le seuil de la *Community License* à la date de rédaction plutôt que
de le recopier d'une source qui aura pu changer.

Seuil relevé le 2026-08-10 sur `questpdf.com/pricing` (FAQ « Choosing Your
License ») :

- **Community License, gratuite** : organisations dont le chiffre d'affaires
  annuel brut est inférieur à 1 000 000 USD, sans limite de temps, sans clé de
  licence ni filigrane. Gratuite aussi, indépendamment du chiffre d'affaires,
  pour les associations caritatives, les établissements académiques publics
  ou à but non lucratif qualifiants, et les projets open source distribués
  sous une licence approuvée OSI.
- **Deux catégories toujours exclues de la Community License, quel que soit
  le chiffre d'affaires** : les organismes publics/gouvernementaux (sauf
  établissements académiques publics qualifiants), et les sociétés cotées en
  bourse.
- **Si le chiffre d'affaires dépasse le seuil** : période de transition de 90
  jours à partir de la fin de l'exercice fiscal au cours duquel le seuil a été
  franchi, sans effet rétroactif ni pénalité pour la période où l'éligibilité
  était acquise.
- **Licence payante, si le seuil est dépassé** : *Professional*, 1 999 USD +
  taxes locales, licence perpétuelle par entité juridique, développeurs
  illimités, un an de mises à jour et de correctifs inclus, renouvelable
  annuellement au prix verrouillé du premier achat (« Price Lock »). *Enterprise*,
  4 999 USD + taxes locales, mêmes conditions étendues à un groupe de sociétés
  affiliées.

## Décision

### MVP et soutenance : Community License

MAAT n'a aucun chiffre d'affaires à ce stade — la Community License
s'applique sans ambiguïté. `QuestPdfBootstrapper.Configure()`
(`MAAT.Infrastructure/Pdf/QuestPdfBootstrapper.cs`) appelle explicitement
`QuestPDF.Settings.License = LicenseType.Community;`, et `Program.cs` invoque
cette méthode au démarrage, aux côtés des autres garde-fous de démarrage
(`ICompromisedPasswordChecker`, la clé de signature JWT). Sans cet appel
explicite, QuestPDF lève une exception au premier document généré — un échec
au runtime, pas à la compilation, qui ne se manifesterait qu'au premier
téléchargement réel si on l'omettait.

### Trajectoire commerciale : ligne à inscrire dans le plan financier

Le plan financier de MAAT projette un ARR dépassant 1 000 000 USD en année 3.
Deux conséquences à assumer, pas à découvrir en soutenance :

- **La transition n'est pas un couperet.** Le franchissement du seuil ouvre
  une période de 90 jours à partir de la fin de l'exercice fiscal concerné,
  et rien n'est dû rétroactivement pour la période de gratuité — le
  changement de statut peut donc être planifié, pas subi.
- **Le coût doit figurer dans les charges du plan de financement à partir de
  l'exercice où le seuil est dépassé.** Au tarif relevé ci-dessus, une
  licence *Professional* (1 999 USD/an, couvrant MAAT en tant qu'entité
  juridique unique, développeurs illimités) suffit tant que MAAT reste une
  seule société ; une *Enterprise* (4 999 USD/an) ne serait nécessaire qu'en
  cas de structuration en plusieurs entités affiliées, non prévue à ce stade.
  Ce montant est négligeable devant l'ARR qui déclenche son besoin (le seuil
  de passage lui-même est 500x ce coût annuel), mais doit néanmoins apparaître
  explicitement — c'est une ligne du plan de financement qu'un jury peut
  demander, pas un détail d'ingénierie invisible.

### Alternative non retenue, et pourquoi

Une bibliothèque de génération PDF alternative sous licence MIT/Apache (par
exemple PdfSharp ou migradoc) aurait évité la question, mais QuestPDF est
retenu (stack figée, voir `CLAUDE.md`) pour son API de mise en page
déclarative (idiomatique en C#, sans dépendance à un moteur de rendu HTML) et
sa combinaison native avec SkiaSharp pour le radar — remettre ce choix en
cause pour une contrainte de coût qui ne se matérialise qu'à un stade de
croissance avancé, et qui reste d'un ordre de grandeur négligeable à ce
stade, n'est pas justifié.

## Conséquences

- Aucun coût de licence à ce jour ; aucune action requise avant que MAAT
  dépasse 1 000 000 USD de chiffre d'affaires annuel brut.
- Point de vigilance à réintroduire dans la revue annuelle du plan
  financier : dès que l'ARR projeté approche le seuil, budgéter la licence
  *Professional* (1 999 USD/an) à partir de l'exercice de dépassement, et
  déclencher son achat dans la fenêtre de transition de 90 jours qui suit la
  fin de cet exercice.
- Si MAAT devient un jour un organisme du secteur public ou une société
  cotée, la Community License cesse de s'appliquer indépendamment du chiffre
  d'affaires — cas hors trajectoire actuelle, non budgété ici.
