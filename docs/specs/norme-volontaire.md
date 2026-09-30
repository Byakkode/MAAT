# Spécification — Rapport selon la norme volontaire (ex-VSME)

Périmètre : les données que l'entreprise saisit pour établir son rapport de durabilité
selon le **module de base** de la norme volontaire européenne, et la section 04 du
rapport PDF qui les présente, information par information, de B1 à B11.

Offre : Essential et au-dessus (`abonnement.md`, section 8). Le rapport Starter se réduit
à sa page de garde et n'a pas de section 04.

Dépendances : `rapport-pdf.md` (section 4, bloc 04), `modele-donnees.md` (entités
`RseIndicators`, `VsmeStatement`, `CompanySite`), `abonnement.md` (section 8), ADR 0013
(géocodage des sites).

---

## 1. Texte de référence

Règlement délégué (UE) 2026/1560 de la Commission du 3 juillet 2026, publié au JOUE le
21 septembre 2026, en vigueur le 24 septembre 2026. Il reprend la VSME de l'EFRAG
(décembre 2024) comme norme d'information en matière de durabilité **à usage volontaire**,
et c'est le plafond de la chaîne de valeur : un grand client soumis à la CSRD ne peut pas
exiger d'une entreprise de 1 000 salariés au plus davantage que cette norme (article 3,
exercices ouverts à partir du 1ᵉʳ janvier 2027).

Texte lu pour cette spécification : annexe I de l'acte C(2026) 5011, telle qu'adoptée par
la Commission (ec.europa.eu/finance/docs/level-2-measures/csrd-delegated-act-2026-5011-annex_en.pdf).
Les numéros de paragraphe cités ci-dessous (§) sont ceux de cette annexe.

**Libellé.** Partout dans le produit, « norme volontaire (ex-VSME) » à la première
occurrence d'un écran ou du document, « norme volontaire » ensuite. Le sigle VSME reste
cité parce que c'est le nom que les clients et les banques connaissent, mais il désigne
désormais le texte de l'EFRAG, pas la norme en vigueur.

---

## 2. Ce que le module de base demande, et d'où vient chaque donnée

Légende de la colonne « Source » : **I** = `RseIndicators` (par exercice, écran
Indicateurs), **S** = `VsmeStatement` (par exercice, même écran), **E** = entreprise
(`Company`, `CompanySite`), **calc** = calculé à la génération.

Une ligne marquée ⓥ est facultative pour une entreprise de 10 salariés au plus
(texte entre crochets de la norme, §8).

