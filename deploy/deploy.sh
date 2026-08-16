#!/usr/bin/env bash
# Déploiement — docs/specs/deploiement.md, section 6. Séquence fixe, dans cet ordre :
# sauvegarde, build, migrations, seed, publication du frontend, démarrage de la
# nouvelle version, vérification de santé, bascule du proxy. Chaque étape doit réussir
# avant la suivante (set -e) ; rien n'est automatique au démarrage du conteneur "api"
# (ni migration ni seed — voir Program.cs) : c'est ce script qui l'orchestre.
#
# Prérequis : lancé depuis le service utilisateur dédié (jamais root), à la racine du
# dépôt cloné sur le VPS, avec .env.production rempli (voir .env.production.example)
# et le domaine déjà pointé (docs/deploiement/checklist.md).
set -euo pipefail

cd "$(dirname "$0")/.."

if [ ! -f .env.production ]; then
    echo "Fichier .env.production introuvable (voir .env.production.example)." >&2
    exit 1
fi

: "${COMPOSE_CMD:=docker compose}"
: "${IMAGE_TAG:=$(git rev-parse --short HEAD)}"
export IMAGE_TAG
COMPOSE="$COMPOSE_CMD -f docker-compose.prod.yml --env-file .env.production --profile tools"

echo "==> [1/8] Démarrage de la base (si arrêtée)"
$COMPOSE up -d db

echo "==> [2/8] Sauvegarde avant migration"
if [ "${SKIP_BACKUP:-0}" = "1" ]; then
    echo "    ignorée (SKIP_BACKUP=1)"
else
    ./deploy/backup.sh
fi

echo "==> [3/8] Construction des images (tag ${IMAGE_TAG})"
$COMPOSE build

echo "==> [4/8] Application des migrations EF Core"
$COMPOSE run --rm migrator

echo "==> [5/8] Chargement des données de référence (idempotent)"
$COMPOSE run --rm api seed

echo "==> [6/8] Construction et publication du frontend statique"
set -a; . ./.env.production; set +a
export VITE_API_URL="https://${API_DOMAIN}"
npm --prefix frontend ci
npm --prefix frontend run build
$COMPOSE run --rm frontend-publish

echo "==> [7/8] Démarrage de la nouvelle version de l'API"
$COMPOSE up -d api

echo "==> [8/8] Vérification de santé puis bascule du proxy"
healthy=0
for _ in $(seq 1 15); do
    if $COMPOSE exec -T api curl -fs http://localhost:8080/api/health >/dev/null 2>&1; then
        healthy=1
        break
    fi
    sleep 2
done

if [ "$healthy" -ne 1 ]; then
    echo "L'API ne répond pas sur /api/health après 30s — déploiement interrompu, proxy non basculé." >&2
    echo "Retour arrière : voir docs/deploiement/checklist.md, section « Retour arrière »." >&2
    exit 1
fi

$COMPOSE up -d proxy
sleep 2
# "caddy reload" plutôt qu'un simple "up -d" : Compose ne recrée un conteneur que si sa
# définition change (image, variables...), pas si le seul contenu d'un fichier
# bind-monté (Caddyfile) a changé — sans ce rechargement explicite, une modification du
# Caddyfile resterait sans effet jusqu'au prochain redémarrage manuel du proxy.
$COMPOSE exec -T proxy caddy reload --config /etc/caddy/Caddyfile

echo "Déploiement terminé (image ${IMAGE_TAG}). Vérifier les cas de test de docs/specs/deploiement.md."
