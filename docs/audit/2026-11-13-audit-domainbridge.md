# Audit DomainBridge — idée, existant et améliorations

- Date : 2026-11-13
- Base auditée : branche `feature/operations-remediation-catalog` (commit `b24f090`), 78 commits au-delà de `main`
- État vérifié : `dotnet test DomainBridge.sln -c Release` → 87 tests, 0 échec (.NET SDK 8.0.428)
- Statut : **proposition à valider** — rien dans ce document n'est encore implémenté

## 1. Verdict

L'idée est bonne et le besoin est réel : après une migration d'objets AD, il reste à basculer chaque poste et à rattacher le profil existant au compte du domaine B, et ni Intune ni ADMT ne le font de manière centralisée, par vagues, avec preuves et retour arrière.

L'existant est une **coquille de contrôle correcte mais sans moteur** : enrôlement, inventaire technique, audit, approbation de campagne, packaging. Aucune ligne de code ne touche encore à un domaine ou à un profil. Trois problèmes dominent :

1. **Dérive de périmètre.** Environ 40 des 78 derniers commits construisent un catalogue de remédiations, des « actions technicien », une PKI d'éditeur de manifestes et un client WPF à onglets Parc / Collections / Déploiements / Bibliothèque. C'est un outil de type PDQ, que `PRODUCT_BRIEF.md` déclare lui-même hors périmètre.
2. **Le cœur du métier n'est pas conçu.** Il n'existe aucune spécification du changement de domaine, du rattachement de profil, de la machine à états sur le poste ni du rollback. Ce sont pourtant les seules parties risquées du produit.
3. **Les contrôles de sécurité sont déclarés mais non câblés.** Les politiques existent en classes testées, mais l'API ne les applique pas (détail en section 3).

## 2. Ce qui est solide et à conserver

| Élément | Pourquoi le garder |
|---|---|
| Agent à connexion sortante HTTPS, aucun canal entrant | Bon modèle : fonctionne derrière pare-feu et survit au changement de domaine |
| Tâches identifiées par un identifiant, jamais de script envoyé par la console | Limite fortement la surface d'attaque ; à étendre aux étapes de migration |
| Discipline TDD, fichiers `docs/testing/*.tdd.md`, fichiers de verrouillage NuGet | Base saine pour un développement automatisé |
| Séparation Contracts / Api / Agent / DirectoryConnector | Découpage adapté à la suite |
| Règles `AGENTS.md` (pas de secret, pas de Domain Admin sur les postes, idempotence, audit) | Restent la référence, inchangées |
| Installeur WiX + script de paquet Intune | Réutilisables une fois l'agent devenu un vrai service |

## 3. Constats sur l'existant

Gravité : **B** bloquant avant toute action réelle sur un poste, **M** majeur, **m** mineur.

### Sécurité

| # | Gr. | Constat | Preuve |
|---|---|---|---|
| S1 | B | Les points d'API de campagne (création, approbation, liste) n'ont **aucune authentification ni autorisation**. `OperatorAuthorizationPolicy` existe mais n'est appelée nulle part ; aucun `AddAuthentication`. Ils ne sont actifs qu'en mode Development/Testing… | `src/DomainBridge.Api/Program.cs:63-115` |
| S2 | B | …or le client de bureau **lance l'API packagée en mode Development**, en HTTP sur 127.0.0.1 : le produit installé tourne donc sans mTLS ni authentification. | `src/DomainBridge.Desktop/MainWindow.xaml.cs` (`StartPackagedApi`) |
| S3 | B | L'identité de l'agent n'est **pas liée à son certificat**. L'identifiant du poste vient de l'URL ou du corps : tout agent valide peut lire les tâches et publier des résultats au nom d'un autre poste. | `Program.cs:118-186` |
| S4 | B | La vérification d'empreinte est tautologique (le certificat est comparé à lui-même) et aucun émetteur n'est épinglé : tout certificat « client » reconnu par le magasin Windows du serveur est accepté. | `src/DomainBridge.Api/AgentTransportSecurity.cs` |
| S5 | B | L'approbation n'enregistre ni approbateur ni séparation des rôles. L'événement d'audit ne contient pas d'acteur, de résultat ni d'identifiant de tâche, contrairement à `SECURITY_BASELINE.md`. | `AuditStore.cs` (`AuditEvent`) |
| S6 | M | Campagnes et résultats sont **en mémoire** même quand PostgreSQL est configuré : perdus au redémarrage. Aucune clé d'idempotence. | `MaintenanceCampaignStore.cs` |
| S7 | M | L'immutabilité de l'audit repose sur des `GRANT` en commentaire SQL ; aucun chaînage ni scellement. | `deployment/postgres/001_audit_events.sql` |

