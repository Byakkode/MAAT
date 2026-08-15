# Spécification — Déploiement

Périmètre : mise en production de MAAT sur un VPS OVHcloud, nommage des
domaines, chiffrement du transport, secrets, migrations, données de référence,
sauvegarde, supervision et retour arrière.

C'est le seul document du projet dont l'échec ne se voit pas en test : tout
passe en local et rien ne fonctionne sur la cible. Il porte aussi des délais
incompressibles — propagation DNS, émission de certificats — qui ne
s'accélèrent pas le jour où l'on en a besoin.

Dépendances : `auth-securite-rgpd.md` (jetons, cookie de rafraîchissement,
en-têtes de durcissement), `modele-donnees.md` (migrations, chargement des
données de référence), `rapport-pdf.md` (polices embarquées, coût CPU de la
génération), `CLAUDE.md` (contrainte d'hébergement non négociable).

---

## 1. Nommage des domaines — à trancher en premier

Cette décision conditionne tout le reste et doit être prise **avant** de
réserver quoi que ce soit.

Le jeton de rafraîchissement est un cookie `SameSite=Strict`
(`auth-securite-rgpd.md`). Un tel cookie n'est envoyé que si le site appelant
et le site appelé partagent le même **domaine enregistrable**. Concrètement :

| Frontend | API | Cookie transmis |
| --- | --- | --- |
| `app.maat.fr` | `api.maat.fr` | oui — même domaine enregistrable |
| `maat.fr` | `api.maat.fr` | oui |
| `maat.fr` | `maat-api.io` | **non** |

Le troisième cas imposerait `SameSite=None`, donc une exposition au CSRF que la
spec de sécurité écarte explicitement, et une révision complète de la stratégie
de jetons. Ce n'est pas une préférence esthétique : c'est le choix du nom de
domaine qui décide de l'architecture d'authentification.

**Retenu** : un seul domaine enregistrable, deux sous-domaines. Le cookie est
émis sur le domaine parent avec l'attribut `Domain`, de sorte qu'il accompagne
les appels vers le sous-domaine de l'API.

Réserver le domaine dès maintenant. La propagation DNS et la vérification de
propriété prennent des heures à des jours selon le registrar.

---

## 2. Cible

VPS OVHcloud, datacenter France (Roubaix, Gravelines ou Strasbourg). Aucun
service tiers hors Union européenne, y compris pour un usage périphérique —
c'est un argument commercial du produit et un engagement du rapport, pas une
préférence technique.

Trois processus sur la machine :

- PostgreSQL 18, en conteneur, port non exposé publiquement ;
- l'API .NET, en conteneur, écoutant en clair sur la boucle locale uniquement ;
- un proxy inverse en frontal, seul processus joignable depuis l'extérieur,
  qui termine TLS et sert les fichiers statiques du frontend.

Le frontend est un ensemble de fichiers statiques produits par `npm run build`.
Il n'a besoin d'aucun processus applicatif : le proxy le sert directement.

**Une réserve de mémoire est nécessaire pour la génération PDF.** C'est
l'opération la plus coûteuse du produit, et SkiaSharp alloue hors du tas managé
— une machine dimensionnée au plus juste sur la seule empreinte .NET se fera
tuer par l'OOM killer au premier rapport d'un utilisateur réel, pas en test.

**Podman ou Docker en production ?** Le poste de développement est en Podman
rootless pour de bonnes raisons, mais elles ne s'appliquent pas à un serveur
dédié. Trancher explicitement dans une ADR plutôt que par habitude, et retenir
que les fichiers `compose` diffèrent peu : ce qui change est le mode
d'exécution, pas la description des services.

---

## 3. Configuration par environnement

Aucun fichier `appsettings.Production.json` en clair dans le dépôt contenant un
secret. Toute valeur sensible vient de l'environnement.

Ce qui change entre `Development` et `Production` :

| Réglage | Development | Production |
| --- | --- | --- |
| `Cors:AllowedOrigins` | `http://localhost:5173`, `http://127.0.0.1:5173` | l'origine du frontend, en HTTPS, sans barre oblique finale |
| Redirection HTTPS | désactivée | activée |
| HSTS | désactivé | activé |
| Détail des erreurs | complet | message générique, détail au journal seulement |
| Chargement des données de référence | automatique au démarrage | commande explicite |
| Jeu de démonstration | disponible | **jamais** |
| Clé de signature JWT | valeur de développement du dépôt | secret injecté, jamais commité |

Le détail des erreurs mérite une vérification réelle : une page d'exception
.NET révèle chemins de fichiers, versions de paquets et parfois fragments de
requête. C'est une fuite d'information, et elle se constate en provoquant une
erreur sur la machine cible, pas en lisant la configuration.

**Génération des secrets.** La clé de signature JWT de production est produite
aléatoirement sur au moins 32 octets, jamais dérivée d'un mot de passe ni
réutilisée d'un autre environnement. Elle est stockée hors du dépôt, dans un
fichier lisible du seul utilisateur de service, et sa rotation invalide toutes
les sessions en cours — comportement attendu, à documenter.

---

## 4. Transport

TLS obligatoire sur les deux sous-domaines, certificats Let's Encrypt avec
renouvellement automatique. Vérifier que le renouvellement fonctionne
réellement plutôt que de le supposer : un certificat qui expire à quatre-vingt-dix
jours tombera pendant vos vacances, ou la veille de la soutenance.

Le proxy porte les en-têtes de durcissement déjà appliqués par l'API
(`auth-securite-rgpd.md`). Vérifier qu'ils ne sont ni dupliqués ni écrasés :
un `Content-Security-Policy` émis deux fois avec des valeurs différentes donne
un résultat dépendant du navigateur.

Le frontend est servi avec un cache long sur les ressources versionnées par
Vite, et **sans cache sur `index.html`** — sinon un déploiement ne parvient pas
aux navigateurs qui ont déjà visité le site, et l'application continue de
charger des ressources disparues.

---

## 5. Réseau et accès

- Pare-feu : seuls 80, 443 et le port SSH sont ouverts. PostgreSQL n'est
  **jamais** joignable depuis l'extérieur, même protégé par mot de passe.
- SSH par clé uniquement, authentification par mot de passe désactivée,
  connexion `root` interdite.
- Un utilisateur de service dédié à l'application, sans droits d'administration.
- Mises à jour de sécurité du système automatiques.

Ces quatre points sont ceux qu'un jury peut vérifier en une commande, et le
scan de ports d'un VPS public commence dans les minutes qui suivent sa création.

---

## 6. Déploiement

Séquence, dans cet ordre :

1. sauvegarde de la base **avant** toute migration ;
2. construction des images ou récupération des artefacts ;
3. application des migrations EF Core ;
4. chargement des données de référence (`seed`), qui doit être idempotent ;
5. démarrage de la nouvelle version ;
6. vérification de santé ;
7. bascule du proxy.

Les migrations ne s'appliquent **jamais** automatiquement au démarrage de
l'application. Deux instances qui démarrent en même temps appliqueraient la
même migration concurremment ; et surtout, une migration lancée par le
processus applicatif s'exécute sans qu'on ait choisi le moment ni vérifié la
sauvegarde. C'est une étape de déploiement, pas un effet de bord du démarrage.

**Point de contrôle de santé** : un endpoint public, sans authentification, qui
vérifie que l'API répond et que la base est joignable. Il ne divulgue ni
version, ni détail d'infrastructure — une simple indication de disponibilité.

**Retour arrière.** Savoir revenir à la version précédente en une commande, et
l'avoir essayé au moins une fois. Attention : une migration qui supprime une
colonne n'est pas réversible par le seul redémarrage de l'ancienne image. Les
migrations destructrices se font en deux temps — d'abord cesser d'utiliser la
colonne, déployer, puis la supprimer au déploiement suivant.

---

## 7. Sauvegarde

Sauvegarde quotidienne automatisée de PostgreSQL, conservée **hors de la
machine** — une sauvegarde stockée sur le VPS qu'elle protège ne protège de
rien.

Destination en Union européenne, cohérente avec la contrainte d'hébergement.

**Une sauvegarde jamais restaurée n'est pas une sauvegarde.** Restaurer une
fois dans une base vide, vérifier que l'application démarre dessus, et noter la
durée de l'opération — c'est la réponse à la question « combien de temps pour
repartir ? », qui sera posée.

Les sauvegardes contiennent des données personnelles : accès restreint, et
durée de conservation cohérente avec les engagements du registre des
traitements. Une suppression de compte doit finir par disparaître aussi des
sauvegardes, à l'expiration de leur rétention — le préciser dans la politique
de confidentialité plutôt que de promettre un effacement immédiat qu'aucun
système ne tient.

---

## 8. Journalisation et supervision

Journaux applicatifs conservés sur la machine avec rotation, et purge à
échéance définie.

**Aucune donnée personnelle dans les journaux** : ni mot de passe, ni jeton, ni
adresse e-mail complète, ni réponse au questionnaire. C'est le point où le RGPD
se perd le plus souvent, parce que la fuite est involontaire — une exception
non filtrée qui recopie le corps d'une requête suffit.

Surveiller au minimum : espace disque restant, disponibilité du point de
contrôle de santé, et échecs d'authentification répétés. Le disque est la
première cause d'arrêt d'un petit serveur, et la base plus les journaux plus les
images de conteneurs le remplissent silencieusement.

---

## 9. Ce qui casse en production et pas en local

Liste des pièges identifiés, à vérifier explicitement sur la cible.

**Les polices du rapport PDF.** Déjà traité par l'embarquement
(`rapport-pdf.md` section 5), mais c'est le piège classique : un VPS Linux nu
n'a ni Poppins ni Inter, et QuestPDF se replie sans erreur. Générer un rapport
réel depuis la machine de production et l'ouvrir.

**Les dépendances natives de SkiaSharp.** Le paquet natif Linux doit être
présent, et l'image de base doit fournir les bibliothèques système qu'il
attend. L'échec est une exception au chargement, au premier rapport seulement.

**Le fuseau horaire et la culture.** Un conteneur nu est en UTC et en culture
invariante. Les dates affichées dans le rapport et le format des nombres
décimaux peuvent différer de ce que vous voyez en local. Fixer explicitement
la culture de formatage plutôt que de dépendre de l'environnement.

**Le cookie `Secure`.** Il n'est pas transmis en HTTP simple. Toute la chaîne
doit être en HTTPS, y compris entre le proxy et l'API si elle n'est pas sur la
boucle locale.

**L'origine CORS.** Elle change entre les environnements et se compare
littéralement. Une barre oblique finale, un `www` de trop, ou du HTTP au lieu
du HTTPS suffit à tout bloquer — le défaut le plus fréquent de ce projet.

---

## Cas de test

Ces vérifications se font **contre la machine cible**, une fois déployée. Elles
ne sont pas automatisables dans la suite de tests et constituent une liste de
contrôle de mise en production.

**Transport et accès**

1. `http://` sur les deux sous-domaines redirige vers `https://`.
2. Le certificat est valide et son renouvellement automatique est configuré.
3. Le port PostgreSQL n'est pas joignable depuis l'extérieur.
4. La connexion SSH par mot de passe est refusée.

**Application**

5. Inscription, connexion, diagnostic complet, rapport PDF — le parcours entier depuis un navigateur, sur la machine de production.
6. Le PDF généré en production est identique à celui généré en local pour les mêmes données, aux polices près — vérifier qu'elles ne sont pas remplacées.
7. Le cookie de rafraîchissement est émis avec `HttpOnly`, `Secure`, `SameSite=Strict`, et le renouvellement de session fonctionne après quinze minutes.
8. Une erreur provoquée ne révèle aucun détail d'infrastructure.
9. Le point de contrôle de santé répond, et signale une base indisponible.

**Données**

10. Les données de référence sont chargées et `seed` relancé ne crée aucun doublon.
11. Aucune donnée de démonstration n'est présente en production.
12. Une sauvegarde est produite automatiquement, et une restauration a été réalisée avec succès au moins une fois.

**Déploiement**

13. Un déploiement complet a été exécuté depuis un état vierge, en suivant uniquement ce document.
14. Un retour à la version précédente a été effectué avec succès.

Le cas 13 est celui qui compte : tant que le déploiement n'a pas été refait de
zéro en suivant ce document, il n'est pas reproductible — il tient dans la
mémoire de la personne qui l'a fait la première fois.
