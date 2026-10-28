# Modèle de menace initial — mise à jour 0.3.0

Actifs : données personnelles, bureau Windows, confirmations, souvenirs locaux,
modèles/binaires, secrets futurs et disponibilité du poste.

| Menace | Contrôle actuel | Limite / prochaine condition |
|---|---|---|
| Injection web/document/titre/mémoire/modèle | Les contenus n’accordent aucune permission ; souvenirs marqués comme données non fiables, registre C# indépendant | Pas de résistance parfaite du modèle garantie ; texte IA sans outil |
| Shell ou chemin inventé | Deux actions informatives ; chemins du moteur fixés par code | Revue de chaque futur gestionnaire et liste d’identités approuvées |
| Confirmation réutilisée, expirée, refusée ou bloquée | Commande immuable, délai actif, anti-rejeu, attente annulable et fenêtre native testée | Aucun effet Windows sensible activé |
| Action/génération après arrêt | Tokens liés, file séquentielle, arrêt verrouillé et destruction du processus | Chaque futur appel natif devra coopérer avant et pendant ses effets |
| Port local occupé par un autre processus | Vérification de l’écoute et du PID avant santé/génération, clé éphémère | Malware même compte et administrateur hors garantie |
| Exposition LAN du moteur | Host 127.0.0.1 imposé et contrôle de toutes les écoutes du PID | Ce contrôle ne bloque pas les connexions sortantes d’un binaire compromis |
| Binaire ou modèle altéré | Archive/modèle SHA-256 épinglés, comparaison des fichiers extraits et verrous de lecture | Les empreintes ne garantissent pas l’absence de vulnérabilité amont |
| Processus enfant orphelin | Job Object associé atomiquement à la création, kill-on-close, tests d’arrêt/crash | Gestion de durée de vie, pas une sandbox |
| Réponse modèle énorme ou outil injecté | Limites de corps/contexte/délai, outils rejetés, affichage texte seul | Pas d’exécution ni de rendu HTML |
| Journal contenant une conversation ou clé | Schéma technique sans paramètres ; sortie native vers NUL | Ne pas étendre le schéma avec des erreurs brutes |
| Paramètres altérés ou double écriture | Champs/valeurs/version validés et instance exclusive par dossier | Droits hérités du compte ; aucune isolation d’un malware local |
| Saturation | File d’actions, plafond anti-rejeu, rotation des logs, contexte et affichage bornés | Mesures prolongées à réaliser |
| Mémorisation ou partage implicite | Défaut désactivé, ajouts UI confirmés et option de partage locale séparée | Pas de mémorisation automatique des conversations |
| Injection SQL ou import modifiant des permissions | Requêtes préparées, valeurs liées, JSON versionné sans permissions, validation et transaction | Pas d’import de bases SQLite brutes |
| Écrasement ou import partiel | Révisions pour correction/suppression, conflit refusant tout l’import, fichier export créé sans écrasement | Pas de fusion automatique d’exports |
| Ancien souvenir réutilisé après suppression | Révision globale, contexte réinitialisé, réponse en cours invalidée | Requête locale déjà transmise non retirable ; pas d’effacement physique garanti |
| Données persistantes sensibles | Texte borné et formats de secrets usuels refusés ; base/export non chiffrés, aucune copie automatique | Détection non exhaustive, ne pas utiliser comme stockage de secrets |
| Base corrompue ou future | Version/identité contrôlées, refus et conservation de l’original | Réparation/migration à préparer sur copie, jamais par remise à zéro silencieuse |

Un gestionnaire C# malveillant, un binaire natif compromis ou une compromission
du compte peut contourner les protections applicatives. Aucun plugin non fiable
n’est chargé. Les tests ordinaires utilisent des doubles et des dossiers temporaires.
Les essais du modèle réel sont séparés, explicitement demandés, et n’utilisent
aucune donnée personnelle, capture écran, entrée micro ou frappe clavier réelle.

## Audio et transcription

Écoute furtive : consentement non persistant, maintien explicite et indicateur,
annulation sur perte de focus et fermeture. Repli de périphérique absent refusé.
La transcription reste une donnée non fiable, corrigeable ; transfert manuel dans
un brouillon puis envoi séparé, jamais passage direct aux actions. Pas de SSML.
Whisper : fichiers épinglés/verrouillés, pipes bornés, Job Object et délai ; aucune
écoute TCP autorisée. Échec de fermeture du pilote : indicateur conservateur,
session bloquée et demande de quitter. Aucune preuve d’effacement physique ou de
bon comportement de chaque pilote. Les tests matériels restent manuels.

## Images du personnage

Les fichiers de ressources locaux sont validés : dimensions et alpha PNG,
comptes/FPS/boucles, limites mémoire et chemins dans assets. Un chemin de manifeste
inexistant peut être résolu par basename seulement s’il est unique ; toute
ambiguïté est une erreur visible. Les grandes feuilles sont libérées après le
découpage réduit ; annulation et numérotation empêchent une ancienne sélection de
remplacer la dernière. Aucun calque mouth n’est composité au corps.

Le personnage ne recherche pas de fenêtre étrangère où s’asseoir et ne se déplace
pas sur initiative du modèle. Son ancrage est borné à la zone de travail ; les
prototypes perch/walk ne constituent pas une animation anatomique validée.
