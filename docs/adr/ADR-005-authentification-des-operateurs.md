# ADR-005 — Authentification des opérateurs par session Windows et groupes AD

- Statut : accepté
- Date : 2026-11-13
- Décideur : responsable du projet DomainBridge
- Remplace : l'authentification Entra ID avec MFA prévue par la base de sécurité et l'ADR-003 (D4).

## Contexte

Le produit s'installe sur le poste de l'administrateur (ADR-004). Entra ID exigeait un administrateur du locataire pour enregistrer une application, un accès Internet à chaque connexion et une licence d'accès conditionnel pour le MFA.

## Décision

1. Les opérateurs s'authentifient avec leur **session Windows** (authentification intégrée Negotiate : Kerberos, à défaut NTLM). La console ne demande aucune saisie.
2. Les rôles viennent de l'appartenance à des **groupes Active Directory**, désignés par leur identifiant de sécurité (SID), jamais par leur nom.
3. Quatre rôles : Préparateur, Approbateur, Technicien, Lecteur. Tout rôle donne la lecture.
4. Hors développement et test, l'API **refuse de démarrer** si aucun groupe n'est configuré ou si une valeur n'est pas un SID.
5. Les routes des agents ne dépendent pas de l'identité d'un opérateur : elles reposent sur le certificat du poste (tranche 2.2).

## Règles d'exploitation

- Seuls des **comptes d'administration dédiés**, distincts des comptes de tous les jours, sont membres des groupes.
- Le préparateur et l'approbateur d'une vague sont **deux comptes différents** ; l'API le vérifiera elle-même (tranche 5.2).
- Pendant la migration, lister les groupes du domaine A **et** ceux du domaine B : le poste de l'administrateur et les comptes changent de domaine.

## Conséquences

- Pas de MFA propre à l'outil. Il n'existe que si l'ouverture de session Windows l'impose déjà (Windows Hello Entreprise, carte à puce).
- L'authentification est ambiante : le navigateur présente la session sans action de l'utilisateur. Toute route modifiante devra donc se protéger contre les requêtes forgées depuis un autre site (menace T21) ; aucune n'existe encore.
- L'autorisation repose sur des rôles et non sur le mode de connexion : passer plus tard à Entra ID ne changerait que la couche d'authentification.

## Alternative écartée

Entra ID avec MFA : dépendances externes et travail supplémentaire jugés disproportionnés pour un outil hébergé sur un poste d'administrateur du réseau interne. À reconsidérer si la console doit être accessible hors du réseau interne, ou si la politique de sécurité impose un MFA par outil.
