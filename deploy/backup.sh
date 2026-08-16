#!/usr/bin/env bash
# Sauvegarde PostgreSQL — docs/specs/deploiement.md, section 7. Chiffrée (GPG,
# asymétrique : aucune phrase de passe stockée sur le VPS) et envoyée hors machine vers
# un stockage objet en Union européenne via rclone (destination configurée une fois
# avec `rclone config`, jamais dans ce dépôt). Appelé par deploy.sh avant toute
# migration, et par une tâche cron quotidienne indépendante (docs/deploiement/checklist.md).
#
# Prérequis hôte : gpg, rclone, et le moteur de conteneurs (docker/podman) déjà
# installés — la restauration n'est PAS automatisée ici (spec : "une sauvegarde jamais
# restaurée n'en est pas une" — testée manuellement une fois, procédure dans le
# checklist, pas un chemin de code à maintenir pour une opération aussi rare).
set -euo pipefail

cd "$(dirname "$0")/.."

if [ ! -f .env.production ]; then
    echo "Fichier .env.production introuvable (voir .env.production.example)." >&2
    exit 1
fi

set -a
# shellcheck disable=SC1091
source .env.production
set +a

: "${BACKUP_RETENTION_DAYS:=30}"
: "${COMPOSE_CMD:=docker compose}"
: "${BACKUP_GPG_RECIPIENT:?BACKUP_GPG_RECIPIENT manquant dans .env.production}"
: "${RCLONE_REMOTE:?RCLONE_REMOTE manquant dans .env.production}"
COMPOSE="$COMPOSE_CMD -f docker-compose.prod.yml --env-file .env.production"

TIMESTAMP=$(date -u +%Y%m%dT%H%M%SZ)
WORKDIR=$(mktemp -d)
trap 'rm -rf "$WORKDIR"' EXIT

DUMP_FILE="$WORKDIR/maat-${TIMESTAMP}.dump"
ENCRYPTED_FILE="${DUMP_FILE}.gpg"

echo "==> Sauvegarde PostgreSQL (format personnalisé, compressé)"
$COMPOSE exec -T db pg_dump -U "${POSTGRES_USER}" -Fc "${POSTGRES_DB}" > "$DUMP_FILE"

echo "==> Chiffrement (destinataire ${BACKUP_GPG_RECIPIENT})"
gpg --batch --yes --recipient "$BACKUP_GPG_RECIPIENT" --trust-model always \
    --encrypt --output "$ENCRYPTED_FILE" "$DUMP_FILE"
shred -u "$DUMP_FILE" 2>/dev/null || rm -f "$DUMP_FILE"

echo "==> Envoi vers ${RCLONE_REMOTE} (hors machine, Union européenne)"
rclone copy "$ENCRYPTED_FILE" "${RCLONE_REMOTE}/"

echo "==> Purge des sauvegardes distantes de plus de ${BACKUP_RETENTION_DAYS} jours"
rclone delete --min-age "${BACKUP_RETENTION_DAYS}d" "${RCLONE_REMOTE}/"

echo "Sauvegarde ${TIMESTAMP} envoyée vers ${RCLONE_REMOTE}."