### Produit et architecture

| # | Gr. | Constat |
|---|---|---|
| P1 | B | L'agent n'est **pas un service Windows** : exécutable console acceptant uniquement `--inventory-once`, version codée en dur `0.1.0`. La boucle de traitement (`MaintenanceJobProcessor`) n'est appelée par aucun hôte. |
| P2 | B | Le contrat d'inventaire **exclut volontairement utilisateurs et profils**. Or le besoin central (dernier utilisateur connecté → profil à migrer) exige exactement ces données. |
| P3 | B | Aucun import de liste, aucune correspondance compte A → compte B, aucune résolution de SID, aucun précontrôle, aucune machine à états, aucun rollback. Le connecteur d'annuaire est une simulation qui recopie un nom. |
| P4 | M | **Deux consoles** concurrentes (React et WPF) affichant les mêmes données. La WPF, mono-poste avec API locale, contredit l'objectif « centralisé » et la double approbation. |
| P5 | M | **.NET 8 est sorti du support le 10 novembre 2026.** Le socle doit passer à .NET 10 LTS avant d'ajouter du code sensible. |
| P6 | M | Documentation incohérente : `GATES.md` a été remplacé par des jalons « desktop », le README parle de « conformité hybride » et de « runbooks technicien », le plan cite encore « ECC Tools ». |
| P7 | m | La CI ne vérifie ni formatage, ni analyseurs, ni couverture, ni secrets, ni construction du MSI ; aucune signature de code. |

## 4. Audit de l'idée : ce que le cahier des charges ne dit pas encore

Ces points décident de la réussite sur le terrain. Ils sont intégrés au périmètre v2.

### 4.1 Le changement de domaine

- **Mécanisme recommandé : jonction hors ligne (Offline Domain Join).** Le connecteur du domaine B, sous compte de service géré (gMSA) délégué sur la seule OU de migration, réutilise le compte ordinateur déjà présent dans B et produit un jeton de jonction. Ce jeton est chiffré pour le certificat du poste destinataire, à durée courte, appliqué par l'agent puis effacé. Aucun mot de passe ne descend sur le poste. *À prouver en laboratoire en premier* (application directe sur un poste encore membre de A, ou sortie locale de A puis application).
- **Première ouverture de session dans B : le poste doit joindre un contrôleur de domaine B.** Il n'y a pas d'identifiants en cache pour le nouveau compte. Les postes nomades hors site sont donc une classe d'exception (VPN avant ouverture de session, ou passage sur site).
- **Risque de perte de réseau au redémarrage** : 802.1X / NAC filaire ou Wi-Fi avec certificat machine émis par la PKI de A, DNS, VPN machine. À détecter au précontrôle, sinon le poste redémarre isolé.
- **Compte ordinateur cible** : même nom ou renommage ? Le jeton doit viser le bon objet, dans la bonne OU, pour recevoir les bonnes GPO dès le premier démarrage.

### 4.2 Le profil

Le rattachement « sur place » (sans copie) consiste à donner au compte B les droits sur le dossier et la ruche de registre du profil existant, puis à déclarer ce dossier comme profil du nouveau SID. C'est rapide et sans doublement d'espace disque, mais :

- **Les secrets protégés par Windows ne suivent pas** : mots de passe enregistrés dans les navigateurs, Gestionnaire d'identifiants, certificats utilisateur avec clé privée, fichiers chiffrés EFS. Ils sont liés à l'ancien compte. C'est cohérent avec la règle « aucune migration de secret », mais doit être **annoncé aux utilisateurs** et détecté (EFS) au précontrôle.
- **Applications modernes (menu Démarrer, applications du Store)** : souvent cassées après changement de SID sur Windows 10/11 ; un réenregistrement à la première ouverture de session est nécessaire et doit être testé par version de Windows.
- **Par application** : OneDrive (reliaison), Outlook (profil et cache), Teams, lecteurs réseau et imprimantes mappés vers des serveurs de A.
- **Hors dossier de profil** : appartenance aux groupes locaux (ex. `A\utilisateur` administrateur local), tâches planifiées et services exécutés sous un compte de A, droits NTFS sur des données locales hors profil.
- **Le « dernier utilisateur connecté » n'est qu'un indice.** Postes partagés, profils multiples, comptes d'administration ou de service connectés en dernier : l'agent doit remonter la liste réelle des profils (taille, dernière utilisation, état chargé) et la console doit confronter liste importée et réalité avant d'autoriser une vague.

### 4.3 L'environnement du poste

