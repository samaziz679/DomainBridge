# ADR-002 — Distribution de l'agent et répartition Intune / console

- Statut : accepté
- Date : 2026-11-13
- Décideur : responsable du projet DomainBridge
- Portée : livraison de l'agent sur les postes et lieu d'exécution de la logique de migration.

## Contexte

La majorité des postes à migrer est déjà gérée par Microsoft Intune ; une minorité ne l'est pas. Tous sont membres du domaine source A. Trois façons de conduire la migration ont été envisagées : un script Intune, une application Win32 Intune, la console centralisée DomainBridge.

## Décision

1. **Intune et la GPO sont des voies de livraison de l'agent, rien d'autre.** La migration est décidée, approuvée, suivie et annulée depuis la console, et exécutée sur le poste par l'agent.
2. **Postes gérés par Intune** : application Win32 contenant le MSI de l'agent, installée en contexte système, avec règle de détection sur la version et déploiement par groupes progressifs.
3. **Postes hors Intune** : le même MSI par GPO (installation assignée à l'ordinateur ou script de démarrage signé), ciblé sur les OU concernées, source en partage protégé.
4. **Postes que ni Intune ni la GPO n'atteignent** : installation manuelle du même MSI par un technicien, ou classement en exception.
5. **Un seul MSI signé** pour les trois voies : même nom de service, mêmes règles de version, de détection, de mise à jour et de désinstallation.
6. **Contrôle de couverture par la console** : la liste de migration est comparée aux agents enrôlés ; un poste sans agent actif est signalé et ne peut pas entrer dans une vague.
7. **Postes hybrides Entra gérés par Intune** : migrés dans une vague dédiée, après validation en laboratoire du changement d'identité d'appareil.

## Alternative écartée : migration par script Intune seul

| Limite | Conséquence |
|---|---|
| Le script doit porter de quoi joindre le domaine B (compte et mot de passe, ou jeton commun) et il est déposé sur le disque du poste | Contraire à la règle « aucun secret dans les scripts ou paquets Intune » d'`AGENTS.md` |
| Exécution unique, sans reprise après redémarrage | La migration s'étend avant et après un redémarrage ; il faudrait recréer un agent de fortune par tâche planifiée |
| Le poste hybride change d'identité d'appareil en passant de A à B | Le canal Intune peut se rompre au moment où le résultat doit remonter |
| Déclenchement au rythme de synchronisation du poste | Pas de fenêtre de maintenance maîtrisée, pas de plafond de simultanéité, pas d'arrêt d'urgence |
| Compte rendu limité à réussi / échoué | Pas de rapprochement avec la liste, pas de preuve avant/après, pas de retour arrière automatique |

Cette alternative ne resterait acceptable que pour quelques dizaines de postes sur site avec reprise manuelle des échecs ; ce n'est pas l'hypothèse du projet.

## Conséquences

- Le canal de l'agent (HTTPS sortant, authentification mutuelle par certificat) doit rester indépendant d'Intune et des deux domaines. Le certificat de l'agent ne doit donc pas dépendre de l'auto-inscription de la PKI du domaine A (voir décision D7 de l'audit).
- La remise du certificat d'agent peut passer par un profil de certificat Intune pour les postes gérés ; les postes hors Intune exigent un enrôlement par l'agent lui-même. Les deux voies doivent aboutir au même type de certificat.
- Les voies Intune et GPO sont testées par le même scénario de laboratoire (E12 du plan de développement) : installation, mise à jour, désinstallation, résultats identiques.
- La désinstallation de fin de projet suit la voie d'installation : retrait de l'affectation Intune ou de la GPO.

## Points à valider en laboratoire

- Comportement de l'inscription Intune d'un poste hybride après bascule vers B (identité d'appareil, utilisateur principal, conformité).
- Coexistence des deux voies sur un même poste (poste recevant à la fois l'application Intune et la GPO) : pas de double installation ni de rétrogradation de version.
