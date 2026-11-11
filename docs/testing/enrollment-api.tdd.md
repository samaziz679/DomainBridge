# TDD evidence — API d’enrôlement

## Portée

L’API expose un contrôle de santé et un seul endpoint d’enrôlement, `POST /api/v1/enrollments`. Elle accepte exclusivement l’identifiant d’agent, l’empreinte de certificat et la version. Elle n’effectue aucune opération Active Directory et ne conserve aucun secret.

## Preuves

| Étape | Commande | Résultat |
|---|---|---|
| RED | `dotnet test tests\\DomainBridge.Api.Tests\\DomainBridge.Api.Tests.csproj` | Échec attendu : l’API ne contenait pas de point d’entrée. |
| GREEN | `dotnet test tests\\DomainBridge.Api.Tests\\DomainBridge.Api.Tests.csproj --no-restore` | 4/4 tests passent après ajout de l’endpoint minimal. |
| Couverture finale | `dotnet test tests\\DomainBridge.Api.Tests\\DomainBridge.Api.Tests.csproj --no-restore --collect:"XPlat Code Coverage"` | 8/8 tests passent ; 100 % lignes, 83,3 % branches. |

## Garanties testées

- `GET /health` répond 200.
- Les trois métadonnées non secrètes valides sont acceptées.
- Les identifiants, empreintes et versions manquants sont refusés.
- JSON invalide, structure non objet et champs additionnels tels que `password` sont refusés.
- La réponse d’enrôlement ne renvoie pas de champ `password`.

## Limites

Le contrôle mTLS, la validation cryptographique du certificat, l’autorisation opérateur, la persistance d’audit et les flux Intune/GPO seront traités dans des incréments séparés avec leurs propres tests.
