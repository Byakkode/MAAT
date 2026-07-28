# 0004 — Hachage des jetons de rafraîchissement et liste locale de mots de passe compromis

## Statut

Acceptée — 2026-07-27

## Contexte

La spécification `docs/specs/auth-securite-rgpd.md` (sections 1 à 3 :
inscription, connexion, rotation et détection de réutilisation des refresh
tokens) laisse deux choix ouverts à l'implémentation, chacun avec une
consigne explicite de les tracer ici plutôt que de les trancher en silence
dans le code.

## Décision

### Les refresh tokens et jetons de vérification d'adresse sont hachés en SHA-256, pas en bcrypt

`RefreshToken.TokenHash` et `EmailVerificationToken.TokenHash`
(`MAAT.Infrastructure/Security/TokenHasher.cs`) utilisent SHA-256, alors que
les mots de passe utilisateur utilisent bcrypt (coût 12).

Un mot de passe est choisi par un humain : son entropie réelle est faible
(quelques dizaines de bits au mieux), ce qui rend une attaque hors ligne par
force brute réaliste si le hash est rapide à calculer — d'où bcrypt, délibérément
lent. Un refresh token ou un jeton de vérification n'est jamais choisi par un
humain : `SecureTokenGenerator` (`MAAT.Application/Security`) produit 256 bits
générés par `RandomNumberGenerator`, un espace de recherche hors de portée
d'une attaque par force brute quel que soit l'algorithme de hachage utilisé
pour le stocker. Un hachage lent n'apporterait donc aucune protection
supplémentaire ici, mais coûterait cher : chaque `POST /api/auth/refresh`
nécessite de retrouver le jeton en base par recherche indexée
(`IX_refresh_tokens_token_hash`), ce que bcrypt interdit puisque son sel
aléatoire rend deux hachages du même jeton différents d'un appel à l'autre.

Cela ne réintroduit pas de comparaison de secret en `==` (interdite par la
spec) : le jeton présenté est haché puis recherché par égalité de hash
indexée en base, jamais comparé directement en tant que chaîne applicative.

### Mots de passe compromis : liste locale plutôt qu'appel à l'API Have I Been Pwned

La spec autorise les deux options et impose de tracer le choix.
`ICompromisedPasswordChecker` est implémenté par
`LocalListCompromisedPasswordChecker`, qui charge une liste de mots de passe
courants embarquée dans l'assembly (`MAAT.Infrastructure/Security/common-passwords.txt`)
plutôt que d'interroger l'API HIBP en k-anonymat.

Le mode k-anonymat de HIBP (envoi des cinq premiers caractères du hash SHA-1)
est effectivement compatible avec la souveraineté des données : aucune
donnée personnelle ne quitte le serveur. Mais l'endpoint d'inscription
dépendrait alors de la disponibilité d'un service tiers hors de notre
contrôle — une panne ou une latence de HIBP deviendrait une panne de
l'inscription MAAT. Une liste locale élimine cette dépendance externe et
garde le test d'intégration (cas 4) déterministe et hors ligne, condition
posée par ce projet pour les tests d'intégration.

