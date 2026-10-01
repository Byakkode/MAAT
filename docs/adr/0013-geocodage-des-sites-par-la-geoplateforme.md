# 0013 — Géocodage des sites par la Géoplateforme de l'IGN

## Statut

Acceptée — 2026-09-30

## Contexte

La norme volontaire européenne (règlement délégué (UE) 2026/1560, information B1, §27 e
vii) demande la **géolocalisation** des sites que l'entreprise détient, loue ou gère
(`docs/specs/norme-volontaire.md`). Une adresse postale ne suffit pas au sens strict ;
des coordonnées, si. Il faut décider comment MAAT les obtient.

Contrainte du projet : l'hébergement et les traitements restent en France (CLAUDE.md,
hébergement), et aucun secret supplémentaire ne doit être nécessaire pour faire tourner
l'application en local.

## Options étudiées

**Coordonnées saisies à la main.** Aucune dépendance. Écarté : presque aucun dirigeant
de PME ne connaît la latitude de son entrepôt, la donnée resterait vide et B1 ne serait
jamais complète.

**Adresse seule.** Le plus simple. Écarté : moins fidèle au texte, et un lecteur (banque,
donneur d'ordres) qui veut croiser les sites avec une carte des zones Natura 2000 (B5) ou
de stress hydrique (B6) devrait refaire le géocodage lui-même.

**Géocodeur commercial (Google, Mapbox…).** Écarté : clé d'API, facturation, et envoi de
données à un prestataire hors UE.

**Service de géocodage de la Géoplateforme (IGN).** Service public, gratuit, sans clé,
hébergé en France, adossé à la Base Adresse Nationale. L'ancienne API Adresse
(`api-adresse.data.gouv.fr`) lui a été transférée et a été arrêtée fin janvier 2026 ;
l'adresse en vigueur est `https://data.geopf.fr/geocodage/search`.

## Décision

La Géoplateforme, appelée **côté serveur** à la création d'un site et à chaque
changement de son adresse. Le serveur enregistre les coordonnées du premier résultat et
son libellé normalisé.

- **L'enregistrement ne dépend jamais du service externe.** Adresse introuvable, délai
  dépassé ou service indisponible : le site est enregistré sans coordonnées, la réponse
  l'indique, et l'écran propose de corriger l'adresse. B1 reste alors « à compléter »,
  ce qui est exact.
- **Seule l'adresse du site est transmise**, jamais le nom de l'entreprise ni une donnée
  personnelle.
- **Pas d'appel depuis le navigateur.** L'appel serveur évite une origine de plus dans la
  politique de sécurité du frontend et permet de le remplacer par un double en test
  (`IGeocoder`), sans réseau.
- **Pas d'appel à la génération du PDF.** Les coordonnées sont lues en base : le
  générateur reste pur (`rapport-pdf.md`, section 3).

## Conséquences

- Une dépendance réseau de plus en production, sans secret. Délai d'attente court
  (5 secondes) pour qu'une panne du service ne bloque pas l'écran.
- Adresses hors de France : la Géoplateforme ne les couvre pas. Le site est enregistré
  sans coordonnées. Acceptable pour la cible (PME françaises) ; à revoir si l'offre
  s'ouvre à des groupes implantés à l'étranger.
- Si l'IGN déplace encore son service, seule l'URL de configuration
  (`Geocoding:BaseUrl`) change.
