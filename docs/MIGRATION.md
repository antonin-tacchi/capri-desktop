# Migration et état des lots — 27 octobre 2026

| Lot | État réel | Validation / reste à faire |
|---|---|---|
| 1 — Audit | Réalisé, statique initial | Sources et provenance conservées ; pas de lancement Capri |
| 2 — Neutralisation / identité | Réalisé pour IRIS | Aucun lien vers le code ou les médias historiques ; scripts/workflows historiques désactivés |
| 3 — Solution .NET 10 | Réalisé et compilé | SDK Microsoft 10.0.403 installé après accord ; compilation Release sans erreur ni avertissement |
| 4 — Permissions | Fonctionnel pour le registre actuel | Refus par défaut, confirmations natives, expiration et arrêt testés ; aucune action Windows sensible réelle activée |
| 5 — Configuration / interface | Fonctionnel pour le socle actuel | Personnage transparent IRIS et prévisualisation sans IA ; persistance, instance unique et rendus testés ; pas encore toutes les sections finales |
| 6 — IA / personnalité | Partiellement fonctionnel, testé réellement | Conversation française, contexte borné, humour, chargement/déchargement et arrêt ; réponse en flux, benchmarks complets et initiative contextuelle non implémentés |
| 7 — Mémoire SQLite | Fonctionnel pour les souvenirs explicites, testé | Consentements séparés, CRUD, recherche, rétention, JSON et invalidation du contexte ; aucun historique automatique des conversations |
| 8–9 — ASR / TTS français | Partiellement implémenté | Whisper/Windows comparés sur corpus synthétique, UI push-to-talk testée avec doublures ; matériel et voix naturelle à valider |
| 10 — Actions Windows | Prévu hors date/heure | Liste d’apps approuvées, médias limités et identité cible ; pas de contrôle générique |
| 11 — Rappels / agenda | Prévu, non implémenté | SQLite, Europe/Paris, ambiguïtés DST et notifications |
| 12 — Distribution / performances | Paquet autonome x64 local construit | Runtime .NET/Windows Desktop 10.0.14 inclus et notices copiées ; publication et mesures prolongées restantes |

## Prochaine étape

Vérifier manuellement la capture et la lecture sur les périphériques choisis, puis comparer une voix féminine neuronale après revue des licences et accord d’acquisition. Le jalon 0.4.0 fournit Whisper Small et Windows hors ligne, une transcription corrigeable et Hortense comme secours. Aucun microphone n’a été utilisé par les tests automatiques. Voir VOICE.md.

Le lot mémoire utilise le SQLite système Windows, sans téléchargement. Voir
MEMORY.md : défaut désactivé, ajouts volontaires, partage local séparé, rétention
et exports non chiffrés. Une suppression dans la base active n’est pas une garantie
d’effacement physique des copies. Les tests ordinaires utilisent des bases temporaires.

## État de l’IA

Fichiers acquis après accord : Qwen3-4B-Instruct-2507 Q4_K_M (Unsloth) et
llama.cpp b11901 Vulkan, versions et empreintes épinglées. Aucun moteur n’est lancé
au démarrage ; chargement uniquement dans la fenêtre de conversation.
L’authentification et le contrôle des écoutes du PID ont été testés avec le vrai
moteur. Le modèle ne reçoit aucun outil et aucun lien n’existe vers le dispatcher
d’actions depuis sa réponse. Le mode CPU est un repli manuel.

Le jalon 0.5.0 intègre les 25 spritesheets fournies, leurs FPS/boucles et un personnage déplaçable. Les poses restent des prototypes, sans modification des PNG. Voir ANIMATIONS.md pour les comportements et limites.

Le MVP complet du cahier des charges n’est pas encore atteint. Le jalon actuel
est une conversation locale utilisable avec souvenirs explicites et voix locale partielle, avec les
protections du socle et des tests exécutés. Les sources Capri n’ont pas été supprimées.


