# Sécurité — jalon 0.5.0

## Contrôles exécutés

- Registre fermé : date/heure et gestion des souvenirs depuis l’interface uniquement. Noms inconnus, origine externe,
  valeurs enum invalides, cible incorrecte, schéma invalide et expiration sont refusés.
- Métadonnées de permission/risque choisies par C#. Denied et Prohibited restent
  prioritaires ; Sensitive/Destructive exigent une confirmation ponctuelle.
- Refus par défaut, approbation explicite, expiration, rejeu, annulation et
  fermeture automatique de la confirmation testés. Son bouton d’arrêt reste accessible.
- Arrêt d’urgence verrouillé jusqu’au redémarrage ; annulation active et en file.
  Les gestionnaires doivent coopérer. Aucun contrôle générique n’est chargé.
- Journal technique sans paramètres, texte de conversation ou erreur brute.
  Une écriture préalable impossible bloque l’action. Rotation bornée.
- Verrou de fichier exclusif empêchant deux instances sur le même dossier ;
  le fichier vide restant après fermeture ne bloque pas le redémarrage.
- Aucune capture d’écran, API cloud, mise à jour distante, shell ou registre
  dans le chemin IRIS. Les scripts d’acquisition externes sont explicitement déclenchés.
- Microphone soumis au consentement éphémère et au maintien explicite ; transcription
  corrigeable, sans envoi automatique au modèle. Audio en mémoire et indicateur
  visible ; arrêt non confirmé par le pilote signalé comme potentiellement actif.
  Capture et lecture réelles restent à tester manuellement. Voir VOICE.md.

Les tests de raccourci emploient une doublure : aucune frappe physique n’a été
produite. La combinaison réelle et les conflits Windows doivent encore être
vérifiés manuellement. Le bouton d’arrêt a été testé dans les fenêtres natives.

## Processus et réseau local

Avant chaque chargement, l’archive épinglée, le modèle et chaque fichier extrait
sont vérifiés. Un fichier supplémentaire bloque le démarrage. Des verrous de
lecture sont conservés pendant le fonctionnement pour limiter les remplacements
après vérification. Aucun exécutable choisi par le modèle n’est accepté.

Le lanceur Win32 crée le processus dans le Job Object dès sa création, sans course
d’assignation après démarrage, grâce à PROC_THREAD_ATTRIBUTE_JOB_LIST. Il n’hérite
que d’un handle NUL pour les entrées/sorties. L’environnement est réduit ; la clé
aléatoire de 256 bits passe par environnement, sans ligne de commande ni fichier.
L’arrêt du parent ferme le Job Object kill-on-close. L’arrêt brutal d’un propriétaire
de test et l’arrêt d’urgence ont été vérifiés avec le moteur réel.
Ce mécanisme gère la durée de vie : ce n’est pas une sandbox Windows.

Le processus reçoit --host 127.0.0.1, un port dynamique, --offline et --no-webui.
Avant la santé et chaque génération, la table TCP Windows vérifie le PID et
refuse toute écoute différente du port prévu sur 127.0.0.1. Ainsi, une course
sur le port n’autorise pas de conversation avec un autre processus. Le client
impose l’authentification et désactive proxy, cookies et redirections. Réponses
limitées à 64 Kio, messages et contexte bornés, génération limitée à deux minutes.
Les appels d’outils éventuellement produits par le modèle sont rejetés.

Le contrôle TCP porte sur les écoutes ; il ne constitue pas une interdiction
système des connexions sortantes d’un binaire compromis. Il n’existe pas de
sandbox réseau, d’isolation matérielle ni de protection contre un administrateur
ou un malware exécuté sous le même compte. Les empreintes garantissent la
conformité aux versions épinglées, pas l’absence de vulnérabilité dans ces versions.

Référence de l’API atomique :
[Microsoft UpdateProcThreadAttribute](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-updateprocthreadattribute).

## Secrets, données et limites

Aucun secret persistant n’est accepté. SecretStore DPAPI CurrentUser et ses tests
restent nécessaires avant toute clé persistante. La clé de session locale reste
en mémoire/environnement du processus ; les réglages, souvenirs et journaux ne sont pas chiffrés.

