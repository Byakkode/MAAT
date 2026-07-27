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

Conséquence assumée : la liste embarquée (quelques centaines d'entrées, à
but de démonstration) est très en deçà des ~100 000 mots de passe recommandés
en production, et ne couvre qu'une fraction des mots de passe réellement
compromis. Avant une mise en production, cette liste doit être remplacée par
le jeu complet des 100 000 mots de passe les plus courants (ou par un appel
HIBP si la dépendance externe est jugée acceptable) — cette ADR ne clôt pas
la question, elle documente uniquement le choix retenu pour ce périmètre.

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
- Remplacer la liste locale par l'API HIBP ou par le jeu complet de 100 000
  mots de passe ne demande de modifier que
  `LocalListCompromisedPasswordChecker` (ou son enregistrement DI dans
  `Program.cs`) : `ICompromisedPasswordChecker` isole ce choix du reste du
  service d'authentification.
- Introduire un jour un identifiant de famille de jetons (pour ne révoquer
  que la chaîne compromise plutôt que toutes les sessions) demandera une
  migration de `refresh_tokens` ; ce n'est pas fait tant que la spec ne
  l'exige pas.
