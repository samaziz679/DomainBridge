# Phase 1 — preuves de faisabilité sur poste de réserve

- Statut : **scripts écrits, non exécutés**. Le banc d'essai (tranche 1.1) n'est pas encore en place.
- Cadre : `docs/adr/ADR-004-poste-admin-et-poste-de-reserve.md`. Les essais utilisent les vrais domaines A et B, uniquement dans les OU de test, sur un poste de réserve sans utilisateur réel, avec des comptes fictifs.
- But : lever trois inconnues techniques avant de construire le moteur de migration. Le code de cette phase est jetable ; seuls les résultats sont conservés, dans des ADR.

## 1. Tranche 1.1 — réunir le banc d'essai

À préparer avec l'administrateur AD. Rien ne commence tant que les six points ne sont pas cochés.

- [ ] Poste de réserve Windows 11, membre de A, sans utilisateur réel, avec image disque ou moyen de réinstallation.
- [ ] OU de test dans A contenant ce poste ; OU de test dans B. Leur nom contient « test » (les scripts le vérifient).
- [ ] Compte ordinateur du poste **déjà créé** dans l'OU de test de B, sous le même nom.
- [ ] Deux ou trois comptes utilisateurs fictifs dans A et leurs équivalents dans B, dont un au login différent.
- [ ] Compte d'essai dont le seul droit est de réinitialiser les comptes ordinateurs de l'OU de test de B (et de A, pour le retour).
- [ ] Compte administrateur local connu sur le poste de réserve, pour reprendre la main si aucun domaine ne répond.

Avant le premier essai : prendre l'image du poste, et ouvrir une fois une session avec chaque compte fictif de A pour créer les profils témoins.

## 2. Tranche 1.2 — jonction hors ligne de A vers B

Question : un jeton de jonction émis par B pour le compte existant fait-il passer le poste de A à B, sans identifiant d'annuaire sur le poste ? Avec quelle séquence ?

**Étape 1 — sur une machine qui joint un contrôleur de B, sous le compte d'essai :**

```powershell
./lab/spikes/New-SpikeJoinToken.ps1 -TargetDomain <domaine-B> -ComputerName <poste> -TestOuDistinguishedName "<OU de test de B>" -OutputPath <support>\poste.token
```

Le fichier produit est un secret : il se transporte par un support de confiance, jamais par messagerie ni partage réseau.

**Étape 2 — sur le poste de réserve, en administrateur :**

```powershell
./lab/spikes/Invoke-SpikeJoinOnSpare.ps1 -Step Apply -SpareComputerName <poste> -TargetDomain <domaine-B> -TokenPath <support>\poste.token -Variant Direct
```

Le script refuse de tourner sur toute autre machine que celle nommée, applique le jeton puis supprime le fichier. Redémarrer, ouvrir une session avec le compte **local**, puis :

```powershell
./lab/spikes/Invoke-SpikeJoinOnSpare.ps1 -Step Verify -SpareComputerName <poste> -TargetDomain <domaine-B>
```

Résultats dans `C:\ProgramData\DomainBridgeSpike` (`apply.json`, `verify.json`), à rapporter pour analyse. Ils ne contiennent pas le jeton.

| Variante | Séquence |
|---|---|
| `Direct` | Le jeton est appliqué alors que le poste est encore membre de A |
| `UnjoinFirst` | Le poste quitte A localement (le compte dans A reste intact), puis applique le jeton |

Essayer `Direct` en premier ; restaurer l'image ; essayer `UnjoinFirst` si `Direct` échoue.

À consigner dans l'ADR « jonction » :

- la variante retenue et son taux de réussite sur plusieurs essais ;
- le contenu du groupe local Administrateurs après bascule (les groupes de A y restent-ils ?) ;
- le temps entre redémarrage et canal sécurisé valide ;
- les politiques de groupe de B effectivement reçues ;
- la durée de validité réelle du jeton et ce qui le rend caduc.

Jalon : une variante réussit trois fois de suite, avec restauration de l'image entre chaque essai.

## 3. Tranche 1.3 — rattachement du profil sur place

Question : après bascule, le compte B ouvre-t-il une session **dans le profil existant**, utilisable ? Que faut-il réparer, par version de Windows ?

Profil témoin (session ouverte une fois avec le compte fictif de A) : fichier sur le bureau, document dans Documents, fond d'écran modifié, une valeur de registre utilisateur, une application du Store épinglée, un identifiant enregistré dans le Gestionnaire d'identifiants.

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

## 4. Tranche 1.4 — retour dans A

Question : après une bascule réussie, peut-on ramener le poste et le profil dans A ?

Séquence à éprouver : jeton émis par A pour le compte ordinateur de départ (droit délégué sur l'OU de test de A), application sur le poste, redémarrage, restauration de la déclaration de profil initiale, ouverture de session avec le compte A.

À consigner : réussite sur plusieurs essais ; état du profil après l'aller-retour ; ce qui doit avoir été sauvegardé avant la bascule pour que le retour soit possible ; comportement si le contrôleur de A est injoignable.

Jalon : séquence de retour documentée et rejouée trois fois.

## 5. Règles de la phase

- Aucun essai sur un poste ou un profil appartenant à un utilisateur réel.
- Identités fictives uniquement ; délégations limitées aux OU de test.
- Un jeton de jonction ne figure jamais dans un fichier de résultat, un journal ou la console, et ne va jamais dans le dépôt.
- Seules les conclusions entrent dans le dépôt, sous forme d'ADR.
