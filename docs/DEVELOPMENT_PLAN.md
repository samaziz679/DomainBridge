# DomainBridge — plan de développement automatisé de bout en bout

- Version : 2.0 — 2026-11-13
- Statut : **validé** le 2026-11-13. Décisions de cadrage : `docs/adr/ADR-003-decisions-de-cadrage.md`. Références : `docs/PRODUCT_BRIEF.md` (périmètre v2), `docs/audit/2026-11-13-audit-domainbridge.md` (constats S*, P*, décisions D*).

## 1. Principe

Chaque incrément est une **tranche verticale** petite, livrée par la même boucle automatique, et n'est fusionnée que si une commande vérifiable passe. Les opérations qui modifient un poste ne sont autorisées qu'après avoir réussi, retour arrière compris, dans un laboratoire à deux forêts reconstruit par script.

Trois niveaux de preuve, du plus rapide au plus réaliste :

| Niveau | Où | Quand | Ce que ça prouve |
|---|---|---|---|
| 1. Tests unitaires et de contrat | CI hébergée | Chaque demande de fusion | Logique, contrats, politiques de sécurité |
| 2. Tests d'intégration sur poste | Machine Windows jetable, hors domaine | Chaque demande de fusion touchant l'agent | Service, profils et registre réels sur comptes locaux synthétiques |
| 3. Scénarios de bout en bout | Laboratoire Hyper-V, deux forêts | Chaque nuit et avant chaque version | Bascule réelle A → B, profil, retour arrière |

## 2. La boucle automatique (identique pour chaque tranche)