- **BitLocker** : resauvegarder la clé de récupération dans B (ou Entra) après bascule ; vérifier l'absence de blocage au redémarrage.
- **Jonction hybride Entra / Intune** : un poste hybride via A change d'identité d'appareil en passant par B. Impact sur la conformité, l'utilisateur principal Intune et… sur le canal Intune qui a livré l'agent.
- **Certificat de l'agent** : s'il provient de l'auto-inscription de la PKI de A, son renouvellement et sa révocation cessent de fonctionner après bascule. Une autorité dédiée à DomainBridge, indépendante des deux domaines, est préférable.
- **LAPS, antivirus/EDR** (protection contre la modification de ruches et d'ACL en masse), **fuseau et heure** (Kerberos).

### 4.4 Le rollback

- Tant que le poste n'a pas basculé, toutes les étapes de préparation sont **additives** (on ajoute des droits, on ne retire rien) : retour arrière trivial.
- Après bascule, le retour dans A nécessite un jeton de jonction émis **par le domaine A**, donc un connecteur source avec un droit d'écriture délégué (réinitialisation du compte ordinateur). Le connecteur source n'est donc pas strictement en lecture seule : **décision à prendre**.
- L'agent tourne sous le compte système local et garde son canal HTTPS quel que soit le domaine : c'est lui le filet de sécurité. Il journalise chaque étape sur disque, reprend après redémarrage, et déclenche le retour arrière si la validation post-bascule échoue dans un délai configuré.

## 5. Améliorations proposées

1. **Recentrer** : geler le catalogue de remédiations, les actions technicien et la PKI d'éditeur. Conserver uniquement le patron « tâche approuvée → agent → résultat », et le réutiliser pour les étapes de migration.
2. **Une seule console** : web (React), servie par l'API centrale, authentification Entra ID et rôles. Geler le client WPF.
3. **Agent = vrai service Windows** signé, avec machine à états journalisée, reprise après redémarrage et étapes idempotentes.
4. **Identité d'agent liée au certificat** : l'identifiant du poste est lu dans le certificat, jamais dans la requête ; émetteur épinglé.
5. **Double approbation réelle** : préparateur ≠ approbateur, tous deux authentifiés, inscrits dans l'audit.
6. **Trois phases par poste** : *Préparer* (réversible, en arrière-plan, plusieurs jours avant) → *Basculer* (fenêtre de maintenance, un redémarrage) → *Valider* (preuves avant/après, puis nettoyage différé).
7. **Liste importée + réalité du poste** : rapprochement automatique, mise en quarantaine des écarts, décision explicite par poste partagé.
8. **Mode simulation (« dry-run »)** de bout en bout sur chaque poste avant toute vague réelle.
9. **Moteur de profil natif**, fondé sur les API Windows documentées, plutôt qu'une dépendance à un outil tiers sous licence (alternatives comparées dans le périmètre v2).
10. **Laboratoire automatisé à deux forêts** reconstruit par script, avec scénarios de bout en bout rejoués à chaque nuit : c'est la seule preuve valable pour un outil de ce type.
11. **Passage à .NET 10 LTS** et durcissement de la CI (format, analyseurs, couverture, secrets, MSI, signature).

## 6. Décisions à prendre (par ordre d'impact)

| # | Question | Ce que la réponse change |
|---|---|---|
| D1 | Les postes voient-ils déjà les contrôleurs de domaine B (routage, DNS) ? Existe-t-il une relation d'approbation A ↔ B ? | Faisabilité de la première ouverture de session, méthode de résolution des comptes, classe d'exception « nomades » |
| D2 | Les comptes ordinateurs dans B portent-ils le même nom que dans A ? Dans quelle OU ? | Conception du jeton de jonction et de la délégation du connecteur |
| D3 | Accepte-t-on un droit délégué d'écriture **dans A** (réinitialiser un compte ordinateur) pour permettre le retour arrière ? | Rollback automatique, ou rollback manuel par un technicien |
| D4 | Console web unique (recommandé) ou client de bureau ? | Gel ou poursuite de `DomainBridge.Desktop` |
| D5 | Moteur de profil natif (recommandé), ProfWiz sous licence, ou USMT ? | Coût de licence, durée par poste, espace disque, effort de test |
| D6 | Volume (nombre de postes), versions de Windows, part de postes hybrides Entra / gérés par Intune, part de nomades | Dimensionnement, matrice de tests, priorité des exceptions |
| D7 | Hébergement de la console : serveur interne ou cloud ? Quelle PKI pour les agents ? | Déploiement, certificat d'agent, exposition réseau |
