# Spécification — Génération du rapport PDF

Périmètre : production à la demande du rapport RSE d'un diagnostic complété,
son rendu graphique, sa traçabilité et son téléchargement.

C'est le livrable que l'utilisateur transmet à son donneur d'ordres ou à sa
banque. Tout le reste du produit — questionnaire, score, recommandations —
n'existe que pour aboutir à ce document. C'est aussi le seul artefact du projet
qui sortira de la plateforme et circulera sans nous.

Dépendances : `modele-donnees.md` (entité `Report`), `scoring.md` (détail de
traçabilité exposé par `ScoringService`), `dashboard.md` (libellés qualitatifs,
échelle du radar), `recommandations.md` (plan d'actions priorisé),
`auth-securite-rgpd.md` section 1 (vérification d'adresse), skill `charte-maat`.

Emplacement : `MAAT.Infrastructure/Pdf/`, derrière une interface
`IReportGenerator` déclarée dans `MAAT.Application`. La couche Application ne
connaît ni QuestPDF ni SkiaSharp.

---

## 1. Licence QuestPDF — à trancher avant d'écrire une ligne

QuestPDF est sous double licence depuis fin 2022 : une *Community License*
gratuite en dessous d'un seuil de chiffre d'affaires annuel (de l'ordre du
million de dollars), et une licence payante au-delà. Le seuil doit être vérifié
sur le site de l'éditeur au moment de l'implémentation, pas d'après ce document.

Deux conséquences, et c'est le même raisonnement que pour FluentAssertions.

Pour le MVP et la soutenance, la licence communautaire s'applique sans
difficulté — le chiffre d'affaires est nul. Il faut néanmoins appeler
explicitement `QuestPDF.Settings.License = LicenseType.Community;` au démarrage,
sinon la bibliothèque lève une exception au premier document généré. C'est un
échec au *runtime*, pas à la compilation : il ne se manifestera qu'au premier
téléchargement réel.

Pour la trajectoire commerciale, votre plan financier projette un ARR bien
supérieur au seuil en année 3. Le coût de licence doit donc figurer dans les
charges du modèle, ou l'alternative être documentée. Ce n'est pas un détail
d'ingénierie : c'est une ligne du plan de financement qu'un jury peut demander.

Consigner ce choix dans une ADR, avec le seuil relevé à la date de rédaction.

---

## 2. Endpoint

`GET /api/diagnostics/{id}/report`

Retourne le document en `application/pdf`, avec un en-tête `Content-Disposition`
portant un nom de fichier lisible et déterministe :
`maat-diagnostic-{code-entreprise-normalisé}-{aaaa-mm-jj}.pdf`, la date étant
celle de complétion du diagnostic, jamais celle de la génération.

Un `GET` malgré l'écriture d'une ligne `Report` : le navigateur doit pouvoir
suivre le lien directement, et l'effet de bord est un journal d'audit, pas une
modification de l'état métier. À assumer explicitement plutôt qu'à masquer.

**Préconditions.**

| Situation | Réponse |
| --- | --- |
| Diagnostic `Completed` | 200, le document |
| Diagnostic `InProgress` | 409 — un score partiel n'existe pas |
| Diagnostic `Archived` | 409, même raison |
| Diagnostic d'une autre entreprise | 404, patron établi |
| Adresse e-mail non vérifiée | 403, avec un motif exploitable par le frontend |

La restriction sur l'adresse non vérifiée vient de `auth-securite-rgpd.md`
section 1, et elle est ciblée : le compte peut se connecter, remplir un
diagnostic et consulter son score. Seule la production du document est
bloquée. La raison est que ce document est destiné à circuler auprès de tiers
sous l'identité d'une entreprise — c'est le seul endroit du produit où une
adresse non vérifiée crée un risque d'usurpation.

**Rôles.** Les trois, `Viewer` compris. La génération est une lecture, et le
`Viewer` est précisément le profil auquel on donne accès pour qu'il récupère le
rapport — un expert-comptable, un responsable achats. Lui refuser le document
viderait le rôle de son usage.

**Limitation de débit.** La génération est l'opération la plus coûteuse en CPU
du produit : mise en page, rendu d'une image, embarquement de polices. Sans
limite, un client qui boucle sur le lien sature le VPS. Même mécanisme que pour
les endpoints de confirmation de mot de passe, partitionné par utilisateur
authentifié, avec un quota nettement plus large — l'ordre de grandeur est une
dizaine de générations par quart d'heure, à ajuster après mesure réelle du
temps de génération.

**Journalisation `Report`.** Une ligne par génération réussie :
`diagnostic_id`, `format`, `generated_at`, `generated_by_user_id`. Écrite après
la production du document, pas avant : une génération qui échoue ne doit pas
laisser de trace d'un rapport qui n'a jamais existé. Cette table est un journal
d'audit, elle ne conditionne jamais la génération suivante.

---

## 3. Déterminisme

Le PDF n'est pas stocké (`modele-donnees.md`, entité `Report`). Cette décision
ne tient que si la régénération produit le même document — sinon deux
exemplaires du « même » rapport circulant chez deux destinataires différents ne
diraient pas la même chose, et l'argument s'effondre.

**Règle.** À diagnostic identique, deux générations produisent un contenu
strictement identique. Le seul élément autorisé à varier est la date de
génération affichée en pied de page.

Ce qui l'impose, concrètement :

- aucun `DateTime.Now` dans le générateur — l'horloge est injectée
  (`TimeProvider`), ce qui rend le déterminisme testable en figeant l'heure ;
- aucun aléatoire, aucun identifiant technique généré à la volée dans le
  document ;
- l'ordre des recommandations vient de `priority_rank` persisté, jamais d'un
  recalcul (`recommandations.md` section 2) ;
- les scores viennent de `DomainScore`, avec le `sector_weight` figé, jamais
  d'une relecture de `SectorWeight`.

Les deux derniers points sont les cas 9 et 11 de `recommandations.md` : ils
existaient déjà pour cette raison, c'est ici qu'ils sont encaissés.

---

## 4. Structure du document

Sept blocs, dans cet ordre. Format A4, portrait.

**Page de garde.** Logo MAAT, raison sociale, code NAF, effectif, région, date
de complétion. Score global en grand, accompagné de son libellé qualitatif.

Le libellé humain d'un code NAF (« 6202A — Conseil en systèmes et logiciels
informatiques ») n'a **aucune source dans ce projet** : `sector-weights.csv`
(`modele-donnees.md`) ne porte que `sector_code`, `domain` et `weight`, jamais
de nom, et 2 secteurs seulement sur les 38 visés y sont seedés à ce jour.
L'inventer serait afficher une donnée non vérifiée sur le document qui
circule hors de la plateforme — justement ce que la section 1 interdit pour
la licence QuestPDF, appliqué ici au contenu. À la place, la page de garde
indique si la pondération **effectivement appliquée** est spécifique au
secteur ou retombée sur la pondération par défaut, auquel cas ce repli est
annoncé explicitement plutôt que laissé silencieux. Un vrai référentiel de
libellés NAF est un travail de contenu à part entière, comme les 38 secteurs
pondérés eux-mêmes (`modele-donnees.md`) — à inscrire à la feuille de route.

Cet indicateur vient de `Diagnostic.default_sector_weighting_applied`, décidé
une fois à la complétion (`questionnaire.md`, section 6, cas 13) — jamais
d'une relecture ou d'une déduction depuis `DomainScore.SectorWeight`, même si
celui-ci est lui aussi persisté et jamais recalculé (même raison que pour
`numerator`/`denominator`). Ce n'est pas qu'une question de déterminisme :
`SectorWeight` seul ne suffit pas à distinguer les deux cas. La renormalisation
de `scoring.md` (cas 7) ramène le coefficient effectif à 1.00 quand un seul
domaine est actif, que la pondération d'origine ait été spécifique ou par
défaut — un diagnostic à un seul domaine actif rendait alors les deux
situations indiscernables tant que l'indicateur se basait sur `SectorWeight`.

**Méthodologie.** Une demi-page, pas davantage : les trois référentiels (VSME,
ISO 26000, GRI), la formule du score de domaine, et le principe de la
pondération sectorielle. C'est ce qui distingue le document d'un questionnaire
rempli — un donneur d'ordres doit pouvoir comprendre d'où sort le chiffre.

**Score global et radar.** Le radar des cinq domaines, échelle fixe 0–100, aux
couleurs de la charte. Même règle que le tableau de bord et pour la même raison :
une échelle adaptée aux données rendrait toutes les entreprises graphiquement
semblables.

**Détail par domaine.** Un tableau : domaine, score sur 100, pondération
sectorielle appliquée, contribution au score global. Y faire figurer le
numérateur et le dénominateur du calcul, exposés par `ScoringService`
(`scoring.md`, section Traçabilité). C'est la valeur « Transparence » rendue
vérifiable : le lecteur peut refaire l'opération.

**Points forts et axes d'amélioration.** Les deux domaines les mieux notés, les
deux les moins bien. Formulés comme une trajectoire, jamais comme un jugement —
même vocabulaire que le tableau de bord.

**Plan d'actions.** Les recommandations triées par `priority_rank`, avec
libellé, domaine, niveau d'effort et état d'avancement. Les actions déjà
cochées apparaissent comme telles : c'est ce qui permet à l'entreprise de
montrer qu'elle a agi entre deux diagnostics. Limiter aux vingt premières et
indiquer le total — un PDF de quarante pages n'est pas lu.

**Mentions.** Bloc final obligatoire, et le plus important juridiquement :

> Ce rapport résulte d'une auto-évaluation déclarative réalisée par l'entreprise
> sur la plateforme MAAT. Il ne constitue ni une certification, ni un audit, ni
> une notation par un organisme tiers indépendant.

Cette phrase n'est pas une précaution d'usage. Le document sera transmis à des
donneurs d'ordres comme preuve de maturité RSE ; le présenter sans cette réserve
reviendrait à laisser croire à une validation externe qui n'a pas eu lieu —
c'est la limite 3 que votre propre rapport identifie, et c'est le premier point
qu'un jury attaquera si elle n'apparaît pas dans le livrable lui-même.

Y ajouter la date de génération, le numéro de version du référentiel de
questions, et la mention que le score reflète les réponses au jour de la
complétion.

---

## 5. Rendu graphique

Le radar est produit par SkiaSharp et embarqué en PNG. Pas d'export depuis le
navigateur : le rendu doit être identique quel que soit le client, et c'est déjà
le problème que votre rapport documente.

**Résolution.** Dessiner à environ trois fois la taille d'affichage cible. Un
PNG rendu à la dimension d'écran est visiblement pixellisé à l'impression, et ce
document a vocation à être imprimé.

**Cohérence avec le tableau de bord.** Mêmes couleurs de domaine, même échelle,
même ordre des axes. Deux représentations divergentes du même diagnostic
suffisent à faire douter de l'ensemble. Les couleurs viennent du skill
`charte-maat`, jamais d'une valeur écrite en dur dans le générateur.

**Polices.** Poppins et Inter doivent être **embarquées dans le dépôt** et
enregistrées explicitement auprès de QuestPDF. C'est le piège de déploiement de
ce module : SkiaSharp et QuestPDF s'appuient sur les polices du système, et un
VPS Linux nu n'a ni l'une ni l'autre. Le rapport se génère alors avec une police
de repli — sans erreur, sans avertissement, et le document ne ressemble plus à
la charte. À vérifier sur la cible, pas seulement en local.

---

## 6. Frontend

Bouton de téléchargement sur le tableau de bord et sur la page de résultat d'un
diagnostic.

La génération prend plusieurs secondes : afficher un état de chargement
explicite et désactiver le bouton pendant l'opération, sinon l'utilisateur
clique trois fois et lance trois générations.

Le téléchargement passe par une requête authentifiée, pas par un lien `<a href>`
direct : l'access token vit en mémoire JS et n'accompagne pas une navigation
classique. Récupérer le corps en blob et déclencher la sauvegarde côté client.

Un compte non vérifié voit le bouton, mais désactivé, avec un message expliquant
la démarche et un lien pour renvoyer l'e-mail de vérification. Masquer le bouton
laisserait l'utilisateur croire que la fonctionnalité n'existe pas.

---

## Cas de test

**Endpoint**

1. Diagnostic `Completed`, compte vérifié → 200, `application/pdf`, corps non vide commençant par la signature `%PDF`.
2. Diagnostic `InProgress` → 409, aucune ligne `Report` créée.
3. Diagnostic `Archived` → 409.
4. Diagnostic d'une autre entreprise → 404.
5. Compte non vérifié → 403, aucune ligne `Report` créée.
6. `Viewer` → 200.
7. Génération réussie → une ligne `Report` avec le `generated_by_user_id` du principal authentifié.
8. Échec de la génération → aucune ligne `Report`.
9. Dépassement du quota → 429.

**Déterminisme**

10. Deux générations successives, horloge figée, produisent des octets identiques.
11. Modification de `SectorWeight` après complétion → le document reste identique.
12. Modification de l'ordre des recommandations en base → l'ordre du document suit `priority_rank`, pas un recalcul.

**Contenu**

13. Le score global affiché correspond à `Diagnostic.global_score` arrondi selon la règle d'affichage.
14. Le libellé qualitatif est celui de la table de `dashboard.md` section 2, pour les cinq tranches.
15. Les cinq domaines figurent avec leur score et leur `sector_weight` persisté.
16. La somme des contributions par domaine est cohérente avec le score global.
17. Le bloc de mentions est présent et contient la réserve sur l'auto-évaluation.
18. Diagnostic sans aucune recommandation déclenchée → document valide, plan d'actions remplacé par un message positif.

**Rendu**

19. Le PNG du radar est embarqué à sa résolution de rendu, sans rééchantillonnage
    par le moteur de mise en page. Rapportée à la taille d'affichage effective
    dans le document, cette résolution atteint au moins 300 dpi, seuil usuel de
    lisibilité à l'impression.
20. Les polices Poppins et Inter sont effectivement embarquées dans le document — vérifiable en inspectant les ressources du PDF, et non en constatant qu'il « a l'air correct ».
21. Les cinq libellés de domaine sont posés en texte autour du radar par le
    moteur de mise en page, jamais incrustés dans le PNG — un lecteur doit
    pouvoir identifier chaque axe, et le texte reste sélectionnable.

**Traçabilité**

22. Pour chaque domaine du tableau de détail (section 4), `numérateur ÷
    dénominateur × 100`, arrondi selon la règle d'affichage, égale exactement
    le score affiché sur la même ligne. Un lecteur qui refait le calcul à la
    main avec les deux nombres imprimés doit tomber juste.
23. Entreprise dont le code NAF n'est pas couvert par `SectorWeight`, avec un
    seul domaine actif → la page de garde indique « pondération par défaut »,
    pas « pondération spécifique ». Ce cas existe précisément parce qu'un
    diagnostic à un seul domaine actif est le point où l'ancienne dérivation
    depuis `DomainScore.SectorWeight` se trompait silencieusement (la
    renormalisation de `scoring.md`, cas 7, ramène le coefficient effectif à
    1.00 dans les deux situations) — voir `Diagnostic.
    default_sector_weighting_applied` (`modele-donnees.md`).

Le cas 10 est celui qui justifie de ne rien stocker. Le cas 20 est celui qui vous
évitera de découvrir en production que votre rapport de soutenance sort en
Times New Roman. Le cas 22 est celui qui rend la valeur « Transparence »
vérifiable plutôt que seulement promise : un numérateur et un dénominateur
qui ne redonnent pas le score affiché sont pires que leur absence, puisqu'ils
prétendent à une preuve qui ne tient pas. Le cas 23 est celui qui a failli passer
inaperçu : plausible sur tout diagnostic multi-domaines, faux uniquement sur le
cas limite d'un seul domaine actif.
