# 0007 — Podman ou Docker en production

## Statut

Acceptée — 2026-08-12

## Contexte

`docs/specs/deploiement.md`, section 2, impose de trancher explicitement entre Podman
et Docker sur le VPS de production plutôt que de reconduire par habitude le choix du
poste de développement, et note que les fichiers `compose` diffèrent peu entre les
deux : ce qui change est le mode d'exécution, pas la description des services
(`docker-compose.prod.yml` est écrit pour fonctionner à l'identique avec
`docker compose` ou `podman compose`).

Le poste de développement est en Podman rootless pour des raisons propres à ce
poste (CLAUDE.md) : mappage d'UID en rootless incompatible avec les bind mounts,
point de montage `postgres:18` observé empiriquement. Aucune de ces deux raisons ne
s'applique à un serveur dédié — la question doit donc être reposée pour la
production, pas héritée du poste de développement.

## Décision

### Podman rootless en production, sous l'utilisateur de service dédié

`docs/specs/deploiement.md`, section 5, impose un utilisateur de service dédié à
l'application, **sans droits d'administration**. C'est l'argument décisif :

- **Docker** : le démon `dockerd` tourne en root, et tout accès à son socket (donc
  toute appartenance au groupe `docker`) équivaut à un accès root sur l'hôte — c'est
  documenté par Docker lui-même comme une frontière de confiance, pas une négligence
  de configuration. Un « utilisateur de service sans droits d'administration » qui
  peut néanmoins lancer des conteneurs Docker est donc une contradiction : soit il
  est dans le groupe `docker` et dispose de facto d'un accès root, soit il ne peut
  rien déployer. Docker propose un mode rootless, mais moins mature et moins
  documenté que celui de Podman — y recourir abandonnerait l'argument principal en
  faveur de Docker (l'écosystème éprouvé) sans en retirer le bénéfice de sécurité
  correspondant.
- **Podman** : pas de démon privilégié. `podman` lancé par l'utilisateur de service
  crée des conteneurs comme des processus enfants de cet utilisateur, avec les
  espaces de noms UID/GID remappés vers une plage sous-ordonnée
  (`/etc/subuid`, `/etc/subgid`) — aucun chemin vers root de l'hôte via l'outillage
  de conteneurs. La contrainte de la section 5 devient alors réellement vraie, pas
  seulement nominale.

### Port privilégiés (80/443) en rootless : point vérifié, pas supposé

Le proxy (`proxy`, image Caddy) publie les ports 80 et 443, réservés au superutilisateur
sur un Linux classique. En rootless, la publication de ports `compose` fonctionne
malgré tout : le remappage de port est effectué par l'outil de réseau en espace
utilisateur (`slirp4netns` ou `pasta`, selon la distribution retenue) en dehors de
l'espace de noms du conteneur, donc sans que le processus du conteneur lui-même ait
besoin d'un privilège pour se lier au port. C'est un point à vérifier concrètement une
fois le VPS commandé et sa distribution connue (`docs/deploiement/checklist.md`), pas
un détail à supposer acquis — cohérent avec la remarque de la spec sur ce document :
« l'échec ne se voit pas en test ».

### Intégration système : à privilégier, pas obligatoire dès le MVP

Podman propose une génération de service systemd par conteneur/pod
(`podman generate systemd`, ou Quadlet sur les distributions qui l'embarquent) sans
dépendre d'un démon tiers pour le redémarrage au boot — Docker offre un résultat
équivalent (`docker.service` + `restart: unless-stopped`) via son propre démon
systemd. Ce n'est donc pas un argument distinctif en soi, mais un point
d'implémentation plus direct sous Podman (pas de démon intermédiaire à superviser),
à câbler lors du premier déploiement plutôt qu'à l'ouverture de cette ADR.

### Alternative non retenue, et pourquoi

**Docker Engine** aurait l'avantage d'une documentation et d'exemples plus abondants,
et `docker compose` reste la référence dont `podman compose` cherche la compatibilité
(risque résiduel : une option de syntaxe compose non encore supportée par le
fournisseur `podman compose`, à surveiller au premier `up` réel plutôt qu'à
présumer). Cet avantage ne compense pas la contradiction avec la section 5 de la
spec : faire tourner Docker en production reviendrait soit à donner de facto un accès
root à l'utilisateur de service (groupe `docker`), soit à basculer sur son mode
rootless, moins rodé, ce qui revient à payer le désavantage de Podman (rootless)
sans bénéficier de sa maturité sur ce point précis.

## Conséquences

- Installer Podman (pas Docker) lors de la préparation du VPS
  (`docs/deploiement/checklist.md`), sous l'utilisateur de service dédié.
- `loginctl enable-linger <utilisateur-service>` nécessaire pour que les conteneurs
  rootless survivent en dehors d'une session interactive et redémarrent au boot —
  sans quoi ils s'arrêtent à la déconnexion SSH.
- `COMPOSE_CMD=podman compose` est la valeur par défaut de `.env.production.example`
  et de `deploy/deploy.sh` ; peut être basculé sur `docker compose` sans modifier
  `docker-compose.prod.yml` si cette décision devait être révisée.
- Vérifier concrètement sur le VPS, une fois commandé : publication effective des
  ports 80/443 en rootless, et lingering actif après un redémarrage complet de la
  machine (pas seulement une déconnexion SSH).
