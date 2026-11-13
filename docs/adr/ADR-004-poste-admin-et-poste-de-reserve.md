# ADR-004 — Hébergement sur le poste de l'administrateur et essais sur poste de réserve

- Statut : accepté
- Date : 2026-11-13
- Décideur : responsable du projet DomainBridge
- Remplace : la décision D7 de l'ADR-003 (serveur interne) et le laboratoire à deux forêts du plan de développement.

## Contexte

Le laboratoire à deux forêts prévu au plan exige un hôte Hyper-V avec au moins 7 Go de mémoire libre et environ 16 Go d'images Windows à télécharger. Le poste de développement ne le permet pas, et aucune autre machine n'est disponible. Le responsable du projet a décidé de ne pas construire ce laboratoire.

## Décisions

### 1. Le produit s'installe sur le poste de l'administrateur

Un installeur unique dépose sur le poste de l'administrateur l'API, le stockage et la console web. Les décisions antérieures restent valables : console web unique (D4), agent sur chaque poste à migrer livré par Intune ou GPO (ADR-002), connexions initiées par l'agent en HTTPS avec authentification mutuelle.

Seul l'hébergement change : le poste de l'administrateur joue le rôle du serveur.

### 2. Les essais se font sur un poste de réserve réel

La faisabilité (phase 1) puis le moteur (phase 4) sont éprouvés sur une machine dédiée, membre du vrai domaine A, sans utilisateur réel et réinstallable, avec des comptes fictifs créés pour l'occasion dans A et dans B.

## Conséquences

### Sur le poste de l'administrateur

- Il écoute en HTTPS : une règle de pare-feu **entrante sur ce poste** est nécessaire, depuis les postes à migrer. Aucun flux entrant n'est ouvert sur les postes migrés ; la règle de sécurité correspondante est respectée.
- Il doit rester allumé et joignable pendant une vague. S'il s'arrête, les agents poursuivent ou annulent seuls leur étape en cours (menace T25) et rendent compte à son retour.
- Il porte un certificat serveur et, à terme, l'accès aux connecteurs d'annuaire. Sa compromission équivaut à celle du serveur (acteur M5 du modèle de menace) : chiffrement du disque, session verrouillée, compte d'administration dédié.
- Il migre lui-même **en dernier**, hors vague automatique.
- Stockage *(confirmé par l'ADR-006)* : une base embarquée dans l'installeur plutôt que PostgreSQL, trop lourd à installer et à exploiter sur un poste de travail pour 200 à 1 000 postes. L'abstraction de stockage existante est conservée ; PostgreSQL reste possible pour un hébergement sur serveur.

### Sur les essais

- La règle « domaines de laboratoire uniquement » d'`AGENTS.md` est remplacée : les essais peuvent utiliser les vrais domaines A et B, **uniquement** dans une OU de test dédiée, sur un poste de réserve désigné, avec des identités fictives.
- Le verrou logiciel prévu en phase 4 change de nature : les étapes modifiantes ne s'exécutent que sur un poste dont le nom figure dans une liste d'autorisation, et dont le compte ordinateur est dans l'OU de test.
- **Il n'y a plus d'essais de bout en bout automatiques chaque nuit.** Un poste physique ne se remet pas à zéro en quelques secondes. Les scénarios E1 à E12 deviennent une campagne d'essais lancée à la demande, avec remise en état du poste entre deux scénarios.
- Les scénarios destructifs (coupure à chaque étape, E5) sont plus lents à rejouer et seront joués moins souvent.
- Windows 10 et Windows 11 exigent chacun leur poste de réserve, ou une réinstallation entre les deux.

## Risques acceptés

| Risque | Parade |
|---|---|
| Une erreur d'essai touche les vrais annuaires | Délégations limitées à l'OU de test dans A et dans B ; comptes fictifs ; aucun droit hors de cette OU |
| Le poste de réserve devient inutilisable | Machine sans donnée ni utilisateur réel ; image disque ou réinstallation prévue avant le premier essai |
| Régression non détectée faute d'essais nocturnes | Tests unitaires et d'intégration sur chaque demande de fusion ; campagne complète obligatoire avant chaque version et avant le pilote |
| Une politique de groupe réelle de B s'applique au poste de test | OU de test avec héritage maîtrisé, décidé avec l'administrateur AD |

## Alternatives écartées

- Laboratoire local Hyper-V : mémoire insuffisante et volume de téléchargement refusé.
- Laboratoire dans Azure : non retenu par le responsable du projet.
- Outil sans agent, connecté à distance aux postes : exige un accès entrant et un compte administrateur sur les postes, contraire à `AGENTS.md`.
- Exécutable lancé manuellement sur chaque poste : supprime le pilotage centralisé et les vagues.
- Essai direct sur le poste d'un vrai utilisateur : risque de perte d'accès au poste ou au profil tant que la jonction et le retour arrière ne sont pas éprouvés.

## Prérequis à réunir avant le premier essai

À préparer avec l'administrateur AD :

1. Un poste de réserve Windows 11, membre de A, sans utilisateur réel, avec une image ou un moyen de réinstallation.
2. Une OU de test dans A contenant ce poste, et une OU de test dans B.
3. Le compte ordinateur de ce poste **déjà créé** dans l'OU de test de B, sous le même nom.
4. Deux ou trois comptes utilisateurs fictifs dans A et leurs équivalents dans B, dont un avec un login différent.
5. Un compte d'essai avec, pour seul droit, la réinitialisation des comptes ordinateurs de l'OU de test de B, et de celle de A pour le retour arrière.
6. Un compte administrateur local du poste de réserve, pour reprendre la main si aucun domaine ne répond.
