# Modèle de menace DomainBridge

- Version : 1.0 — 2026-11-13 (tranche 0.4 du plan de développement)
- Statut : **rédigé, en attente de relecture par l'équipe sécurité**
- Portée : la cible décrite par `docs/PRODUCT_BRIEF.md` et les ADR 001 à 003, pas seulement le code actuel. L'état actuel du code est rappelé en section 7.

## 1. Pourquoi ce produit est une cible

DomainBridge concentre trois pouvoirs rarement réunis :

1. un service qui tourne sous le **compte système** sur chaque poste et modifie droits de fichiers, registre et appartenance au domaine ;
2. des **droits d'écriture délégués dans deux annuaires** (réinitialisation de comptes ordinateurs dans B, et dans A pour le retour arrière) ;
3. la capacité d'**ordonner un redémarrage et un changement d'identité** à des centaines de postes en une action.

Un défaut peut donc mener à une élévation de privilèges locale, à l'usurpation d'un compte ordinateur de domaine, à la divulgation d'un profil à la mauvaise personne ou à l'indisponibilité du parc.

## 2. Biens à protéger

| # | Bien | Pourquoi il compte |
|---|---|---|
| B1 | Jeton de jonction hors ligne | Contient le secret du compte ordinateur dans B : qui le détient peut se faire passer pour ce poste dans le domaine |
| B2 | Clé privée du certificat d'agent | Identité du poste auprès de l'API ; permet de recevoir ses tâches et son jeton |
| B3 | Droits délégués des connecteurs (gMSA) dans A et B | Réinitialiser un compte ordinateur = le détourner ou le rendre inutilisable |
| B4 | Pouvoir d'approuver et de lancer une vague | Déclenche des changements irréversibles à court terme sur de nombreux postes |
| B5 | Journal d'audit | Seule preuve de qui a fait quoi ; base du support et du retour arrière |
| B6 | Profils utilisateurs sur les postes | Données personnelles et professionnelles ; confidentialité et intégrité |
| B7 | Liste de migration, correspondances de comptes, inventaire | Données nominatives ; une correspondance fausse donne un profil à la mauvaise personne |
| B8 | Binaire et MSI de l'agent | Exécutés en compte système sur tout le parc |
| B9 | État de retour arrière stocké sur le poste | Sa falsification peut empêcher le retour ou le détourner |
| B10 | Autorité de certification des agents | Qui la contrôle fabrique des identités d'agent |

## 3. Acteurs de menace

| # | Acteur | Capacité supposée |
|---|---|---|
| M1 | Attaquant sur le réseau interne | Écoute, rejoue, tente de joindre l'API et les connecteurs ; pas de compte |
| M2 | Utilisateur standard d'un poste | Session locale sans droits d'administration ; maîtrise le contenu de son propre profil |
| M3 | Administrateur local d'un poste, ou poste compromis | Contrôle total du poste, donc de la clé de son agent si elle n'est pas dans le TPM |
| M4 | Opérateur malveillant ou compte d'opérateur compromis | Accès légitime à la console avec un rôle |
| M5 | Attaquant ayant compromis le serveur DomainBridge | Contrôle de l'API et de la base |
| M6 | Attaquant de la chaîne de fabrication | Peut altérer une dépendance, la CI, le partage GPO ou le paquet Intune |

