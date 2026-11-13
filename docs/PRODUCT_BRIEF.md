# DomainBridge — objectif produit recadré

## Vision

DomainBridge est le cockpit centralisé qui rend une migration Windows **Domain A → Domain B** pilotable, vérifiable et réversible, tout en utilisant Microsoft Intune comme système de gestion des appareils.

## Problème traité

Lors d'une migration de domaine, les équipes doivent faire passer les postes Windows et leurs profils locaux du domaine A vers le domaine B après la migration des objets annuaire. Intune déploie et met à jour les postes gérés, mais ne fournit pas à lui seul un workflow de migration de domaine avec précontrôles, orchestration de profils, preuves avant/après et rollback de pilote.

## Hypothèses et frontière de migration

- Les objets Active Directory sont déjà présents dans le domaine B avant toute campagne : utilisateurs, comptes ordinateurs, groupes de sécurité, unités d'organisation et GPO nécessaires.
- DomainBridge reçoit une **liste centralisée de postes à migrer**, avec au minimum l'identité du poste source, l'identité attendue dans le domaine B et le dernier utilisateur interactif connu.
- Le dernier utilisateur connecté sert à identifier le **profil principal candidat à migrer**. Les postes partagés, les profils multiples, les profils temporaires et les comptes de service sont signalés comme exceptions à valider par un technicien.
- Le périmètre d'exécution est limité au **poste Windows** : précontrôle, sortie/entrée de domaine selon le runbook approuvé, association du profil local au compte déjà migré, redémarrage, validation et rollback documenté.
- La création, la migration ou la modification d'objets AD, d'utilisateurs, de groupes et de GPO reste hors périmètre DomainBridge.

## Utilisateurs

- Administrateur poste de travail : prépare les collections et exécute les vagues approuvées.
- Administrateur AD : délègue les opérations de domaine et valide les prérequis annuaire.
- Responsable migration : suit le risque, les résultats, les incidents et la décision de passage à la vague suivante.

## Capacités à développer

1. **Import et réconciliation de liste** — importer la liste centralisée, retrouver le poste, le compte ordinateur cible et le dernier utilisateur connu ; mettre en quarantaine les ambiguïtés.
2. **Précontrôles de poste** — connectivité DNS/DC, appartenance, canal sécurisé, profil candidat, BitLocker, espace disque et prérequis applicatifs.
3. **Campagnes de migration** — vagues, fenêtre de maintenance, approbation séparée, état détaillé par appareil et arrêt de vague.
4. **Orchestration de profil** — préparation et suivi d'outils approuvés (par exemple USMT ou ProfWiz sous licence) pour rattacher le profil local au compte déjà migré ; jamais de mots de passe ou secrets.
5. **Validation Domain B** — confirmer l'appartenance au domaine cible, la résolution DNS, la connexion du compte attendu et l'intégrité du profil après redémarrage.
6. **Rollback documenté** — état de retour enregistré avant changement et procédure guidée après un échec de pilote.
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

- 100 % des enregistrements de la liste pilote sont réconciliés ou explicitement placés en exception.
- 100 % des appareils pilotes ont un précontrôle complet et une preuve avant/après.
- 100 % des changements de migration sont associés à un opérateur, une approbation et un journal d'audit.
- Un échec pilote peut être isolé et faire l'objet d'un rollback documenté sans bloquer une vague ultérieure.
- Le profil principal désigné est validé après rattachement au compte déjà migré dans le domaine B.

## Références locales de contexte

- **ProfWiz / ForensiT** — `C:\Users\Admin\Documents\Claude\Projects\REA\Profwiz.exe` : référence d'orchestration de profil. Son exécutable, sa licence et ses mécanismes internes ne sont ni distribués ni reproduits par DomainBridge.
- **Admin Arsenal / PDQ Deploy et PDQ Inventory** — `C:\Users\Admin\Desktop\Admin Arsenal.zip` : référence fonctionnelle pour l'expérience opérateur (inventaire, cible, statut et historique). Aucun code ou composant propriétaire n'est repris.
