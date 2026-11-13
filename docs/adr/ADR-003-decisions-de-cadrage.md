# ADR-003 — Décisions de cadrage D1 à D7

- Statut : accepté
- Date : 2026-11-13
- Décideur : responsable du projet DomainBridge
- Portée : réponses aux décisions ouvertes par `docs/audit/2026-11-13-audit-domainbridge.md`, section 6.

## Décisions

| # | Sujet | Décision | Conséquence pour la conception |
|---|---|---|---|
| D1 | Réseau et approbation | Les postes de A voient les contrôleurs de domaine B ; une relation d'approbation A ↔ B existe | La première ouverture de session dans B est possible sur site. Les comptes de B peuvent être résolus depuis un poste encore dans A. Le précontrôle vérifie malgré tout la visibilité poste par poste |
| D2 | Nom des postes | Même nom dans A et dans B | Pas de renommage pendant la bascule. Le jeton de jonction réutilise le compte homonyme de B. La colonne `poste_cible` de la liste est retirée |
| D3 | Retour arrière | Droit délégué dans A : réinitialisation des comptes ordinateurs d'une OU précise | Retour arrière automatique par l'agent après bascule. Le connecteur source a une écriture, limitée à cette seule opération, sous gMSA |
| D4 | Console | Console web unique | `DomainBridge.Desktop` est gelé puis retiré de la solution et de l'installeur |
| D5 | Profil | Moteur natif dans l'agent | Fondé sur les API Windows documentées. Aucun composant tiers, aucune rétro-ingénierie. Toute la charge de test est interne |
| D6 | Volume et versions | 200 à 1 000 postes ; Windows 10 et Windows 11 | Vagues de quelques dizaines de postes. Matrice de laboratoire : une image Windows 10 22H2 et une image Windows 11 de la version majoritaire du parc. Toute version plus ancienne est refusée au précontrôle |
| D7 | Hébergement | Serveur interne | API, base et console sur un serveur Windows du réseau interne, joignable en HTTPS par les postes. Un poste nomade doit être sur le VPN pour que son agent communique |

## Points restant ouverts

| Point | Quand il sera tranché |
|---|---|
| Autorité de certification des agents (dédiée DomainBridge, PKI interne existante, ou profil de certificat Intune) | Tranche 2.2, avec l'équipe sécurité |
| Part réelle de postes hybrides Entra, de postes nomades et de postes partagés | Mesurée par la phase 3 (lecture seule) sur le parc réel |
| Version exacte de Windows 11 à mettre dans le laboratoire | Mesurée par la phase 3 |
| OU de A et de B sur lesquelles porter les délégations | Tranche 0.1, avec l'administrateur AD |

## Alternatives écartées

- D3 — retour manuel sans écriture dans A : supprime le filet de sécurité automatique après bascule.
- D4 — client de bureau, ou deux consoles : mono-opérateur, ou double effort de développement et de test.
- D5 — ProfWiz (licence, boîte noire) ; USMT (copie complète, lent, double l'espace disque).
- D7 — hébergement cloud : couvrirait les nomades hors VPN, non retenu.
