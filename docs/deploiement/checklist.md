# Checklist de déploiement — VPS de production

Liste de contrôle exécutable, dans l'ordre. Complète `docs/specs/deploiement.md` (qui
fait autorité sur le *pourquoi*) et `docs/adr/0007-podman-ou-docker-en-production.md`
(qui fait autorité sur le choix Podman). Rien à exécuter par habitude : cocher au fur
et à mesure, sur la machine réelle — ce document est le seul dont l'échec ne se voit
pas en local (docs/specs/deploiement.md, introduction).

Convention : `<...>` = valeur à remplacer. `[distro]` = commande dépendant de la
distribution retenue à la commande du VPS — non choisie à la rédaction de ce document
(CLAUDE.md : « ne rien supposer sur la distribution du VPS tant qu'il n'est pas
commandé »), à compléter une fois connue plutôt qu'à deviner.

## 0. Nommage des domaines — avant tout le reste

- [ ] Domaine enregistrable unique choisi (ex. `maat.fr`), deux sous-domaines prévus :
      `app.<domaine>` (frontend) et `api.<domaine>` (API) — docs/specs/deploiement.md,
      section 1. **Pas** deux domaines distincts (casserait `SameSite=Strict`).
- [ ] Domaine réservé chez un registrar.
- [ ] Propagation DNS vérifiée avant de continuer : `dig +short <domaine>`

## 1. Commande du VPS

- [ ] VPS OVHcloud commandé, datacenter France (Roubaix, Gravelines ou Strasbourg).
- [ ] Dimensionnement mémoire couvrant la génération PDF (SkiaSharp alloue hors du tas
      managé, docs/specs/deploiement.md section 2) — pas le strict minimum de
      l'empreinte .NET seule.
- [ ] Distribution notée ici une fois connue : `______________________`
- [ ] Accès SSH initial reçu.

## 2. Premier accès et durcissement

- [ ] Connexion initiale : `ssh root@<ip-vps>`
- [ ] Mises à jour de sécurité appliquées ([distro] : `apt update && apt upgrade -y`
      sur Debian/Ubuntu, `dnf upgrade -y` sur Fedora/Alma, ou équivalent) puis mises à
      jour automatiques activées ([distro] : `unattended-upgrades` ou
      `dnf-automatic`).
- [ ] Clé SSH personnelle copiée : `ssh-copy-id -i ~/.ssh/<cle>.pub root@<ip-vps>`
- [ ] Utilisateur de service créé, sans droits d'administration
      (docs/specs/deploiement.md section 5) : `adduser --disabled-password maat-svc`
- [ ] Clé SSH de déploiement ajoutée à `maat-svc` (pas à `root`) :
      `mkdir -p ~/.ssh && chmod 700 ~/.ssh` puis clé publique dans
      `~/.ssh/authorized_keys`, `chmod 600`.
- [ ] `PasswordAuthentication no` dans `/etc/ssh/sshd_config`.
- [ ] `PermitRootLogin no` dans `/etc/ssh/sshd_config`.
- [ ] `sshd` rechargé (`systemctl reload sshd`) et connexion `root` par mot de passe
      vérifiée refusée **avant** de fermer la session en cours.
- [ ] Pare-feu configuré, seuls 80, 443 et le port SSH ouverts ([distro] : `ufw` sur
      Debian/Ubuntu, `firewalld` sur Fedora/Alma) : `ufw allow 22/tcp && ufw allow
      80/tcp && ufw allow 443/tcp && ufw enable`
- [ ] Scan externe des ports depuis une autre machine, confirme que rien d'autre ne
      répond : `nmap -Pn <ip-vps>`

## 3. Moteur de conteneurs — Podman (ADR 0007)

- [ ] Podman installé ([distro]) sous `maat-svc`, pas root.
- [ ] Plugin compose disponible : `podman compose version` (ou `podman-compose`
      installé séparément selon la distribution).
- [ ] Lingering activé pour que les conteneurs survivent hors session interactive :
      `loginctl enable-linger maat-svc`
- [ ] Publication effective d'un port privilégié en rootless vérifiée (ADR 0007,
      point non supposé) : `podman run --rm -p 8443:8443
      docker.io/library/caddy:2-alpine caddy version` sous `maat-svc`.
- [ ] Redémarrage complet du VPS effectué une fois, conteneurs actifs vérifiés après
      reboot (pas seulement après une déconnexion SSH) : `reboot` puis, après
      reconnexion, `podman ps` sous `maat-svc`.

## 4. DNS

