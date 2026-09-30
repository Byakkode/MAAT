# 0014 — Détection des zones sensibles pour la biodiversité par l'API Carto de l'IGN

## Statut

Acceptée — 2026-10-01

## Contexte

L'information B5 de la norme volontaire (règlement délégué (UE) 2026/1560, §35) demande les
sites situés **dans ou près** d'une zone sensible pour la biodiversité, et le nom de cette
zone (`docs/specs/norme-volontaire.md`). Aucun dirigeant de PME ne sait spontanément si son
entrepôt jouxte une zone Natura 2000 : laissée à la seule déclaration, B5 serait souvent
fausse ou vide.

La norme définit (annexe A) :

- une zone sensible : les aires protégées (Natura 2000, sites Ramsar, aires protégées
  nationales…) et les zones reconnues pour leur importance écologique (Key Biodiversity
  Areas, Liste rouge de l'UICN…) ;
- « près » : un site qui chevauche ou jouxte une telle zone, avec une zone tampon
  facultative selon l'activité.

MAAT connaît déjà les coordonnées de chaque site (ADR 0013).

## Options étudiées

**Déclaration seule.** Aucune dépendance. Écarté : c'est la situation qui rend B5 peu fiable.

**Base téléchargée et interrogée en local.** Les contours Natura 2000 et ZNIEFF sont publics,
mais pèsent plusieurs centaines de Mo, changent chaque année, et demanderaient une extension
géographique (PostGIS) absente de la stack. Écarté pour un gain nul à l'échelle d'une PME.

**Module Nature de l'API Carto de l'IGN.** Service public, gratuit, sans clé, hébergé en
France, qui expose les données de l'INPN (Muséum national d'Histoire naturelle) : pour une
géométrie donnée, il renvoie les zones qui l'intersectent, couche par couche.

## Décision

Le module Nature de l'API Carto (`https://apicarto.ign.fr/api/nature/`), appelé côté serveur
juste après le géocodage d'un site.

- **Couches retenues** : Natura 2000 directive Habitats et directive Oiseaux, réserves
  naturelles (hors Corse et Corse), parcs nationaux, réserves nationales de chasse et de
  faune sauvage, ZNIEFF de type 1. Les ZNIEFF de type 2 et les parcs naturels régionaux sont
  écartés : ce sont de grands ensembles paysagers ou des projets de territoire, pas des zones
  sensibles au sens de la norme, et une large part de la France rurale s'y trouve.
- **« Près » = cercle de 500 m** autour de l'adresse géocodée (polygone à 16 côtés). On ne
  connaît pas l'emprise du site : ce rayon approche « chevauche ou jouxte » pour une PME. Il
  est configurable (`SensitiveAreas:RadiusMeters`).
- **La réponse de l'utilisateur prime.** La détection ne répond à B5 que pour les sites où il
  ne s'est pas prononcé ; l'écran et le rapport disent alors « détection automatique », et le
  rapport rappelle la méthode.
- **Tout ou rien.** Les couches sont interrogées en parallèle ; si l'une échoue, la recherche
  entière est considérée comme non faite : une réponse partielle ferait croire à l'absence de
  zone là où une couche n'a pas été lue. Le site reste « non vérifié » et la recherche est
  relancée au prochain enregistrement.
- **Jamais à la génération du PDF.** Le résultat est stocké avec le site : le générateur reste
  pur (`rapport-pdf.md`, section 3).

## Conséquences

- Un appel réseau de plus par site enregistré (sept requêtes parallèles, 8 secondes au plus),
  sans secret. Mesuré le 2026-10-01 : de 0,1 à 2,8 secondes selon la couche.
- Le rayon de 500 m est une convention de MAAT, annoncée comme telle dans le rapport. Une
  activité à forte emprise (carrière, industrie) mériterait un rayon plus large : l'utilisateur
  peut alors répondre lui-même.
- Les zones d'importance écologique hors de France (sites de groupes implantés à l'étranger)
  ne sont pas couvertes, comme le géocodage (ADR 0013).
- La détection s'appuie sur des bases publiques qui peuvent évoluer : un site enregistré
  garde le résultat obtenu à son enregistrement, jusqu'à sa prochaine modification.
