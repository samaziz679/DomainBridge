# DomainBridge — périmètre produit v2

- Version : 2.0 — 2026-11-13 (remplace « objectif produit recadré »)
- Statut : **validé** le 2026-11-13. Décisions de cadrage : `docs/adr/ADR-003-decisions-de-cadrage.md`.

## Vision

DomainBridge bascule, de manière centralisée, des postes Windows du **domaine A** vers le **domaine B** et rattache les profils locaux existants aux comptes déjà migrés, par vagues approuvées, avec preuve avant/après et retour arrière.

## Hypothèse d'entrée

La migration des objets d'annuaire est **terminée avant toute campagne** : utilisateurs, comptes ordinateurs, groupes de sécurité, OU et GPO existent dans B. DomainBridge ne crée, ne migre et ne modifie aucun de ces objets, à une exception près : la réinitialisation du compte ordinateur cible nécessaire à la jonction et celle du compte ordinateur source pour un retour arrière, par droit délégué sur une OU précise de A.

## Entrée : la liste de migration

Fichier CSV importé dans la console, une ligne par poste :

| Colonne | Obligatoire | Rôle |
|---|---|---|
| `poste` | oui | Nom du poste, identique dans A et dans B |
| `dernier_utilisateur` | oui | Compte A du dernier utilisateur connecté (`A\login`) : profil principal candidat |
| `utilisateur_cible` | non | Compte B si le login change ; par défaut même login |
| `vague` | non | Affectation à une vague |

La liste est un **point de départ, pas une vérité** : la console la rapproche de ce que l'agent observe réellement sur le poste (profils présents, dernière utilisation, taille) et de ce que les connecteurs trouvent dans A et B. Tout écart place le poste en **quarantaine** jusqu'à décision d'un technicien.

## Dans le périmètre

### 1. Import et rapprochement
- Import de la liste, contrôle de format, détection des doublons.
- Résolution de chaque compte A et B (existence, compte actif, identifiant de sécurité) par les connecteurs.
- Confrontation avec l'inventaire de l'agent ; mise en quarantaine des ambiguïtés.

### 2. Inventaire de migration (agent, lecture seule)
- Appartenance au domaine, état du canal sécurisé, version de Windows.
- Profils locaux : compte, taille, dernière utilisation, état chargé. Aucune lecture du contenu des profils.
- Signaux de risque : BitLocker, fichiers EFS, 802.1X/certificat machine, jonction hybride Entra, espace disque, comptes de A présents dans les groupes locaux, tâches planifiées et services sous compte de A, session ouverte.

### 3. Précontrôles et simulation
- Verdict par poste : **prêt**, **prêt avec réserves**, **bloqué**, avec motif normalisé.
- Visibilité d'un contrôleur de domaine B et résolution DNS depuis le poste (acquise en principe, vérifiée poste par poste).
- Simulation complète sans modification, obligatoire avant toute vague réelle.

### 4. Campagnes et vagues
- Vagues, fenêtre de maintenance, plafond de postes simultanés, arrêt d'urgence d'une vague.
- Double approbation : la personne qui prépare n'est pas celle qui approuve.
- Seuil d'arrêt automatique si le taux d'échec d'une vague dépasse une limite configurée.

### 5. Migration du poste (trois phases)
1. **Préparer** — réversible, en arrière-plan, sans interruption de l'utilisateur : capture de l'état de retour, ajout des droits du compte B sur le dossier et le registre du profil. Rien n'est retiré.
2. **Basculer** — en fenêtre de maintenance, un redémarrage : jonction au domaine B par jeton de jonction hors ligne chiffré pour ce poste, déclaration du profil pour le compte B, correction des groupes locaux.
3. **Valider** — après redémarrage : appartenance à B, canal sécurisé, profil rattaché, sauvegarde de la clé BitLocker dans B, réenregistrement des applications modernes à la première ouverture de session.

### 6. Profils
- Profil principal désigné par la liste ; profils supplémentaires sur décision explicite (postes partagés).
- Rattachement sur place, sans copie de données.
- Correspondance de comptes A → B explicite quand les logins diffèrent.

### 7. Retour arrière
- Avant bascule : annulation complète et automatique.
- Après bascule : retour dans A piloté par l'agent si la validation échoue dans le délai configuré, ou sur demande d'un opérateur.
- Chaque étape destructive a un chemin de retour testé en laboratoire avant d'être autorisée en pilote.

### 8. Preuve et reporting
- Journal d'audit : qui, quoi, quel poste, quelle tâche, quel résultat, quand.
- Fiche de preuve par poste (état avant, étapes, état après), tableau de bord de vague, export CSV et SIEM.

### 9. Distribution de l'agent
Intune et la GPO **livrent** l'agent ; la console **pilote** la migration. Aucune étape de migration n'est exécutée par un script Intune ou GPO (voir `docs/adr/ADR-002-distribution-agent.md`).