Données dans %LOCALAPPDATA%\IRIS, indépendantes de Capri. Paramètres validés,
version/champs/valeurs bornés, écriture temporaire puis remplacement. Un fichier
invalide est conservé et le démarrage affiche une erreur. Pas d’ACL supplémentaire :
les droits hérités du profil s’appliquent. Journal JSONL avec rotation vers une
génération précédente à environ 1 Mio. Aucun historique de conversation sur disque.

La mémoire SQLite est désactivée par défaut, avec un consentement séparé pour son
utilisation par le modèle. Aucun souvenir n’est extrait automatiquement d’une
conversation. Chaque mutation est réservée à l’origine UI et passe par la
confirmation native. SQL préparé, paramètres liés, transactions et révisions
empêchent l’injection SQL et l’écrasement d’une sélection périmée. La purge des
expirés suit la durée de conservation choisie et confirmée, aux accès à la base.
La base future/corrompue n’est jamais réinitialisée automatiquement. Le composant
SQLite est chargé depuis System32, sans téléchargement ni bibliothèque privée.

Imports JSON bornés et validés, champs inconnus refusés, aucune permission
restaurée depuis un fichier. Une erreur ou un conflit refuse l’import entier.
Les exports sont en clair, sur choix de fichier explicite, sans écrasement ; les
copies restent indépendantes de la suppression dans la base. Les formats usuels
de secrets sont refusés, sans prétention à une détection exhaustive.

Les souvenirs injectés dans la conversation restent des données non fiables,
jamais une instruction système ou une autorisation. Les changements de révision
réinitialisent le contexte ; une réponse en cours est écartée s’il a changé.
La recherche et l’enveloppe envoyée au modèle sont bornées. Voir MEMORY.md pour
les limites de rétention, d’effacement physique et de sauvegarde.

La suppression de la session ne constitue pas une promesse d’effacement physique
de la RAM ou des sauvegardes. Une erreur de journal après un effet ne l’annule pas.
Les futurs gestionnaires devront être idempotents lorsque pertinent et vérifier
leurs résultats.

Avant applications/médias : identités explicitement approuvées, aucun chemin ou
argument libre du modèle, aucun hôte de scripts/interpréteur autorisé comme app.
Cette liste ne suffit pas à isoler le contrôle générique du bureau. Les données
externes/mémorisées ne pourront jamais modifier les permissions. Origin doit
rester défini par un adaptateur de confiance, pas par le JSON du modèle.

## Personnage — 0.5.0

Le moteur de rendu n’importe aucun contrôleur PC du monolithe Capri. Il déplace
uniquement sa propre fenêtre, suite à une commande de menu ou un glissement.
Sleep/wake utilise l’activité locale d’IRIS : filtre de messages de l’application,
sans hook global, valeur de touche ni journal de frappe. Aucun accès au contenu
des autres fenêtres. Aucun texte du modèle n’est interprété comme une animation
ou une commande. Les callbacks de génération et lecture audio choisissent des
états fixes. Le pipeline de permissions reste inchangé. L’arrêt d’urgence annule
les chargements, arrête les timers et ferme la prévisualisation.

Les fichiers locaux sont des ressources de confiance du projet, validées en
forme et en taille ; GDI+ n’est pas une sandbox de décodage d’images hostiles.
Le présentateur acquiert un DC uniquement pour dessiner sa fenêtre alpha et ne
capture aucun pixel du bureau. Les limites et tests sont dans ANIMATIONS.md.

## Lecture et bulle — 0.6.0

La réponse du chat est transmise au contrôleur de lecture et à la bulle dessinée
avec les ressources fournies. Le texte reste une donnée, jamais du SSML ou une
commande. La police est privée au processus. Les sorties sont sérialisées et
l’entrée microphone attend leur arrêt ; erreur de fermeture pilote : session
bloquée. Aucun nouveau droit PC ni téléchargement. Effacer/changer la mémoire
arrête aussi la lecture et ferme la bulle. Détails dans SPOKEN_REPLIES.md.
