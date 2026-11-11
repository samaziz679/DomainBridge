# DomainBridge

Plateforme sécurisée de migration de postes et de profils Windows entre un domaine source et un domaine cible.

## Objectif du MVP

DomainBridge orchestre, audite et contrôle la migration de postes Windows du **domaine source A** vers le **domaine cible B**, sans stocker de mots de passe d'administrateur sur les postes.

## Principes de sécurité

- Agent Windows à connexion sortante HTTPS avec authentification mutuelle par certificat.
- Aucun secret de domaine ou mot de passe utilisateur dans le dépôt, les scripts ou les paquets de déploiement.
- Opérations de migration approuvées, traçables et réversibles autant que possible.
- Déploiement de l'agent par Microsoft Intune et par stratégie de groupe (GPO).

## Phases

1. Fondations, architecture et sécurité.
2. Console centrale et journal d'audit.
3. Agent Windows en lecture seule et inventaire.
4. Paquets de déploiement Intune et GPO.
5. Migration contrôlée en laboratoire, puis pilote.

> Le dépôt ne contient pas encore de moteur de migration. Les opérations sur les domaines et les profils ne seront développées qu'après validation des contrôles de sécurité et de rollback.
