# Spécification — Coquille applicative et espace du compte

Périmètre : le cadre commun à tous les écrans — navigation, en-tête, identité
visuelle — et l'espace du compte, qui expose les droits RGPD déjà implémentés
côté serveur.

Ces deux chantiers ont la même nature : ils ne créent presque aucune capacité
nouvelle. Ils rendent atteignable ce qui existe déjà et qu'aucune interface
n'expose. Le rapport de projet affirme que « les utilisateurs peuvent demander la
suppression complète de leur compte depuis leur espace personnel » — cet espace
est le sujet de la section 4.

Dépendances : `auth-securite-rgpd.md` (rôles, effacement, portabilité,
vérification d'adresse), `dashboard.md`, `questionnaire.md`, `recommandations.md`,
`rapport-pdf.md` (points d'entrée), skill `charte-maat`.

---

## 1. Structure

Trois zones fixes, conformément à la section 6 de la charte.

```
┌────────────┬──────────────────────────────────────┐
│            │  en-tête : entreprise · menu compte  │
│  latérale  ├──────────────────────────────────────┤
│            │                                      │
│            │  contenu de l'écran                  │
│            │                                      │
└────────────┴──────────────────────────────────────┘
```

La barre latérale et l'en-tête sont rendus une fois, autour du routeur. Un écran
ne les redéclare jamais : sans cela, la position du contenu varie de quelques
pixels d'une page à l'autre, ce qui se voit immédiatement à la navigation.

**Pas de barre de recherche**, malgré la charte. Il n'y a rien à chercher : cinq
écrans, un diagnostic en cours, un historique de quelques lignes. Un champ de
recherche qui ne trouve rien est pire qu'absent. À réintroduire le jour où
l'historique justifie un filtre.

---

## 2. Identité

**Le nom sert de logo.** « MAAT » en Poppins Bold, blanc sur le bleu foncé de la
barre latérale, avec « diagnostic RSE » en Inter sous le nom, en plus petit et
plus discret. C'est déjà le traitement de la page de garde du PDF, et la
cohérence entre les deux supports vaut mieux qu'un pictogramme improvisé.

Ne pas dessiner de symbole faute d'en avoir un. Un logo faible attire l'œil et
affaiblit l'ensemble ; un mot bien posé ne se remarque pas, ce qui est
exactement le but.

Le nom renvoie au tableau de bord au clic, comme partout ailleurs sur le web.

---

## 3. Navigation

| Entrée | Destination | Visible pour |
| --- | --- | --- |
| Tableau de bord | `/` | tous |
| Diagnostic | questionnaire en cours, ou démarrage | tous |
| Plan d'actions | liste complète des recommandations | tous |
| Rapports | génération et historique | tous |
| Mon compte | espace du compte | tous |

Les icônes viennent de Lucide, comme prévu par la charte : `BarChart2`,
`FileText`, `CheckCircle`, `Settings`.

**Aucune entrée n'est masquée selon le rôle.** Un `Viewer` voit les cinq, et ce
sont les actions à l'intérieur des écrans qui sont désactivées — pas les écrans
eux-mêmes. Masquer une entrée laisse croire à un dysfonctionnement ; une action
désactivée avec un motif explicite enseigne le modèle de droits. C'est la même
logique que le bouton de rapport grisé pour un compte non vérifié.

**L'entrée active est signalée par deux moyens**, jamais par la seule couleur :
fond distinct (`bg-white/10`) et graisse typographique distincte (`font-semibold`).
Exigence WCAG déjà posée par les autres specs.

**État du diagnostic en cours.** Si un diagnostic est `InProgress`, l'entrée
« Diagnostic » porte son avancement — « 23 / 45 ». C'est le rappel le plus utile
de toute la navigation, puisque la promesse produit est de terminer.

---

## 4. En-tête

À gauche, la raison sociale de l'entreprise. À droite, un menu portant l'adresse
e-mail, le rôle, un lien vers l'espace du compte et la déconnexion.

**Le rôle est affiché en toutes lettres.** Trois rôles existent, leurs droits
diffèrent, et un utilisateur qui se voit refuser une action doit pouvoir
comprendre pourquoi sans quitter l'écran.

**Un bandeau si l'adresse n'est pas vérifiée**, sous l'en-tête, sur tous les
écrans, avec un bouton de renvoi. Ce n'est pas une décoration : sans
vérification, le rapport PDF — livrable central du produit — reste inaccessible.
Le bandeau se ferme pour la session, jamais définitivement.

---

## 5. Espace du compte

`/compte`. Quatre blocs, dans cet ordre de risque croissant.

**Identité.** Adresse e-mail, rôle, entreprise, date de création. L'adresse n'est
pas modifiable dans le MVP : la changer imposerait un nouveau cycle de
vérification et une gestion de l'adresse intermédiaire. À inscrire à la feuille
de route avec cette raison.

**Mot de passe.** Changement exigeant le mot de passe actuel, conformément au
patron de confirmation déjà en place. Un changement réussi invalide les autres
sessions — comportement attendu, à annoncer avant validation et non après.

**Mes données.** Export de l'ensemble des données personnelles et des diagnostics
de l'utilisateur, dans un format lisible et réexploitable. C'est le droit à la
portabilité, et il est déjà implémenté côté serveur.

**Suppression du compte.** Traitée à la section 6, parce qu'elle est
irréversible et qu'elle porte une règle métier que l'interface ne peut pas
inventer.

---

## 6. Suppression du compte

**La confirmation exige de saisir son adresse e-mail**, pas de cliquer « oui ».
Une action irréversible ne doit pas pouvoir être déclenchée par un réflexe.

**L'écran énonce ce qui disparaît et ce qui reste**, avant la saisie et non dans
une note en bas. L'utilisateur doit savoir si ses diagnostics partent avec lui ou
restent attachés à l'entreprise — c'est la question qu'il se pose, et la réponse
appartient à `auth-securite-rgpd.md`, pas à l'interface. À vérifier contre la
spec et contre le comportement réel de `AccountRgpdTests` avant de rédiger le
texte : une promesse d'effacement que le code ne tient pas est pire que pas de
promesse du tout.

**Corrigé — c'était une élévation de privilège, pas seulement un écart de
documentation.** La version précédente de cette section documentait fidèlement
un comportement serveur où `DELETE /api/me` supprimait toujours l'entreprise
entière, sans distinction de rôle ni comptage d'administrateurs : un `Viewer`
seul pouvait ainsi supprimer l'`Admin`, tous les `User` et tous les
diagnostics de son entreprise en supprimant son propre compte. `AccountService.
DeleteAccountAsync` applique désormais la règle suivante, vérifiée par les cas
19 à 22bis (`AccountRgpdTests.cs`, y compris sur des entreprises à plusieurs
comptes — la classe ne testait auparavant que des entreprises à administrateur
unique, ce qui explique que le défaut ne soit jamais apparu) :

- si l'appelant est le **dernier `Admin`** de son entreprise (le seul, en
  comptant lui-même) : l'entreprise entière disparaît avec lui — tous ses
  comptes, tous ses diagnostics, réponses, scores et rapports ;
- sinon (`Viewer`, `User`, ou `Admin` alors qu'un autre `Admin` existe) :
  **seul son propre compte** disparaît. L'entreprise, les autres comptes et
  tous les diagnostics restent intacts. Les rapports que ce compte a lui-même
  générés disparaissent avec lui (`Report.generated_by_user_id` est en
  `ON DELETE RESTRICT` — ils doivent être purgés avant le compte, sans quoi la
  suppression échouerait), jamais ceux générés par un autre compte.

`GET /api/auth/me` expose `isLastAdmin` : c'est cette valeur, lue avant la
saisie, qui détermine lequel des deux textes l'écran affiche (section 7).
Les diagnostics n'appartiennent pas à un utilisateur individuel (pas de
propriétaire en base) : ils sont rattachés à l'entreprise entière, donc « que
deviennent mes diagnostics » ne se pose que dans le cas du dernier `Admin` —
dans tous les autres cas, la réponse est « rien, ils restent ».

---

## 7. Frontend

Squelette de chargement plutôt qu'un indicateur centré, comme au tableau de bord.

**Un lien d'évitement** en tête de page, visible au focus, menant au contenu
principal. Sans lui, un utilisateur au clavier traverse cinq entrées de
navigation à chaque changement d'écran.

Sous 768 px, la barre latérale se replie derrière un bouton et se déploie en
panneau. Le panneau piège le focus tant qu'il est ouvert et se ferme à `Échap`.

Toutes les valeurs proviennent du skill `charte-maat`. Aucune valeur hexadécimale
inventée.

---

## Cas de test

**Coquille**

1. La navigation est rendue une seule fois : passer d'un écran à l'autre ne remonte pas le composant.
2. L'entrée active est signalée par deux moyens distincts, dont un non chromatique.
3. Un diagnostic `InProgress` affiche son avancement dans l'entrée « Diagnostic ».
4. `Viewer` voit les cinq entrées de navigation.
5. Le nom de l'application renvoie au tableau de bord.
6. Lien d'évitement présent, atteignable au premier `Tab`, menant au contenu principal.
7. Sous 768 px, la barre latérale se replie ; le panneau ouvert piège le focus et se ferme à `Échap`.
8. Test axe sur la coquille, dans les trois états du tableau de bord.

**En-tête**

9. Le rôle du principal authentifié est affiché en toutes lettres.
10. Adresse non vérifiée → bandeau présent, bouton de renvoi fonctionnel.
11. Adresse vérifiée → aucun bandeau.
12. Déconnexion → jeton effacé de la mémoire, redirection vers la connexion.

**Compte**

13. Les informations affichées sont celles du principal authentifié, jamais d'un autre compte.
14. Changement de mot de passe sans le mot de passe actuel → refusé.
15. Changement réussi → les autres sessions sont invalidées.
16. Export → contient les données de l'utilisateur et ses diagnostics, aucune donnée d'une autre entreprise.
17. `Viewer` → export accessible, c'est son droit personnel et non une action d'administration.

**Suppression**

18. Confirmation par une adresse e-mail incorrecte → refusée.
19. Suppression par un `Viewer` ou un `User` → **seul son propre compte
    disparaît** ; l'entreprise, les autres comptes et les diagnostics restent
    intacts.
20. Suppression par un `Admin` alors qu'un autre `Admin` existe → même
    résultat que le cas 19 : seul son propre compte disparaît.
21. Suppression par le **dernier** `Admin` → l'entreprise entière disparaît
    avec lui — tous ses comptes, tous ses diagnostics — et l'écran l'avait
    annoncé (section 6).
22. Suppression d'un compte qui a lui-même généré un rapport, alors qu'il
    n'est pas le dernier `Admin` → réussit, et n'emporte que ce rapport, pas
    ceux générés par un autre compte pour le même diagnostic.
22bis. Après suppression, la reconnexion avec les mêmes identifiants échoue —
    pour le compte supprimé seul dans les cas 19/20/22, pour tous les comptes
    de l'entreprise dans le cas 21.
23. Les tables de référence — questions, recommandations, pondérations — sont intactes.

Les cas 19 à 22 sont ceux à vérifier réellement plutôt qu'à asserter : ce sont
eux qui déterminent la portée exacte d'une action irréversible, et ce sont eux
dont le texte d'interface doit correspondre exactement au comportement du
serveur. `AccountRgpdTests.cs` ne testait auparavant que des entreprises à
administrateur unique (`RegisterCompanyAndLoginAdminAsync` crée toujours une
entreprise fraîche avec un seul `Admin`) — les cas 19, 20 et 22 exigent une
entreprise à plusieurs comptes, sans quoi l'élévation de privilège corrigée par
ce commit resterait indétectable.