Hors modèle : un attaquant déjà administrateur du domaine A ou B (il n'a pas besoin de DomainBridge), et l'accès physique prolongé à un poste non chiffré.

## 4. Frontières de confiance et flux

```text
 Opérateur (navigateur) ──OIDC Entra + rôles──► API centrale ◄──mTLS, sortant──── Agent (compte système)
                                                 │      │                              │
                                              PostgreSQL│                         poste : profils,
                                                        │                         registre, domaine
                                   canal authentifié interne
                                          ┌─────────────┴─────────────┐
                                   Connecteur A (gMSA)          Connecteur B (gMSA)
                                   lecture + réinit. OU         lecture + réinit. OU
                                          │                           │
                                      Annuaire A                  Annuaire B

 CI ──► MSI signé ──► Intune / partage GPO ──► poste
```

Frontières : (F1) poste ↔ API, (F2) navigateur ↔ API, (F3) API ↔ connecteurs, (F4) connecteurs ↔ annuaires, (F5) API ↔ base, (F6) fabrication ↔ poste, (F7) utilisateur standard ↔ agent sur le même poste.

## 5. Menaces et parades

Catégories : **U** usurpation, **A** altération, **R** répudiation, **D** divulgation, **I** indisponibilité, **E** élévation de privilèges.
État : *ouvert* = le code actuel est vulnérable ; *à concevoir* = la fonction n'existe pas encore, la parade est une exigence de conception.

### 5.1 Agent ↔ API (F1)

| # | Cat. | Scénario | Parade exigée | Tranche | État |
|---|---|---|---|---|---|
| T1 | U | Un poste compromis (M3) se présente sous l'identité d'un autre poste et récupère ses tâches ou son jeton de jonction | L'identité du poste est lue dans le certificat présenté, jamais dans l'URL ou le corps. Émetteur épinglé sur l'autorité DomainBridge | 2.2 | ouvert (S3, S4) |
| T2 | U | Un appareil inconnu obtient un certificat d'agent (M1) | L'enrôlement exige une preuve que le demandeur est bien le poste nommé : profil de certificat Intune, ou preuve d'identité machine du domaine A vérifiée côté serveur. Jamais d'enrôlement sur simple déclaration | 2.2 | ouvert |
| T3 | U, D | Vol de la clé privée de l'agent sur un poste (M3) | Clé non exportable, dans le TPM quand il existe. Révocation par poste. Le vol ne donne accès qu'aux tâches de ce poste | 2.2 | à concevoir |
| T4 | A | Rejeu ou modification d'une tâche en transit (M1) | TLS mutuel ; tâche liée au poste, à durée courte, avec clé d'idempotence ; l'agent refuse toute tâche hors d'une liste fermée d'étapes | 2.4, 4.1 | à concevoir |
| T5 | E | La console envoie du code arbitraire aux postes (M4, M5) | Aucune tâche ne transporte de script, de ligne de commande ni de chemin libre : uniquement un identifiant d'étape et des paramètres typés et validés par l'agent | 4.1 | acquis par conception, à maintenir |

### 5.2 Jeton de jonction (B1)

| # | Cat. | Scénario | Parade exigée | Tranche | État |
|---|---|---|---|---|---|
| T6 | D | Le jeton est lu en transit, dans la base, dans un journal ou sur le disque du poste | Chiffré par le connecteur B pour la clé publique du certificat du poste destinataire : l'API et la base ne voient qu'un bloc illisible. Jamais journalisé (test dédié). Effacé du poste dès application | 4.3 | à concevoir |
| T7 | U | Le serveur compromis (M5) demande un jeton pour le poste X en fournissant le certificat d'un poste qu'il contrôle | Le connecteur vérifie lui-même que le certificat est émis par l'autorité DomainBridge **et** que son nom correspond au compte ordinateur demandé. Il ne fait pas confiance à l'API sur ce point | 4.3 | à concevoir |
| T8 | A | Un jeton ancien est réutilisé | Durée de vie courte ; chaque émission réinitialise le compte, ce qui invalide le jeton précédent ; un seul jeton actif par poste | 4.3 | à concevoir |
| T9 | I, U | Le connecteur est détourné pour réinitialiser en masse des comptes ordinateurs (M5) | Délégation limitée à une OU ; le connecteur n'agit que pour un poste présent dans une vague approuvée ; plafond de débit ; alerte au-delà. Serveur de connecteur durci et isolé | 4.3, 5.2 | à concevoir |

### 5.3 Poste : utilisateur standard ↔ agent (F7)

C'est la zone la plus exposée aux élévations de privilèges, car l'agent manipule en compte système un dossier que l'utilisateur contrôle.

| # | Cat. | Scénario | Parade exigée | Tranche | État |
|---|---|---|---|---|---|
| T10 | E | L'utilisateur (M2) place dans son profil un lien ou une jonction vers un dossier système ; l'agent, en ajoutant les droits du compte B, lui donne accès à ce dossier | Ne jamais suivre les points de réanalyse ; chemin du profil lu uniquement dans la liste des profils du registre machine ; refus de tout chemin hors du dossier des profils ; test d'intégration dédié avec jonction piégée | 4.2 | à concevoir |
| T11 | E | Remplacement d'un fichier de l'agent, d'une bibliothèque chargée ou de sa configuration par M2 | Dossiers d'installation et de données réservés au système et aux administrateurs ; chemin de service entre guillemets ; aucune bibliothèque chargée depuis un dossier inscriptible | 2.5 | à concevoir |
| T12 | A | Falsification du journal local ou de l'état de retour arrière (B9) pour détourner la reprise | Stockage réservé au système ; chaque étape revérifie l'état réel du poste au lieu de croire le journal | 4.1 | à concevoir |
| T13 | E | L'agent expose un canal local (port, tube nommé) utilisable par M2 | Aucun canal d'écoute local. L'agent ne fait que des appels sortants | 2.5 | acquis par conception, à maintenir |

### 5.4 Profils et correspondance de comptes (B6, B7)

| # | Cat. | Scénario | Parade exigée | Tranche | État |
|---|---|---|---|---|---|
| T14 | D | Une correspondance erronée ou malveillante rattache le profil d'une personne au compte d'une autre | Comptes résolus par les connecteurs dans les annuaires, pas saisis librement. Tout login différent entre A et B exige une correspondance explicite visible à l'approbation. Écart = quarantaine | 3.2, 3.3 | à concevoir |
| T15 | D | La liste importée contient une ligne piégée (injection de formule, chemin, caractères de contrôle) | Validation stricte du format ; noms confrontés à l'annuaire ; échappement à l'export CSV | 3.3 | à concevoir |
| T16 | D | L'inventaire remonte plus que nécessaire (contenu de profils, fichiers) | Liste fermée de champs ; jamais de contenu de profil ; accès par rôle ; durée de conservation définie et purge en fin de projet | 3.1 | à concevoir |
| T17 | A, D | Les droits de l'ancien compte restent indéfiniment sur le profil | Phase de nettoyage différée qui retire les droits du compte A après validation ; tracée | 4.5 | à concevoir |

### 5.5 Opérateurs et console (F2, B4)

| # | Cat. | Scénario | Parade exigée | Tranche | État |
|---|---|---|---|---|---|
| T18 | U, E | Accès à l'API sans identité, ou avec un rôle insuffisant | Authentification Entra ID avec MFA sur chaque route ; rôles vérifiés côté serveur ; en production, l'API refuse de démarrer si l'authentification n'est pas configurée | 2.1 | ouvert |
| T19 | E | Un seul compte (M4) prépare et approuve sa propre vague | Le préparateur ne peut pas approuver ; les deux identités figurent dans l'audit ; vérifié côté serveur | 5.2 | à concevoir |
| T20 | I | Un compte compromis lance une vague sur tout le parc | Plafond de postes par vague et de simultanéité ; fenêtre de maintenance imposée ; seuil d'arrêt automatique sur taux d'échec ; arrêt d'urgence | 5.1, 5.2 | à concevoir |
| T21 | A | Attaques web classiques sur la console (script injecté, requête forgée) | Jetons portés en en-tête et non en cookie ; politique de sécurité de contenu ; aucun rendu de HTML issu des données | 2.1, 3.5 | à concevoir |

### 5.6 Audit et données (F5, B5)

| # | Cat. | Scénario | Parade exigée | Tranche | État |
|---|---|---|---|---|---|
| T22 | R | Un opérateur nie une action, ou un administrateur de la base efface ses traces | Événement complet (acteur, poste, tâche, résultat) ; ajout seul ; compte applicatif sans droit de modification ni de suppression ; chaînage par empreinte ; export continu vers le SIEM | 2.3 | ouvert (S5, S7) |
| T23 | A, I | Perte de l'état des vagues au redémarrage du serveur, provoquant des postes à moitié migrés et « oubliés » | Tout l'état en base ; l'agent reste maître de sa propre reprise même sans serveur | 2.4, 4.1 | ouvert (S6) |
| T24 | D | Fuite de la base (comptes, noms de postes) | Chiffrement au repos et en transit ; aucun secret en base ; sauvegardes protégées | 6.4 | à concevoir |

### 5.7 Disponibilité et retour arrière

| # | Cat. | Scénario | Parade exigée | Tranche | État |
|---|---|---|---|---|---|
| T25 | I | Le serveur DomainBridge tombe pendant une vague ; les agents concluent à un échec et reviennent tous en arrière | Le retour automatique se décide sur des **faits locaux** (contrôleur B injoignable, canal sécurisé invalide), jamais sur l'indisponibilité de la console | 4.6 | à concevoir |
| T26 | I | Poste isolé du réseau après redémarrage (certificat réseau de A) | Précontrôle bloquant ; retour automatique local | 3.4, 4.6 | à concevoir |
| T27 | I | Le verrou « laboratoire » est levé par erreur de configuration en production | Les étapes modifiantes exigent à la fois le mode et une liste d'autorisation de postes ; absence de configuration = refus | 4.1 | à concevoir |

### 5.8 Chaîne de fabrication et de livraison (F6, B8)

| # | Cat. | Scénario | Parade exigée | Tranche | État |
|---|---|---|---|---|---|
| T28 | A, E | MSI altéré sur le partage GPO ou dans Intune | Signature de code vérifiée à l'installation ; partage en lecture seule pour les postes ; clé de signature hors dépôt et hors CI ordinaire | 2.6, 6.1 | partiel (script d'installation vérifie la signature) |
| T29 | A | Dépendance ou action de CI compromise | Fichiers de verrouillage et restauration verrouillée ; audit de dépendances ; recherche de secrets ; actions de CI épinglées par empreinte ; inventaire logiciel (SBOM) | 0.3, 6.1 | partiel (actions non épinglées) |
| T30 | E | La console pousse une mise à jour d'agent | Aucune mise à jour par la console : seulement par MSI via Intune ou GPO | 2.5 | acquis par conception, à maintenir |
| T31 | D | Secret introduit dans le dépôt, un paquet ou un journal | Recherche de secrets en CI ; règles `AGENTS.md` ; revue obligatoire des chemins sensibles | 0.3 | en place |

