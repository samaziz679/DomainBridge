# DomainBridge

Plateforme centralisée de migration de postes Windows et de leurs profils d'un **domaine source A** vers un **domaine cible B**.

## Ce que fait le produit

DomainBridge reçoit une liste de postes avec leur dernier utilisateur connecté, la rapproche de la réalité observée sur chaque poste, puis bascule les postes vers le domaine B et rattache les profils locaux existants aux comptes déjà migrés — par vagues approuvées, avec preuve avant/après et retour arrière.

Les objets d'annuaire (utilisateurs, comptes ordinateurs, groupes, GPO) sont supposés **déjà migrés** dans B. DomainBridge ne les crée ni ne les modifie.

Périmètre complet, exceptions et limites connues : [docs/PRODUCT_BRIEF.md](docs/PRODUCT_BRIEF.md).

## État actuel

Le dépôt contient le socle de contrôle : enrôlement et inventaire technique des agents, journal d'audit, console web en lecture seule, installeur de laboratoire. **Il ne contient pas encore de moteur de migration** : aucune opération ne touche à un domaine ou à un profil.

| Document | Contenu |
|---|---|
| [docs/audit/2026-11-13-audit-domainbridge.md](docs/audit/2026-11-13-audit-domainbridge.md) | Audit de l'existant et constats de sécurité ouverts |
| [docs/DEVELOPMENT_PLAN.md](docs/DEVELOPMENT_PLAN.md) | Plan de développement par phases |
| [docs/adr/](docs/adr/) | Décisions d'architecture |
| [docs/security/THREAT_MODEL.md](docs/security/THREAT_MODEL.md) | Modèle de menace et exigences de sécurité vérifiables |
| [GATES.md](GATES.md) | Jalons et preuves |
| [AGENTS.md](AGENTS.md) | Règles de sécurité obligatoires |

## Composants

| Dossier | Rôle |
|---|---|
| `src/DomainBridge.Api` | API centrale et journal d'audit |
| `src/DomainBridge.Agent` | Agent Windows (aujourd'hui : inventaire à la demande) |
| `src/DomainBridge.Contracts` | Contrats partagés API / agent |
| `src/DomainBridge.DirectoryConnector` | Connecteur d'annuaire (aujourd'hui : simulation) |
| `apps/DomainBridge.Console` | Console web des opérateurs |
| `installer/TestLab` | Installeur MSI de laboratoire, non signé |
| `deployment/` | Scripts de paquet Intune et de déploiement GPO |

## Vérifier

Une seule commande, identique en local et en CI :

```powershell
./scripts/verify.ps1
```

Elle enchaîne restauration verrouillée, formatage, construction avec analyseurs, tests avec seuil de couverture, puis tests, construction et audit de dépendances de la console. Prérequis : SDK .NET indiqué dans `global.json`, Node.js 24.

## Principes de sécurité

- Agent à connexion sortante HTTPS avec authentification mutuelle par certificat ; aucun canal entrant vers les postes.
- Aucun mot de passe, secret de domaine ou jeton de jonction dans le dépôt, les scripts, les journaux ou les paquets de déploiement.
- Toute action destructive exige une approbation de campagne, une clé d'idempotence, un événement d'audit et un chemin de retour testé.
- Intune et la GPO livrent l'agent ; la console pilote la migration.
