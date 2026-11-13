# Jalons DomainBridge

Un jalon n'est coché qu'avec une commande rejouable et une preuve datée. Plan de référence : `docs/DEVELOPMENT_PLAN.md`.

## Phase 0 — Cadrage et recentrage

- [x] **0.1 — Décisions de cadrage D1 à D7 prises.**
  PREUVE : 2026-11-13 — `docs/adr/ADR-003-decisions-de-cadrage.md`, `docs/adr/ADR-002-distribution-agent.md`.
  RESTE : OU de délégation dans A et B.

- [x] **0.2 — Hors-périmètre retiré** (catalogue de remédiations, actions technicien, campagnes de maintenance, PKI d'éditeur, client de bureau).
  CHECK : `dotnet test DomainBridge.sln -c Release --filter OutOfScopeRouteTests`
  PREUVE : 2026-11-13 — 4 routes retirées répondent 404 ; 50 tests .NET et 4 tests console verts ; `DomainBridge-TestLab-0.3.0.msi` construit sans client de bureau.

- [x] **0.3 — Socle de vérification et .NET 10 LTS.**
  CHECK : `./scripts/verify.ps1`
  PREUVE : 2026-11-13 — `VERIFY OK` sur .NET 10 (SDK 10.0.404) : format, analyseurs en erreurs, 50 tests, couverture de lignes 96 % pour un seuil de 80 %, console et audit de dépendances verts ; `DomainBridge-TestLab-0.3.0.msi` reconstruit.
  RESTE : exécution de la CI sur GitHub non encore observée (rien n'est poussé).

- [ ] **0.4 — Modèle de menace écrit et relu.**
  PREUVE PARTIELLE : 2026-11-13 — `docs/security/THREAT_MODEL.md` rédigé (31 menaces, 10 exigences vérifiables).
  RESTE : relecture et validation par l'équipe sécurité ; le jalon n'est coché qu'après cette relecture.

## Écarts connus, à ne pas perdre de vue

- Les magasins PostgreSQL (`PostgresAuditStore`, `PostgresInventoryStore`) ne sont couverts par aucun test : exclus du seuil de couverture jusqu'aux tests d'intégration de la tranche 2.4.
- Les constats de sécurité S3 à S7 de l'audit restent ouverts (phase 2). S1 et S2 ont disparu avec le code retiré, mais l'API n'a toujours aucune authentification d'opérateur.

## Phase 1 — Preuves de faisabilité sur poste de réserve

Décision : `docs/adr/ADR-004-poste-admin-et-poste-de-reserve.md`. Protocole : `docs/lab/PHASE1_FAISABILITE.md`.

- [ ] **1.1 — Banc d'essai prêt** (poste de réserve, OU de test dans A et B, comptes fictifs, délégations).
  ÉTAT : 2026-11-13 — à réunir avec l'administrateur AD ; rien n'est encore en place.

- [ ] **1.2 — Jonction hors ligne de A vers B.**
  CHECK : `lab/spikes/New-SpikeJoinToken.ps1` puis `lab/spikes/Invoke-SpikeJoinOnSpare.ps1`
  ÉTAT : scripts écrits, syntaxe vérifiée, garde-fou de nom de machine vérifié ; **jamais exécutés sur un domaine**.

- [ ] **1.3 — Rattachement du profil sur place.** Protocole écrit ; script à écrire au vu des résultats de 1.2.

- [ ] **1.4 — Retour dans A.** Protocole écrit.

## Phase 2 — Socle de confiance

- [x] **2.1 — Authentification et rôles des opérateurs** (ADR-005, menace T18).
  CHECK : `dotnet test DomainBridge.sln -c Release --filter "OperatorAccess"`
  PREUVE : 2026-11-13 — 15 tests : routes de console refusées sans identité (401) ou sans groupe (403), rôles issus des seuls groupes configurés, rôle fourni par l'appelant ignoré, routes d'agent indépendantes, démarrage refusé en production sans groupe. Essai réel de l'authentification Windows sur l'API lancée localement : 401 avec défi Negotiate sans identité, 200 et rôle Lecteur avec la session Windows.
  RESTE : essai Kerberos entre deux machines d'un domaine (seul NTLM local a été éprouvé) ; protection anti-falsification, à poser avec la première route modifiante.

- [x] **2.2 — Identité d'agent liée au certificat** (constats S3 et S4, menace T1).
  CHECK : `dotnet test DomainBridge.sln -c Release --filter "AgentIdentity"`
  PREUVE : 2026-11-13 — 15 tests : identité lue dans le certificat, autorité épinglée, certificat auto-signé refusé, usurpation d'un autre poste refusée en 403 et tracée, démarrage refusé en production sans autorité épinglée.
  RESTE : procédure d'enrôlement (menace T2) et contrôle de révocation.

- [ ] **2.3 — Audit complet et chaîné** (constats S5 et S7, menace T22).
  CHECK : `dotnet test DomainBridge.sln -c Release --filter "AuditChain"`
  PREUVE PARTIELLE : 2026-11-13 — acteur, résultat et tâche dans chaque événement ; chaînage par empreinte ; modification, suppression et réordonnancement détectés (9 tests).
  RESTE : la migration `deployment/postgres/003_audit_chain.sql` et le magasin PostgreSQL ne sont **pas exécutés** (aucune base disponible) ; export SIEM ; acteur humain, qui dépend de 2.1.

- [ ] **2.4 — Persistance de tout l'état.** Non commencé : choix du stockage à confirmer (ADR-004).
- [ ] **2.5 — Agent en service Windows.** Non commencé.
- [ ] **2.6 — MSI unique signé.** Non commencé.