| Information | Donnée (§) | Source |
| --- | --- | --- |
| **B1** Base d'établissement | Option retenue et déclaration de conformité (§27 a) | calc (toujours option A : module de base seul) |
| | Informations omises au titre du §22 (§27 b) | S `omitted_disclosures` |
| | Base individuelle ou consolidée (§27 c) ; filiales et adresse du siège si consolidée (§27 d) | S `reporting_basis`, `subsidiaries` |
| | Forme juridique (§27 e i) | S `legal_form` |
| | Code NACE (§27 e ii) | calc depuis `Company.sector_code` (les quatre premiers caractères du NAF rév. 2 : `6201Z` → `62.01`) |
| | Total du bilan (§27 e iii) | S `total_assets_eur` |
| | Chiffre d'affaires (§27 e iv) | I `revenue_eur` |
| | Effectif (§27 e v) | calc : somme CDI + CDD de B8 si renseignée, sinon I `employee_count_fte` |
| | Pays principal d'activité et localisation des actifs importants (§27 e vi) | S `primary_country` ; les sites |
| | Géolocalisation des sites détenus, loués ou gérés (§27 e vii) | E `CompanySite` (adresse, coordonnées) |
| | Certifications et labels de durabilité : émetteur, date, note (§28) | S `certifications` |
| **B2** Pratiques, politiques, initiatives | A-t-on des pratiques (§29 a), des politiques et sont-elles publiques (§29 b), des initiatives futures (§29 c), des objectifs (§29 d) ; thèmes couverts parmi l'annexe B (§30) | S `b2_*` |
| **B3** Énergie et GES ⓥ | Consommation totale d'énergie en MWh (§32) | I `energy_consumption_kwh` (÷ 1 000) |
| | Ventilation électricité / combustibles × renouvelable / non renouvelable, si disponible (§32) | I `energy_*_mwh` |
| | Émissions brutes de GES Scope 1 et Scope 2 (méthode fondée sur la localisation), en tCO₂eq (§33) | I `scope1_tco2e`, `scope2_location_tco2e` |
| **B4** Pollution | Polluants rejetés dans l'air, l'eau et le sol que l'entreprise doit déjà déclarer (loi, système de management environnemental), ou lien vers le document public (§34) | S `b4_applicable`, `pollutants`, `pollution_report_url` |
| **B5** Biodiversité | Sites situés dans ou près d'une zone sensible pour la biodiversité, et nom de la zone (§35) | E `CompanySite.in_or_near_sensitive_area`, `sensitive_area_name` |
| **B6** Eau ⓥ | Prélèvement total (§36) | I `water_withdrawal_m3` |
| | Si procédés fortement consommateurs d'eau : consommation, et part consommée en zone de stress hydrique (§37) | I `water_consumption_m3`, `water_consumption_stress_m3` |
| **B7** Ressources, économie circulaire, déchets ⓥ | Applique-t-on des principes d'économie circulaire, et comment (§38) | S `circular_economy_applied`, `circular_economy_description` |
| | Déchets produits, dangereux et non dangereux (§39 a) | I `hazardous_waste_tons`, `non_hazardous_waste_tons` |
| | Part orientée vers le recyclage ou la réutilisation (§39 b) | I `recycling_rate_pct` |
| | Si secteur à flux de matières importants : flux annuel des matières (§39 c) | S `material_flows_description` |
| **B8** Effectifs | Effectif par type de contrat (§40 a), par sexe (§40 b), par pays si plusieurs pays (§40 c), en effectif ou en ETP | I `permanent_employees`, `temporary_employees`, `female_employees`, `male_employees`, `other_gender_employees` ; S `employee_count_unit`, `employees_by_country` |
| **B9** Santé et sécurité | Nombre et taux d'accidents du travail enregistrables (§41 a) | I `recordable_accidents`, `hours_worked` ; taux calc |
| | Nombre de décès (§41 b) | I `work_fatalities` |
| **B10** Rémunération, négociation, formation | Salaire au moins égal au minimum applicable (§42 a) | S `minimum_wage_met` |
| | Écart de rémunération femmes-hommes, **seulement si la loi l'impose déjà** (§42 b) | I `gender_pay_gap_pct` |
| | Part des salariés couverts par une convention collective (§42 c) | I `collective_bargaining_pct` |
| | Heures de formation annuelles moyennes par salarié (§42 d) | I `training_hours_per_employee` |
| **B11** Corruption | Nombre de condamnations et montant des amendes de l'exercice (§43) | S `corruption_convictions`, `corruption_fines_eur` |

**Taux d'accidents.** La norme demande « le nombre et le taux » sans fixer la base dans
son annexe. MAAT suit le guide de l'EFRAG : nombre d'accidents enregistrables ÷ heures
travaillées × 200 000, et **affiche la base** à côté du chiffre (« pour 200 000 heures
travaillées »). Le taux de fréquence français (base 1 000 000) reste saisissable à part,
dans les compléments : les deux chiffres ne se comparent pas et ne doivent jamais porter
le même nom. Est enregistrable un accident qui entraîne un décès ou plus de trois jours
d'absence (annexe A de la norme).

### Règles de cohérence (Domain, `RseIndicators`)

Une valeur calculée n'est jamais saisie deux fois différemment :

- les quatre cellules de la ventilation énergétique renseignées → `energy_consumption_kwh`
  devient leur somme × 1 000 ;
- Scope 1 et Scope 2 renseignés → `co2_emissions_tons` devient leur somme ;
- déchets dangereux et non dangereux renseignés → `waste_tons` devient leur somme.

Le tableau de bord continue de lire les champs historiques, qui restent donc justes.

---

## 3. Complétude et déclaration de conformité