**Mise à jour du 2026-07-27 : remplacement par une liste réelle.** La liste
initiale (quelques dizaines d'entrées fabriquées à but de démonstration) a été
remplacée par `Passwords/Common-Credentials/100k-most-used-passwords-NCSC.txt`
du dépôt [SecLists](https://github.com/danielmiessler/SecLists)
(`danielmiessler/SecLists`, branche `master`, récupérée le 2026-07-27). Cette
liste est celle publiée par le NCSC (National Cyber Security Centre,
Royaume-Uni) à partir du corpus *Pwned Passwords* de Troy Hunt (Have I Been
Pwned), dans le cadre de sa campagne « Cyber Aware » recommandant de bloquer
les mots de passe les plus fréquemment compromis — un usage directement
équivalent à celui de `ICompromisedPasswordChecker` ici. Le fichier source
contient 99 840 entrées.

**Filtrage à 12 caractères minimum.** `AuthService.RegisterAsync` rejette
tout mot de passe de moins de 12 caractères (politique de longueur, section 1)
*avant* d'appeler `ICompromisedPasswordChecker` : aucune valeur transmise à
`IsCompromisedAsync` ne peut donc jamais faire moins de 12 caractères. Sur les
99 840 entrées de la liste NCSC, seules 1 212 atteignent cette longueur — les
98 628 autres ne peuvent structurellement jamais correspondre à une entrée
vérifiée et n'auraient fait qu'alourdir la ressource embarquée et le temps de
chargement pour rien. Le fichier embarqué
(`MAAT.Infrastructure/Security/common-passwords.txt`) ne contient donc que ce
sous-ensemble de 1 212 entrées, filtré depuis la liste NCSC avec `awk
'length($0) >= 12'` (aucune modification du contenu des entrées conservées,
uniquement une exclusion par longueur).

Conséquence assumée, à surveiller : ce filtrage couple la liste à la valeur
actuelle de `MinimumPasswordLength` (12). Si cette politique de longueur
change un jour, la liste embarquée doit être régénérée depuis la source NCSC
avec le nouveau seuil — sans quoi elle sous-couvrirait silencieusement les
mots de passe compromis désormais acceptés par la politique de longueur.

**Chargement au démarrage, pas à la première vérification.**
`LocalListCompromisedPasswordChecker` charge la ressource embarquée dans un
`HashSet<string>` une seule fois, à la construction de l'instance (elle-même
enregistrée en Singleton). `Program.cs` force cette construction juste après
`builder.Build()`, aux côtés des autres garde-fous de démarrage, pour que le
coût soit payé avant `app.Run()` plutôt qu'au hasard de la première
inscription. Mesuré isolément (hors coût de démarrage ASP.NET Core lui-même) :
construction à froid ~9 ms, à chaud ~0,3 ms — négligeable devant le temps de
démarrage total de l'application (~600 ms, mesuré en Development sur ce
poste), qui reste dominé par l'amorçage du hôte .NET et non par ce
chargement.

### Réutilisation détectée : révocation de toutes les sessions actives de l'utilisateur, pas seulement de la chaîne du jeton volé

`RefreshTokenRepository.RevokeAllActiveForUserAsync` révoque, par `UserId`,
tous les refresh tokens non encore révoqués — pas seulement ceux
descendant du jeton présenté en réutilisation. L'entité `RefreshToken`
existante ne porte pas de identifiant de « famille » ou de jeton parent :
ajouter cette notion aurait demandé une colonne et une migration
supplémentaires pour un gain de précision non requis par la spec, qui parle
explicitement de révoquer « toute la famille de jetons de cet utilisateur ».
Conséquence assumée : un utilisateur connecté sur plusieurs appareils est
déconnecté de tous ses appareils dès qu'une réutilisation est détectée sur
l'un d'eux, même si les autres sessions n'ont pas été compromises — c'est le
comportement recommandé par l'OWASP cité dans la spec, pas une simplification
qui l'affaiblit.

## Conséquences

- Aucune donnée de mot de passe en clair ni aucun jeton en clair n'est
  persisté : seuls les hachages le sont, conformément à la spec.
- Remplacer la liste locale par l'API HIBP, ou régénérer la liste locale
  filtrée si `MinimumPasswordLength` change, ne demande de modifier que
  `LocalListCompromisedPasswordChecker` (ou son enregistrement DI dans
  `Program.cs`) : `ICompromisedPasswordChecker` isole ce choix du reste du
  service d'authentification.
- Introduire un jour un identifiant de famille de jetons (pour ne révoquer
  que la chaîne compromise plutôt que toutes les sessions) demandera une
  migration de `refresh_tokens` ; ce n'est pas fait tant que la spec ne
  l'exige pas.
