# Livrer l'agent : Intune et GPO

Décision de référence : `docs/adr/ADR-002-distribution-agent.md`. Intune et la GPO **livrent** l'agent ; la console pilote la migration.

## Un seul paquet

L'agent est un MSI unique, `DomainBridge-Agent-<version>.msi`, identique pour toutes les voies de livraison.

```powershell
./installer/Agent/Build-AgentMsi.ps1 -SigningCertificateThumbprint <empreinte> -TimestampServer <serveur d'horodatage>
```

| Ce que fait le MSI | Détail |
|---|---|
| Installe | `C:\Program Files\DomainBridge\Agent\DomainBridge.Agent.exe`, exécutable autonome |
| Enregistre le service | `DomainBridgeAgent`, démarrage automatique, compte système local |
| Écrit la configuration | `HKLM\SOFTWARE\DomainBridge\Agent` : `Version`, `ServerUrl` |
| Mise à jour | Une version supérieure remplace la précédente et **conserve** l'adresse du serveur |
| Désinstallation | Arrête et retire le service, supprime les fichiers et la configuration |

Le MSI ne contient aucun secret. Il prend deux paramètres, tous deux conservés lors d'une mise à jour :

| Paramètre | Obligatoire | Rôle |
|---|---|---|
| `SERVERURL` | oui | Adresse HTTPS du serveur DomainBridge, sur le poste de l'administrateur |
| `CERTISSUERS` | non | Empreintes des autorités dont l'agent peut présenter un certificat, séparées par des virgules. Vide : toute autorité |

## PKI : rien n'est codé en dur

Aucune autorité de certification n'est inscrite dans le code. Tout se règle par configuration, des deux côtés :

| Côté | Réglage | Effet |
|---|---|---|
| Serveur | `AgentIdentity:TrustedIssuerThumbprints` (liste) | Seuls les certificats émis par ces autorités sont acceptés. Plusieurs autorités peuvent coexister, par exemple pendant un changement de PKI |
| Agent | `CERTISSUERS` à l'installation, ou `CertificateIssuerThumbprints` sous `HKLM\SOFTWARE\DomainBridge\Agent` | L'agent ne présente qu'un certificat émis par ces autorités |

Le réglage côté agent est utile dès qu'un poste détient **plusieurs certificats à son nom** : celui de l'auto-inscription du domaine A, celui d'Intune, celui du domaine B. Sans lui, l'agent prend celui qui expire le plus tard, qui peut ne pas être celui que le serveur accepte.

Changer de PKI en cours de projet se fait sans recompiler : ajouter la nouvelle autorité côté serveur, déployer les nouveaux certificats, mettre à jour `CERTISSUERS`, puis retirer l'ancienne autorité.

Sans certificat de machine ou sans `SERVERURL`, le service s'installe et démarre quand même : il attend et réessaie. Un certificat livré plus tard par un profil Intune est donc pris en compte sans réinstallation.

Sans `-SigningCertificateThumbprint`, le paquet est produit **non signé** et son nom se termine par `-unsigned` : il est réservé au poste de réserve.

## Voie Intune (postes gérés)

1. Emballer le MSI :

   ```powershell
   ./deployment/intune/Build-IntunePackage.ps1 -IntuneWinAppUtilPath <IntuneWinAppUtil.exe> -MsiPath <msi> -OutputDirectory <dossier>
   ```

2. Créer l'application Win32 :

   | Champ | Valeur |
   |---|---|
   | Installation | `msiexec /i "DomainBridge-Agent-<version>.msi" SERVERURL="https://<poste-admin>:<port>/" /qn /norestart` |
   | Désinstallation | `msiexec /x "{code produit}" /qn /norestart` |
   | Contexte | Système |
   | Détection | Code produit et version du MSI, proposés par Intune |

3. Affecter par groupes progressifs.

## Voie GPO (postes hors Intune)

1. Déposer le MSI et `deployment/gpo/Deploy-AgentStartup.ps1` sur un partage lisible par les comptes ordinateurs et inscriptible par les seuls administrateurs.
2. Créer une GPO liée aux OU concernées, avec un script de **démarrage de l'ordinateur** :

   ```powershell
   Deploy-AgentStartup.ps1 -MsiPath \\<serveur>\<partage>\DomainBridge-Agent-<version>.msi -ServerUrl https://<poste-admin>:<port>/
   ```

Le script n'installe que si la version présente est inférieure, et refuse un MSI dont la signature n'est pas valide.

## Postes que ni Intune ni la GPO n'atteignent

Installation manuelle du même MSI par un technicien, avec la même ligne de commande que la voie Intune.

## Vérifier un poste

```powershell
./deployment/scripts/Test-DomainBridgeAgent.ps1
```

Code de sortie 0 si l'agent est installé à la version attendue et en cours d'exécution.

## Ce qui n'est pas encore éprouvé

- L'installation réelle du MSI, sa mise à jour et sa désinstallation (scénario E12) : elles exigent des droits d'administrateur et se joueront sur le poste de réserve.
- La signature : aucun certificat de signature de code n'était disponible lors du développement.
- La remise du certificat de machine à l'agent (menace T2) : procédure à définir avec le choix de PKI.