| Situation du poste | Voie de livraison de l'agent |
|---|---|
| Géré par Intune (majorité du parc) | Application Win32 Intune : le MSI emballé, installation en contexte système, règle de détection sur la version |
| Membre de A, hors Intune | GPO : même MSI, installation assignée à l'ordinateur ou script de démarrage signé, ciblée sur les OU concernées |
| Nomade ne recevant plus les GPO | Installation manuelle du même MSI par un technicien, ou classement en exception |

- Un seul service Windows signé, un seul MSI, mêmes règles de version, de détection et de mise à jour quelle que soit la voie.
- La console compare la liste de migration aux agents enrôlés et signale tout poste sans agent : aucun poste n'est oublié en silence, et aucun ne peut entrer dans une vague sans agent actif.
- Le canal de l'agent (HTTPS sortant, certificat propre) ne dépend ni d'Intune ni du domaine : il reste disponible pendant et après la bascule, y compris si le poste change d'identité d'appareil dans Intune.
- Désinstallation automatique de l'agent en fin de projet, par la même voie que l'installation.

## Classes d'exception (traitées, mais jamais automatiquement)

| Classe | Traitement |
|---|---|
| Poste partagé / plusieurs profils actifs | Choix explicite des profils par un technicien |
| Poste nomade sans accès à un contrôleur de domaine B | Reporté jusqu'à présence sur site ou VPN avant ouverture de session |
| Fichiers EFS détectés | Bloqué : déchiffrement par l'utilisateur avant migration |
| Réseau dépendant d'un certificat machine de A (802.1X) | Bloqué tant que l'équivalent B n'est pas en place |
| Poste hybride Entra / géré par Intune | Vague dédiée, après validation en laboratoire du changement d'identité d'appareil |
| Dernier utilisateur = compte d'administration ou de service | Quarantaine |
| Serveurs, contrôleurs de domaine, Windows antérieur à Windows 10 | Refusés |

## Limites connues, à communiquer aux utilisateurs

Ne suivent **pas** le profil, par conception (ce sont des secrets liés à l'ancien compte) :
- mots de passe enregistrés dans les navigateurs et le Gestionnaire d'identifiants ;
- certificats personnels avec clé privée ;
- sessions ouvertes des applications (OneDrive, Teams, Outlook) : une reconnexion est demandée.

## Hors périmètre

- Création ou migration d'utilisateurs, groupes, comptes ordinateurs, GPO, sIDHistory.
- Migration de mots de passe, de secrets LSA, de caches d'authentification, de jetons.
- Migration de serveurs, de boîtes aux lettres, de données de serveurs de fichiers et de leurs droits.
- Catalogue de remédiations, actions technicien génériques, déploiement de logiciels, gestion des mises à jour, prise en main à distance : **le code existant de ce type est gelé** et sera retiré.
- Remplacement d'Intune, d'Autopatch ou d'un outil d'inventaire.
- Exécution de scripts arbitraires depuis la console.

## Utilisateurs et rôles

| Rôle | Peut |
|---|---|
| Préparateur (administrateur poste de travail) | Importer, rapprocher, lancer simulations, composer les vagues |
| Approbateur (responsable migration) | Approuver ou refuser une vague, déclencher l'arrêt d'urgence |
| Technicien | Lever une quarantaine, traiter une exception, demander un retour arrière |
| Lecteur (support, audit) | Consulter états et preuves |

## Choix structurants

| Sujet | Décision | Alternatives écartées |
|---|---|---|
| Changement de domaine | Jonction hors ligne, jeton chiffré par poste, connecteur B sous gMSA délégué | Compte de jonction avec mot de passe sur le poste : contraire aux règles de sécurité |
| Profil | Moteur natif dans l'agent, sur API Windows documentées, sans rétro-ingénierie d'outil tiers | ProfWiz : licence par poste et boîte noire difficile à auditer. USMT : copie complète, lent, double l'espace disque |
| Console | Web unique servie par l'API, Entra ID + rôles | Client WPF local : mono-opérateur, incompatible avec la double approbation |
| Hébergement | Serveur interne, joignable en HTTPS par les postes | Cloud : non retenu |
| Socle | .NET 10 LTS (API, agent, connecteurs), PostgreSQL, React | Rester sur .NET 8 : hors support depuis le 10/11/2026 |

## Mesures de succès

| Jalon | Critère |
|---|---|
| Laboratoire | 100 % des scénarios de la matrice passent, retour arrière compris, sur chaque version de Windows visée |
| Pilote | 100 % des lignes rapprochées ou en exception explicite ; ≥ 95 % de bascules réussies au premier essai ; 0 poste inutilisable ; chaque échec ramené à l'état initial |
| Généralisation | Chaque changement relié à un préparateur, un approbateur et une preuve ; durée d'indisponibilité par poste mesurée et publiée |
