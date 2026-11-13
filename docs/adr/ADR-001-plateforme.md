# ADR-001 — Stack applicative et limites du MVP

- Statut : accepté
- Date : 2026-11-11
- Décideur : équipe DomainBridge
- Portée : console centrale, agent Windows et connecteur annuaire pour une migration d’un domaine source A vers un domaine cible B.

## Contexte

DomainBridge doit orchestrer, auditer et contrôler une migration de poste et de profil Windows, sans exposer de mot de passe d’administrateur sur les points de terminaison. Les agents doivent fonctionner derrière les pare-feux usuels et être déployables à la fois avec Intune et par GPO.

Le produit débute par un MVP non destructif : découverte locale, enrôlement mutuel, planification, approbation et journalisation. Il n’exécute ni jointure de domaine, ni remappage de profil, ni migration de secrets.

## Décision

| Élément | Choix |
|---|---|
| API centrale | ASP.NET Core sur .NET 10 LTS |
| Console opérateur | React et TypeScript |
| Agent Windows | Service Worker .NET 10, code signé |
| Connecteur annuaire | Service .NET 10 installé sur un serveur d’administration dédié |
| Base d’audit | PostgreSQL |
| Identité du connecteur | gMSA ou identité managée avec délégation minimale |
| Communication | HTTPS sortant de l’agent, mTLS, jetons courts liés à l’appareil |
| Déploiement agent | Paquet Win32 Intune et MSI assignable par GPO |
| Journalisation | Événements immuables, corrélation par opération, export SIEM |

## Conséquences

- Le navigateur n’accède jamais directement à Active Directory ; l’API applique autorisations, approbations et journalisation.
- L’agent initie les connexions sortantes. Le MVP n’ouvre pas WinRM, RDP, SMB ou RPC pour le pilotage.
- Les secrets ne figurent ni dans l’agent, ni dans les journaux, ni dans les paquets Intune/GPO. Les certificats et jetons ont une durée de vie limitée et sont révocables.
- La console web demeure séparée du backend et ne porte aucun droit annuaire.
- PostgreSQL conserve les états de campagne, l’inventaire, les décisions et les événements d’audit ; il ne conserve aucun mot de passe.

## Alternative écartée

Un agent PowerShell autonome aurait accéléré un premier prototype, mais rend plus difficile la signature, la mise à jour, les contrats versionnés, les tests et le contrôle de privilèges. PowerShell reste acceptable pour les scripts d’installation ou de détection, mais pas comme moteur d’orchestration.

## MVP-01 : limites explicites

Autorisé :
- inventaire local en lecture seule ;
- enrôlement par certificat et mTLS ;
- remontée d’état, journal d’audit et approbations ;
- simulation d’une campagne dans un laboratoire avec domaines synthétiques A et B.

Interdit :
- extraction, stockage, transport ou migration de mots de passe, caches d’authentification, secrets LSA ou jetons ;
- action automatique de jointure/sortie de domaine ;
- changement de profil utilisateur ;
- exécution de scripts arbitraires depuis la console.

## Critères de sortie vers le MVP opérationnel

1. Modèle de menace et flux mTLS validés.
2. Identités synthétiques et laboratoire A/B documentés.
3. Agent signé, installable via Intune et GPO.
4. API, agent et connecteur testés avec couverture mesurée.
5. Une campagne exige une approbation humaine et produit une trace d’audit complète.

## Révision du 2026-11-13

- Socle porté de .NET 8 à **.NET 10 LTS** : .NET 8 est sorti du support le 10 novembre 2026.
- La console opérateur est une application web unique ; le client de bureau a été retiré (ADR-003, D4).
- Le catalogue de remédiations et les actions technicien ont été retirés du produit (périmètre v2).