1. **Ticket** : une tranche = un ticket avec critère d'acceptation exécutable.
2. **Branche** dédiée, test d'abord (rouge), puis code minimal (vert), puis nettoyage.
3. **Vérification locale** par une commande unique, `scripts/verify.ps1` :
   - `dotnet format --verify-no-changes`
   - `dotnet build -warnaserror` avec analyseurs activés
   - `dotnet test` avec seuil de couverture (80 % global, 90 % sur l'agent et les politiques de sécurité)
   - console : `npm ci`, `npm test`, `npm run build`, `npm audit --audit-level=high`
   - recherche de secrets (gitleaks) et analyse des scripts PowerShell (PSScriptAnalyzer)
4. **Demande de fusion** : la CI rejoue `verify.ps1`, construit le MSI et le paquet Intune, publie les artefacts.
5. **Revue de sécurité obligatoire** (propriétaires de code) dès qu'un chemin sensible est touché : identité, certificats, jonction, profil, retour arrière — conformément à `AGENTS.md`.
6. **Fusion**, puis exécution nocturne du niveau 3 ; un échec nocturne ouvre automatiquement un ticket bloquant.
7. **Registre des jalons** `GATES.md` : une ligne par jalon, avec la commande et la preuve datée. Aucun jalon coché sans preuve.

Ce plan est conçu pour être exécuté tranche par tranche par un agent de développement, un humain validant les demandes de fusion sensibles et les passages de phase.

## 3. Laboratoire automatisé

Construit et détruit par script (`lab/Build-Lab.ps1`, module AutomatedLab sur Hyper-V), jamais à la main :

- forêt `lab-a.test` et forêt `lab-b.test`, un contrôleur de domaine chacune, réseau isolé ;
- un serveur DomainBridge (API, PostgreSQL, connecteurs, autorité de certification de test) ;
- postes clients : une image Windows 10 22H2 et une image Windows 11 ;
- identités 100 % synthétiques, générées par script ; comptes ordinateurs et utilisateurs pré-créés dans B pour reproduire l'hypothèse d'entrée ;
- point de contrôle Hyper-V restauré avant chaque scénario : résultats reproductibles.

Matrice minimale des scénarios de bout en bout (`lab/scenarios/*.ps1`, exécutés par un agent de CI auto-hébergé) :

| # | Scénario | Résultat attendu |
|---|---|---|
| E1 | Poste mono-utilisateur, login identique | Bascule réussie, profil retrouvé (bureau, documents, registre utilisateur) |
| E2 | Login différent entre A et B | Idem via correspondance explicite |
| E3 | Poste partagé, trois profils | Seuls les profils choisis sont rattachés |
| E4 | Utilisateur connecté pendant la préparation | Préparation sans interruption ; bascule différée |
| E5 | Coupure de courant simulée à chaque étape | Reprise ou retour arrière, jamais d'état intermédiaire |
| E6 | Contrôleur B injoignable après redémarrage | Retour automatique dans A dans le délai |
| E7 | Retour arrière demandé après bascule réussie | Poste et profil de nouveau utilisables dans A |
| E8 | Précontrôles bloquants (EFS, disque plein, serveur) | Refus, aucun changement |
| E9 | Rejeu de la même tâche | Aucun double effet (idempotence) |
| E10 | Agent usurpant l'identité d'un autre poste | Rejeté par l'API |
| E11 | BitLocker activé | Pas de blocage au démarrage, clé sauvegardée dans B |
| E12 | Installation, mise à jour, désinstallation par MSI | Mêmes résultats par les voies Intune et GPO |

## 4. Phases

Chaque phase se termine par un jalon vérifiable. Les phases 0 à 3 ne modifient aucun poste réel.

### Phase 0 — Cadrage et recentrage

| Tranche | Contenu | Jalon |
|---|---|---|
| 0.1 | Décisions D1–D7 prises (ADR-003) ; reste : OU de délégation dans A et B, mise à jour de l'ADR-001 | ADR acceptés |
| 0.2 | Gel du hors-périmètre : retrait du catalogue de remédiations, des actions technicien, de la PKI d'éditeur ; gel de `DomainBridge.Desktop` | Tests verts, plus aucune référence « maintenance » exposée |
| 0.3 | Passage à .NET 10 LTS, `scripts/verify.ps1`, CI durcie, propriétaires de code, remise à plat de `GATES.md` et du README | CI verte sur la nouvelle base |
| 0.4 | Modèle de menace écrit (agent, API, connecteurs, jeton de jonction) | Revue de sécurité signée |

### Phase 1 — Preuves de faisabilité en laboratoire (code jetable)

Lever les trois inconnues techniques **avant** de construire quoi que ce soit autour.

| Tranche | Question | Jalon |
|---|---|---|
| 1.1 | Laboratoire à deux forêts reconstruit par script | `lab/Build-Lab.ps1` aboutit sans intervention |
| 1.2 | La jonction hors ligne fait-elle passer un poste de A vers B en réutilisant le compte existant, sans mot de passe sur le poste ? Quelle séquence exacte ? | Séquence documentée et rejouée 5 fois de suite |
| 1.3 | Le rattachement de profil sur place donne-t-il une session utilisable, par version de Windows ? Que faut-il réparer (applications modernes, etc.) ? | Liste des corrections nécessaires, par version |
| 1.4 | Le retour dans A fonctionne-t-il après bascule ? | Séquence de retour documentée |

Les résultats sont consignés dans des ADR ; le code d'essai n'est pas conservé.

### Phase 2 — Socle de confiance

| Tranche | Contenu | Corrige |
|---|---|---|
| 2.1 | Authentification Entra ID des opérateurs, rôles Préparateur / Approbateur / Technicien / Lecteur appliqués sur chaque point d'API | S1, S2 |
| 2.2 | Identité d'agent lue dans le certificat, émetteur épinglé, autorité dédiée DomainBridge, enrôlement et renouvellement | S3, S4 |
| 2.3 | Audit complet (acteur, poste, tâche, résultat), chaîné et en ajout seul ; export SIEM | S5, S7 |
| 2.4 | Persistance PostgreSQL de tout l'état, migrations de schéma versionnées, clés d'idempotence | S6 |
| 2.5 | Agent en service Windows : boucle de scrutation, journal local, reprise après redémarrage, mise à jour par MSI | P1 |
| 2.6 | MSI unique signé (certificat de test en laboratoire), paquet Intune et déploiement GPO | P7 |

Jalon : scénarios E10 et E12 verts ; aucune route d'API accessible sans identité.

### Phase 3 — Connaître avant d'agir (lecture seule)

| Tranche | Contenu |
|---|---|
| 3.1 | Inventaire de migration par l'agent : domaine, profils, signaux de risque (corrige P2) |
| 3.2 | Connecteurs réels en lecture sur A et B : résolution de comptes et d'identifiants de sécurité, compte ordinateur cible |
| 3.3 | Import CSV, rapprochement liste / poste / annuaires, quarantaine |
| 3.4 | Précontrôles et verdict par poste, motifs normalisés |
| 3.5 | Console : import, rapprochement, fiche poste, file des exceptions |

Jalon : sur le laboratoire, 100 % des lignes d'une liste d'essai sont classées prêtes, réservées, bloquées ou en quarantaine ; scénario E8 vert. **Cette phase est déjà utile en production** : elle peut tourner sur le parc réel en lecture seule et révéler l'ampleur des exceptions avant que le moteur n'existe.

### Phase 4 — Moteur de migration (laboratoire uniquement)

Verrou logiciel : les étapes modifiantes ne s'exécutent que si l'API est configurée « laboratoire » et le poste dans une liste d'autorisation.

| Tranche | Contenu | Scénarios |
|---|---|---|
| 4.1 | Machine à états de l'agent : étapes idempotentes, journal sur disque, reprise, simulation | E5, E9 |
| 4.2 | Phase *Préparer* : capture de l'état de retour, ajout des droits sur dossier et registre du profil | E4 |
| 4.3 | Connecteur B : émission du jeton de jonction, chiffrement pour le poste, expiration, effacement | — |
| 4.4 | Phase *Basculer* : application du jeton, redémarrage, déclaration du profil, groupes locaux | E1, E2, E3 |
| 4.5 | Phase *Valider* et corrections après bascule (BitLocker, applications modernes) | E11 |
| 4.6 | Retour arrière automatique et à la demande | E6, E7 |

Jalon : matrice E1–E12 verte trois nuits consécutives sur chaque version de Windows visée.

### Phase 5 — Campagnes

| Tranche | Contenu |
|---|---|
| 5.1 | Vagues, fenêtres de maintenance, plafond de simultanéité |
| 5.2 | Double approbation, arrêt d'urgence, seuil d'arrêt automatique |
| 5.3 | Tableau de bord de vague, fiche de preuve par poste, exports |
| 5.4 | Essai de charge : 1 000 agents simulés |

Jalon : une vague de laboratoire complète, approuvée par deux identités distinctes, avec un échec provoqué ramené à l'état initial.

### Phase 6 — Durcissement et mise en production

| Tranche | Contenu |
|---|---|
| 6.1 | Signature de code de production (clé hors dépôt), SBOM, version reproductible |
| 6.2 | Test d'intrusion ciblé : agent, API, jeton de jonction |
| 6.3 | Guides d'installation, d'exploitation, de support et de retour arrière ; communication aux utilisateurs sur les limites connues |
| 6.4 | Sauvegarde, restauration et supervision de la plateforme |

Jalon : revue de sécurité finale sans réserve bloquante.

### Phase 7 — Pilote

Phase 3 d'abord sur tout le parc (lecture seule), puis simulation, puis 5 à 10 postes de l'équipe informatique, puis 20 à 50 postes volontaires de profils variés. Critères de succès : ceux du périmètre v2. Chaque incident devient un scénario de laboratoire supplémentaire avant la vague suivante.

### Phase 8 — Généralisation et clôture

Vagues de taille croissante, revue après chaque vague, traitement des classes d'exception en vagues dédiées, puis désinstallation de l'agent, révocation des certificats, retrait des délégations dans A et B, archivage de l'audit.

## 5. Ordre et dépendances

```text
0 Cadrage ─► 1 Faisabilité ─► 4 Moteur ─► 5 Campagnes ─► 6 Durcissement ─► 7 Pilote ─► 8 Généralisation
     └─────► 2 Socle ─► 3 Lecture seule ─┘
```

- La phase 1 peut invalider des choix du périmètre : c'est son rôle, et c'est pourquoi elle précède tout développement du moteur.
- Les phases 2 et 3 ne dépendent pas des réponses techniques de la phase 1 et avancent en parallèle dès la fin de la phase 0.

## 6. Risques principaux

| Risque | Parade |
|---|---|
| La jonction hors ligne ne convient pas à l'environnement réel | Tranche 1.2 en tout premier ; variante documentée dans l'ADR jonction |
| Poste isolé du réseau après redémarrage | Précontrôle bloquant ; retour arrière automatique (E6) |
| Profil rattaché mais applications cassées | Tranche 1.3 par version de Windows ; corrections automatisées et testées (E1) |
| Antivirus / EDR bloquant l'agent | Exclusion validée avec l'équipe sécurité pendant le pilote informatique |
| Nouvelle dérive de périmètre | Toute tranche hors `PRODUCT_BRIEF.md` est refusée en revue |
| Secret dans le dépôt ou les journaux | Recherche de secrets en CI ; jeton de jonction jamais journalisé, test dédié |

## 7. Prochaine action

Démarrer la phase 0 par les tranches 0.2 (retrait du hors-périmètre, dont `DomainBridge.Desktop`) et 0.3 (.NET 10, vérification unique, CI durcie).
