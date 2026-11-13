# Phase 1 — preuves de faisabilité en laboratoire

- Statut : **scripts écrits, non exécutés** — aucun hôte de laboratoire n'était disponible le 2026-11-13.
- But : lever trois inconnues techniques avant de construire le moteur de migration. Le code de cette phase est jetable ; seuls les résultats sont conservés, dans des ADR.

## 1. Hôte nécessaire

| Topologie | Machines | Mémoire libre | Disque libre | ISO |
|---|---|---|---|---|
| Minimale | DCA, DCB, CL11 | 7 Go | 80 Go | Windows Server, Windows 11 |
| Complète | + CL10, SRV | 14 Go | 140 Go | + Windows 10 |

Prérequis logiciels : Windows 10/11 Pro ou Windows Server avec Hyper-V activé, session PowerShell élevée, module AutomatedLab (`Install-Module AutomatedLab`, puis `New-LabSourcesFolder`), ISO d'évaluation Microsoft déposés dans `C:\LabSources\ISOs`.

Vérification, sans rien modifier :

```powershell
./lab/Test-LabHost.ps1 -Topology Minimal
```

La topologie minimale suffit pour les tranches 1.2 à 1.4 sur Windows 11. La complète est nécessaire pour valider Windows 10 (décision D6) et, plus tard, pour les scénarios de bout en bout.

## 2. Tranche 1.1 — construire le laboratoire

```powershell
./lab/Build-Lab.ps1 -Topology Minimal
```

Résultat attendu : forêts `lab-a.test` et `lab-b.test` avec approbation bidirectionnelle (décision D1), client joint à A, utilisateurs synthétiques dans les deux forêts, compte ordinateur du client **déjà créé** dans l'OU `Migration` de B (hypothèse d'entrée du produit, décision D2), point de contrôle `Baseline` sur chaque machine.

Jalon : le script aboutit sans intervention, deux fois de suite après destruction complète du laboratoire.

## 3. Tranche 1.2 — jonction hors ligne de A vers B

Question : un jeton de jonction émis par B pour le compte existant fait-il passer le poste de A à B, sans identifiant d'annuaire sur le poste ? Avec quelle séquence ?

```powershell
./lab/spikes/Spike-OfflineDomainJoin.ps1 -Variant Direct -Iterations 5
./lab/spikes/Spike-OfflineDomainJoin.ps1 -Variant UnjoinFirst -Iterations 5
```

| Variante | Séquence |
|---|---|
| `Direct` | Le jeton est appliqué alors que le poste est encore membre de A |
| `UnjoinFirst` | Le poste quitte A localement (le compte dans A n'est pas touché), puis applique le jeton |

À consigner dans l'ADR « jonction » :

- la variante retenue et son taux de réussite sur cinq essais ;
- le contenu du groupe local Administrateurs après bascule (les groupes de A y restent-ils ?) ;
- le temps entre redémarrage et canal sécurisé valide ;
- le comportement si le compte dans B est dans une autre OU ou désactivé ;
- la durée de validité réelle d'un jeton et ce qui l'invalide.

Jalon : une variante réussit cinq fois de suite.

## 4. Tranche 1.3 — rattachement du profil sur place

Question : après bascule, le compte B ouvre-t-il une session **dans le profil existant**, utilisable ? Que faut-il réparer, par version de Windows ?

Préparation du profil témoin (session ouverte une fois avec le compte A) : fichier sur le bureau, document dans Documents, fond d'écran modifié, une valeur de registre utilisateur, une application du Store épinglée, un identifiant enregistré dans le Gestionnaire d'identifiants.

Séquence à éprouver, dans cet ordre :

1. Résoudre l'identifiant de sécurité du compte A et celui du compte B (possible avant bascule grâce à l'approbation).
2. **Avant bascule**, ajouter les droits du compte B sur le dossier du profil, sans suivre les jonctions, sans rien retirer.
3. **Avant bascule**, charger la ruche de registre du profil et celle des classes, y ajouter les droits du compte B, les décharger.
4. Basculer le poste (séquence retenue en 1.2).
5. **Après bascule**, déclarer le dossier existant comme profil du compte B dans la liste des profils de la machine.
6. Ouvrir une session avec le compte B.

À consigner dans l'ADR « profil », pour Windows 11 puis Windows 10 :

| Observation | Résultat attendu |
|---|---|
| La session s'ouvre dans le profil existant, sans profil temporaire | oui |
| Bureau, Documents, fond d'écran, valeur de registre | retrouvés |
| Menu Démarrer, recherche, applications du Store | à constater ; lister la correction nécessaire |
| Identifiant enregistré | perdu (attendu, limite connue) |
| Durée des étapes 2 et 3 pour un profil de 1, 10 et 50 Go | mesurée |
| Comportement si le profil est chargé (session ouverte) pendant l'étape 3 | à constater |
| Comportement face à une jonction piégée dans le profil (menace T10) | aucun droit accordé hors du profil |

Jalon : une liste fermée des corrections nécessaires par version de Windows, chacune rejouée avec succès.

## 5. Tranche 1.4 — retour dans A

Question : après une bascule réussie, peut-on ramener le poste et le profil dans A ?

Séquence à éprouver : jeton émis par A pour le compte d'origine (droit délégué de la décision D3), application sur le poste, redémarrage, restauration de la déclaration de profil d'origine, ouverture de session avec le compte A.

À consigner : réussite sur cinq essais ; état du profil après l'aller-retour ; ce qu'il faut avoir sauvegardé avant la bascule pour que le retour soit possible ; comportement si le contrôleur de A est injoignable.

Jalon : séquence de retour documentée et rejouée cinq fois.

## 6. Règles de la phase

- Identités, noms et certificats 100 % synthétiques.
- Le mot de passe du laboratoire est généré à la construction et conservé hors dépôt, protégé pour l'utilisateur Windows courant.
- Un jeton de jonction n'est jamais écrit dans un fichier de résultat, un journal ou la console.
- Les résultats vont dans `artifacts/lab/` (ignoré par git) ; seules les conclusions entrent dans le dépôt, sous forme d'ADR.
