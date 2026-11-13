# DomainBridge — objectif produit recadré

## Vision

DomainBridge est le cockpit on-premise et hybride qui rend une migration Windows **Domain A → Domain B** pilotable, vérifiable et réversible, tout en utilisant Microsoft Intune comme système de gestion des appareils.

## Problème traité

Lors d'une migration de domaine, les équipes doivent rapprocher les états AD local, Entra ID, Intune, profils Windows et dépendances réseau. Intune déploie et met à jour les postes gérés, mais ne fournit pas à lui seul un workflow de migration de domaine avec précontrôles, orchestration de profils, preuves avant/après et rollback de pilote.

## Utilisateurs

- Administrateur poste de travail : prépare les collections et exécute les vagues approuvées.
- Administrateur AD : délègue les opérations de domaine et valide les prérequis annuaire.
- Responsable migration : suit le risque, les résultats, les incidents et la décision de passage à la vague suivante.

## Capacités à développer

1. **Inventaire hybride de migration** — corrélation en lecture seule des appareils AD, Entra et Intune ; détection des appareils absents, doublons et non gérés.
2. **Précontrôles de poste** — connectivité DNS/DC, appartenance, état du canal sécurisé, profils, BitLocker, espace disque et prérequis applicatifs.
3. **Campagnes de migration** — collections, vagues, fenêtre de maintenance, approbation séparée, état détaillé par appareil et arrêt de vague.
4. **Orchestration de profil** — préparation et suivi d'outils approuvés (par exemple USMT ou ProfWiz sous licence), avec sauvegarde et validation ; jamais de mots de passe ou secrets.
5. **Rollback documenté** — état de retour enregistré avant changement et procédure guidée après un échec de pilote.
6. **Runbooks technicien** — diagnostics AD/DNS/GPO/profil signés, paramétrés, audités et exécutés seulement sur des appareils ciblés.
7. **Preuve et reporting** — preuve avant/après, chronologie, export support/SIEM et tableau de décision de vague.

## Intune : responsabilités conservées

- Enrôlement et conformité MDM.
- Déploiement d'applications Win32, scripts et remédiations.
- Windows Update, Autopatch, anneaux, pilotes et mises à niveau Windows.
- Actions d'appareil usuelles et gestion des politiques.

## Hors périmètre DomainBridge

- Réimplémentation d'Intune, Autopatch, PDQ Deploy ou d'un dépôt général de packages.
- Gestionnaire générique MSI/EXE/WinGet ou orchestrateur général de patching.
- Collecte de mots de passe, secrets LSA, caches d'authentification ou jetons.
- Prise en main à distance généraliste ; l'outil s'intègre aux solutions existantes.

## Mesures de succès du pilote

- 100 % des appareils pilotes ont un précontrôle complet et une preuve avant/après.
- 100 % des changements de migration sont associés à un opérateur, une approbation et un journal d'audit.
- Un échec pilote peut être isolé et faire l'objet d'un rollback documenté sans bloquer une vague ultérieure.
- Les appareils détectés hors Intune sont identifiés et orientés vers un processus d'enrôlement, sans déploiement parallèle.