- [ ] Enregistrement A (ou AAAA) `app.<domaine>` → IP du VPS.
- [ ] Enregistrement A (ou AAAA) `api.<domaine>` → IP du VPS.
- [ ] Propagation vérifiée depuis l'extérieur du réseau du VPS : `dig +short
      app.<domaine>` et `dig +short api.<domaine>`

## 5. Code et secrets

- [ ] Dépôt cloné dans le répertoire de `maat-svc` : `git clone <url-depot> maat`
- [ ] `.env.production` créé à partir de `.env.production.example`, rempli, jamais
      commité (déjà exclu par `.gitignore`) : `cp .env.production.example
      .env.production`
- [ ] `Jwt__SigningKey` généré aléatoirement, ≥ 32 octets, jamais réutilisé d'un autre
      environnement (docs/specs/deploiement.md section 3) : `openssl rand -base64 48`
- [ ] `.env.production` en permissions `600`, lisible du seul `maat-svc` : `chmod 600
      .env.production`
- [ ] Paire de clés GPG du destinataire de sauvegarde générée **ailleurs que sur le
      VPS** ; seule la clé publique est importée sur le VPS
      (`BACKUP_GPG_RECIPIENT` dans `.env.production`) — la clé privée ne doit jamais
      resider sur la machine qu'elle est censée protéger.
- [ ] Remote rclone configuré vers un stockage objet en Union européenne : `rclone
      config`, référencé dans `RCLONE_REMOTE`.
- [ ] Fournisseur d'e-mail transactionnel européen configuré (Brevo, Scaleway TEM —
      CLAUDE.md) : `IEmailSender` réel enregistré, sans quoi le démarrage hors
      Development échoue volontairement (garde-fou déjà présent dans Program.cs). À
      défaut, `EMAIL_PROVIDER=none` (valeur par défaut de `.env.production.example`)
      démarre quand même via `NullEmailSender` (ADR 0009) — mais la vérification
      d'adresse et donc le rapport PDF restent inopérants tant que cette variable
      n'est pas basculée sur un fournisseur réel : à ne pas laisser en l'état au-delà
      d'un premier déploiement de vérification.

## 6. Premier déploiement

- [ ] Migrations EF Core à jour dans le dépôt cloné (générées en local au préalable).
- [ ] Déploiement lancé en tant que `maat-svc` : `~/maat/deploy/deploy.sh`
- [ ] Point de contrôle de santé répond depuis l'extérieur : `curl -s
      https://api.<domaine>/api/health`
- [ ] Parcours complet testé depuis un navigateur, sur la machine de production :
      inscription, connexion, diagnostic complet, génération et téléchargement du
      rapport PDF (docs/specs/deploiement.md, cas de test 5).
- [ ] Rapport PDF généré en production ouvert, polices Poppins/Inter vérifiées
      visuellement (pas de repli silencieux — section 9 de la spec).
- [ ] Cookie de rafraîchissement inspecté (outils développeur du navigateur) :
      `HttpOnly`, `Secure`, `SameSite=Strict` présents, renouvellement après quinze
      minutes fonctionnel.
- [ ] Erreur provoquée volontairement (ex. requête malformée) : la réponse ne révèle
      ni chemin de fichier, ni version, ni trace (docs/specs/deploiement.md section 3).

## 7. Sauvegarde et restauration

- [ ] Tâche cron quotidienne installée pour `maat-svc` : `crontab -e` puis
      `0 3 * * * /home/maat-svc/maat/deploy/backup.sh >> /home/maat-svc/backup.log 2>&1`
- [ ] Une sauvegarde manuelle déclenchée (`./deploy/backup.sh`) et son arrivée sur le
      stockage distant vérifiée (`rclone ls <remote>`).
- [ ] **Restauration testée au moins une fois**, dans une base vide, durée notée
      (spec : « une sauvegarde jamais restaurée n'en est pas une ») :
      `rclone copy <remote>/<fichier>.dump.gpg .` puis `gpg --decrypt --output
      maat-restore.dump <fichier>.dump.gpg` puis `podman compose -f
      docker-compose.prod.yml --env-file .env.production exec -T db pg_restore -U
      "$POSTGRES_USER" -d "$POSTGRES_DB" --clean --if-exists < maat-restore.dump`
- [ ] Durée mesurée de la restauration : `______________________`
- [ ] Durée de conservation des sauvegardes (`BACKUP_RETENTION_DAYS`) alignée sur le
      registre des traitements RGPD.

## 8. Supervision et journalisation

- [ ] Rotation des journaux applicatifs configurée (`logrotate` ou équivalent
      [distro]), purge à échéance définie.
- [ ] Vérification manuelle qu'aucune donnée personnelle n'apparaît dans les
      journaux (mot de passe, jeton, e-mail complet, réponse au questionnaire) :
      `podman compose -f docker-compose.prod.yml logs api | grep -iE
      'password|email|jwt'` — ne doit rien remonter de sensible en clair.
- [ ] Espace disque surveillé (ex. `df -h` en tâche cron avec seuil d'alerte) — la
      base, les journaux et les images de conteneurs le remplissent silencieusement.
- [ ] Disponibilité du point de contrôle de santé surveillée depuis l'extérieur
      (service de supervision externe à choisir, hors périmètre de ce dépôt).
- [ ] Échecs d'authentification répétés surveillés (déjà journalisés côté
      application, voir auth-securite-rgpd.md section 5).

## 9. Retour arrière

- [ ] Procédure connue et **déjà exécutée une fois** avant la mise en production
      réelle (docs/specs/deploiement.md, cas de test 14) : `IMAGE_TAG=<sha-precedent>
      podman compose -f docker-compose.prod.yml --env-file .env.production up -d
      --no-deps api`
- [ ] Rappel : une migration qui supprime une colonne n'est pas réversible par ce seul
      redémarrage — vérifier que la migration à annuler ne l'a pas fait
      (docs/specs/deploiement.md section 6 : les migrations destructrices se font en
      deux temps).

## 10. Vérification finale

- [ ] Les 14 cas de test de `docs/specs/deploiement.md` repassés un par un, sur la
      machine cible, en partant d'un état vierge (cas 13 : le déploiement n'est
      reproductible que s'il a été refait de zéro en suivant ce document).
