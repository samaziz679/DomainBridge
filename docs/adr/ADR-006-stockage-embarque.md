# ADR-006 — Stockage embarqué SQLite

- Statut : accepté
- Date : 2026-11-13
- Décideur : responsable du projet DomainBridge
- Remplace : PostgreSQL comme base de la plateforme (ADR-001). Confirme la proposition de l'ADR-004.

## Contexte

Le produit s'installe sur le poste de l'administrateur (ADR-004) pour 200 à 1 000 postes. Installer, sécuriser, sauvegarder et mettre à jour un serveur PostgreSQL sur un poste de travail est disproportionné. Par ailleurs, les magasins PostgreSQL existants n'avaient jamais été exécutés, faute de base de test.

## Décision

1. L'état du serveur est conservé dans **un fichier SQLite**, sans service à installer. Emplacement par défaut : `%ProgramData%\DomainBridge\Server\domainbridge.db` ; modifiable par `Storage:DatabasePath`.
2. Le schéma évolue par **migrations numérotées** embarquées dans le produit ; le numéro appliqué est conservé dans le fichier. Une base issue d'une version plus récente du produit est refusée au démarrage.
3. Hors développement et test, le stockage est **toujours durable** : sans chemin configuré, l'emplacement par défaut s'applique.
4. Le code et les scripts PostgreSQL sont **retirés**. Les interfaces de stockage sont conservées : un autre moteur pourra être rebranché si le produit est un jour hébergé sur un serveur.

## Garanties vérifiées par les tests

- Les événements d'audit et l'inventaire survivent au redémarrage de l'API, chaîne d'audit intacte.
- La base refuse elle-même toute modification ou suppression d'un événement d'audit (déclencheurs).
- Un même événement remis deux fois n'est enregistré qu'une fois.
- Quarante écritures simultanées laissent une chaîne intacte.

## Limites à connaître

- **Un fichier se copie et se remplace.** Les déclencheurs empêchent une modification par requête, pas la réécriture complète du fichier par quelqu'un qui a les droits d'écriture sur le disque. La chaîne d'empreintes détecte une modification partielle ; seule une copie extérieure (export SIEM, tranche 2.3) permet de détecter une réécriture complète.
- **Droits sur le dossier** : il doit être réservé au compte de service et aux administrateurs. À poser par l'installeur du serveur.
- **Sauvegarde** : copier le fichier à l'arrêt, ou utiliser la sauvegarde en ligne de SQLite. Procédure à écrire en tranche 6.4.
- **Un seul serveur** écrit dans le fichier. C'est cohérent avec l'hébergement sur un seul poste.

## Alternative écartée

PostgreSQL : adapté à un hébergement sur serveur, non retenu (ADR-004).