§27 a : « a module shall be complied with in its entirety ». Le rapport n'écrit donc la
déclaration de conformité **que si** toutes les données essentielles sont renseignées.
Sinon il l'annonce comme partiel et nomme ce qui manque — jamais l'inverse : une
déclaration de conformité sur un rapport incomplet est précisément ce qu'un banquier ou un
acheteur relèvera.

`VsmeCompleteness` (Domain, service pur) rend, pour chaque information B1 à B11, l'un de
trois états : **Complète**, **À compléter** (liste des données manquantes), **Omise**
(§22, déclarée comme telle dans B1). Règles :

- une donnée ⓥ n'est jamais manquante pour une entreprise de 10 salariés au plus ;
  l'effectif retenu est celui de B1, et à défaut la tranche `Micro` de `Company.size_range` ;
- une donnée « si applicable » (§15) n'est jamais manquante : B4 si `b4_applicable` n'est
  pas vrai, consommation d'eau (§37), flux de matières (§39 c), écart de rémunération
  (§42 b), répartition par pays (§40 c), B11 sans condamnation ;
- B1 exige au moins un site, chacun localisé (coordonnées obtenues) ;
- B2 exige une réponse (oui ou non) aux quatre questions ; B5 exige, pour chaque site, la
  réponse à « zone sensible ? » ;
- une information omise au titre du §22 compte comme traitée.

Rapport **conforme** : toutes les informations sont Complètes ou Omises. L'écran affiche
la même complétude que le PDF (« 8 informations sur 11 complètes »), calculée par le même
service et renvoyée par l'API : les deux ne peuvent pas diverger.

---

## 4. API

Toutes les routes exigent un utilisateur authentifié ; la lecture est ouverte aux trois
rôles, l'écriture réservée à `Admin` et `User` (un `Viewer` reçoit `403`), et à l'offre
Essential au moins (`canEditIndicators`, réponse `403 plan_required`).

| Méthode | Route | Rôle |
| --- | --- | --- |
| `GET` / `PUT` | `/api/indicators/{year}` | Indicateurs de l'exercice (existant, champs ajoutés) |
| `GET` / `PUT` | `/api/vsme/{year}` | Déclarations de l'exercice (`VsmeStatement`) |
| `GET` | `/api/vsme/{year}/completeness` | Complétude B1 à B11 (section 3) |
| `GET` / `POST` | `/api/company/sites` | Sites de l'entreprise |
| `PUT` / `DELETE` | `/api/company/sites/{id}` | Modifier, supprimer un site |

`GET /api/vsme/{year}` sans ligne pour l'exercice répond `204`, comme les indicateurs.
Cinquante sites au plus par entreprise (`422` au-delà).

**Géocodage (ADR 0013).** À la création et à chaque changement d'adresse, le serveur
interroge le service de géocodage de la Géoplateforme de l'IGN
(`https://data.geopf.fr/geocodage/search`, sans clé) et enregistre les coordonnées du
premier résultat, avec son libellé normalisé. Adresse introuvable ou service
indisponible : le site est enregistré **sans** coordonnées, et la réponse le dit
(`geocoded: false`) ; l'écran propose de corriger l'adresse. L'enregistrement ne dépend
jamais de la disponibilité du service externe. Seule l'adresse du site est transmise :
jamais le nom de l'entreprise ni une donnée personnelle.

---

## 5. Écran Indicateurs

L'écran garde son nom et sa route. Il s'organise en deux parties, pour l'année choisie :

1. **Informations de durabilité (norme volontaire)** : une carte par information, de B1 à
   B11, dans l'ordre de la norme, chacune avec sa pastille de complétude. B1 porte la
   liste des sites. Les données ⓥ sont signalées « facultatif jusqu'à 10 salariés ».
2. **Compléments** : les indicateurs de MAAT que la norme ne demande pas (achats
   responsables, investissements RSE, part export, taux de rotation, index Egapro, taux de
   fréquence français, part de CDI). §13 autorise ces compléments ; ils figurent en fin de
   section 04 du rapport.

Un bandeau en tête reprend la complétude (section 3) et nomme les informations à
compléter avant de pouvoir déclarer le rapport conforme.

---

## 6. Section 04 du rapport PDF