## 6. Exigences de sécurité vérifiables

Chaque exigence doit avoir un test automatique avant le pilote.

1. Aucune route d'API ne répond sans identité valide, hormis `/health`.
2. Un agent ne peut lire ni écrire que ce qui concerne le poste nommé dans son certificat (scénario E10).
3. Le jeton de jonction n'apparaît en clair ni dans la base, ni dans un journal, ni sur disque après application.
4. Le connecteur refuse d'émettre un jeton si le certificat fourni ne correspond pas au compte ordinateur, ou si le poste n'est pas dans une vague approuvée.
5. L'agent ne suit aucun point de réanalyse et refuse tout chemin de profil hors du dossier des profils.
6. Le préparateur d'une vague ne peut pas l'approuver.
7. Chaque action modifiante produit un événement d'audit complet, et le compte applicatif ne peut ni modifier ni supprimer un événement.
8. Rejouer une tâche ne produit aucun effet supplémentaire.
9. Sans configuration explicite, les étapes modifiantes et l'API de production refusent de fonctionner.
10. Le retour automatique ne se déclenche pas quand seule la console est indisponible.

## 7. Écart avec le code actuel

Après la phase 0, le code ne contient **aucune action modifiante**, ce qui borne le risque réel. Restent ouverts, tous prévus en phase 2 :

