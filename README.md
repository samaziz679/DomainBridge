# DomainBridge

Cockpit sécurisé de migration et de conformité hybride pour les postes Windows entre un domaine source et un domaine cible.

## Objectif du MVP

DomainBridge orchestre, audite et contrôle la migration de postes Windows du **domaine source A** vers le **domaine cible B**, sans stocker de mots de passe d'administrateur sur les postes.

Microsoft Intune demeure le système de référence pour le MDM, les applications Win32, les scripts, les remédiations, les mises à jour Windows et les politiques de conformité. DomainBridge complète Intune avec le pilotage de migration AD, les précontrôles hybrides et la preuve d'exécution par poste.

## Périmètre produit

- Découvrir et corréler les états AD local, Microsoft Entra ID et Intune.
- Préparer, approuver, exécuter par vagues et valider une migration Domain A → Domain B.
- Orchestrer les prérequis, la migration de profils avec des outils approuvés et le rollback documenté.
- Identifier les postes non gérés ou non conformes et les orienter vers l'enrôlement Intune.
- Centraliser les runbooks technicien AD/DNS/GPO/profils et leur audit.

DomainBridge ne remplace ni Intune, ni Windows Autopatch, ni un outil généraliste de prise en main à distance.

## Principes de sécurité

- Agent Windows à connexion sortante HTTPS avec authentification mutuelle par certificat.
- Aucun secret de domaine ou mot de passe utilisateur dans le dépôt, les scripts ou les paquets de déploiement.
- Opérations de migration approuvées, traçables et réversibles autant que possible.
- Déploiement de l'agent par Microsoft Intune et par stratégie de groupe (GPO).

## Phases

1. Fondations, architecture et sécurité.
2. Console centrale et journal d'audit.
3. Agent Windows en lecture seule et inventaire.
4. Connecteurs de lecture Intune, AD local et Entra ID.
5. Migration contrôlée en laboratoire, puis pilote.

> Le dépôt ne contient pas encore de moteur de migration. Les opérations sur les domaines et les profils ne seront développées qu'après validation des contrôles de sécurité et de rollback.