Titre « Informations de durabilité », sous-titre « Module de base de la norme volontaire
(ex-VSME), exercice {année} ». L'exercice est l'année de référence de `rapport-pdf.md`
(la plus récente qui ne dépasse pas l'année de complétion du diagnostic).

1. **Encart de conformité.** Rapport conforme : « Ce rapport de durabilité est établi
   selon le module de base (option A) de la norme volontaire européenne, règlement
   délégué (UE) 2026/1560. » Sinon : « Rapport partiel : {n} informations sur 11 restent à
   compléter avant de pouvoir déclarer la conformité au module de base », suivi de leur
   liste.
2. **B1 à B11**, un bloc par information, titre et numéro de la norme. Chaque donnée
   s'affiche avec sa valeur, sa unité, et la valeur de l'exercice précédent quand elle
   existe (§14 : information comparative dès la deuxième année). Une donnée absente
   s'affiche selon sa nature, jamais en blanc :
   - « Non renseigné » : donnée essentielle manquante ;
   - « Facultatif (10 salariés ou moins) » : donnée ⓥ non saisie par une micro-entreprise ;
   - « Non applicable » : donnée « si applicable » dont la condition n'est pas remplie ;
   - « Information omise (§22) » : information déclarée comme omise dans B1.
3. **Compléments** : les indicateurs hors norme renseignés, avec leur tendance, comme
   l'ancien bloc « Indicateurs RSE ».

Sans aucune donnée pour aucun exercice, l'encart d'invitation à la saisie remplace la
section, comme auparavant.

Les données B1 à B11 sont présentées telles qu'enregistrées à la date de génération :
elles rejoignent le suivi du plan d'actions dans ce que `rapport-pdf.md` (section 3)
appelle l'état vivant du document. À données d'entrée identiques, octets identiques.

---

## Cas de test

1. `VsmeCompleteness` : entreprise de 25 salariés, tout renseigné → conforme.
2. Même entreprise, Scope 2 manquant → B3 À compléter, rapport partiel, donnée nommée.
3. Entreprise de 8 salariés sans énergie, GES, eau ni déchets → B3, B6, B7 Complètes.
4. `b4_applicable` faux ou absent → B4 Complète ; vrai sans polluant ni lien → À compléter.
5. B3 déclarée omise (§22) → Omise, et le rapport peut être conforme.
6. Aucun site, ou un site sans coordonnées → B1 À compléter.
7. Un site sans réponse sur la zone sensible → B5 À compléter.
8. Ventilation énergétique complète → `energy_consumption_kwh` = somme × 1 000 ; Scope 1 +
   Scope 2 → `co2_emissions_tons` ; dangereux + non dangereux → `waste_tons`.
9. Taux d'accidents = accidents ÷ heures × 200 000 ; sans heures, taux absent, nombre affiché.
10. Code NACE dérivé du NAF : `6201Z` → `62.01`, `0111Z` → `01.11`.
11. `PUT /api/vsme/{year}` : Starter → `403 plan_required` ; Essential → `200` ;
    `Viewer` → `403`.
12. `PUT /api/indicators/{year}` en Essential → `200` (était `403`) ; `Viewer` → `403`.
13. Création d'un site : géocodeur qui répond → coordonnées enregistrées ; géocodeur en
    erreur ou sans résultat → site enregistré, `geocoded: false`.
14. Cinquante et unième site → `422`.
15. PDF : rapport conforme → phrase de conformité présente ; partiel → phrase de
    conformité absente et informations manquantes listées.
16. PDF : deux générations aux mêmes données → mêmes octets (`rapport-pdf.md`, cas 10).
17. Complétude de bout en bout : une entreprise Essential qui renseigne tout atteint 11/11
    (API, `VsmeApiTests`). Dans le navigateur (E2E), seule l'offre Starter est atteignable
    sans Stripe : l'écran Indicateurs y présente B1 à B11 en lecture seule, la complétude
    lue auprès de la vraie API (5 sur 11 sans aucune saisie pour une micro-entreprise, la
    tranche par défaut à l'inscription : B4 et B11 « si applicable », B3, B6 et B7
    facultatives), et l'offre
    Essential qui ouvre la saisie. La saisie elle-même est couverte à l'écran par
    `IndicatorsPage.test.tsx`.