| Menace | Constat d'audit | Situation |
|---|---|---|
| T1 | S3, S4 | Identité d'agent non liée au certificat ; émetteur non épinglé |
| T2 | — | Pas de procédure d'enrôlement vérifiée |
| T18 | (ex-S1, S2) | Aucune authentification d'opérateur ; les lectures de console ne sont ouvertes qu'en mode développement, que l'installeur de laboratoire utilise sur 127.0.0.1 |
| T22 | S5, S7 | Événement d'audit sans acteur ; immutabilité non appliquée |
| T23 | S6 | État non persistant hors audit et inventaire |
| T29 | — | Actions de CI référencées par étiquette et non par empreinte |

## 8. Risques résiduels acceptés ou à arbitrer

| Risque | Raison | Décision attendue |
|---|---|---|
| Compromission simultanée du serveur et de l'autorité de certification des agents : émission de jetons pour un poste choisi | La parade T7 repose sur l'autorité. Limité par le plafond de débit, l'OU et l'exigence de vague approuvée | Séparer l'autorité du serveur DomainBridge : à trancher avec le choix de PKI (tranche 2.2) |
| Poste compromis par son administrateur local avant migration | Il contrôle déjà le poste ; DomainBridge ne lui donne rien de plus que l'identité de ce seul poste | Accepté |
| Les secrets liés à l'ancien compte ne suivent pas le profil | Conséquence voulue de la règle « aucune migration de secret » | Accepté, à communiquer aux utilisateurs |
| Droit de réinitialisation dans A pour le retour arrière | Décision D3 ; borné à une OU | Accepté ; à retirer dès la fin du projet |
| Fenêtre entre bascule et nettoyage où les deux comptes ont accès au profil | Nécessaire au retour arrière | Durée maximale à fixer (tranche 4.5) |

## 9. Points à relire en priorité par l'équipe sécurité

1. Le mécanisme d'enrôlement et le choix de l'autorité de certification (T2, T7).
2. La conception du jeton de jonction de bout en bout (T6 à T9).
3. Les manipulations de droits en compte système sur un dossier contrôlé par l'utilisateur (T10 à T12).
4. Les règles de déclenchement du retour automatique (T25).
