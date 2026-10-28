# Journal des modifications

## 0.6.0 — 28 octobre 2026 — Réponses parlées et bulle IRIS

- Réponses du chat reliées à la synthèse locale Windows, lecture activée par défaut
  selon la demande utilisateur, option de coupure, relecture et interruption.
- Bulle utilisateur RGBA et police Share Tech Mono privée ; pages lisibles adaptées
  au cadre, sans perte de texte et affichées pendant le clip correspondant.
- Un seul lecteur pour chat et Voix ; arrêt confirmé avant capture micro, annulation
  sur nouvelle question, fermeture, modification mémoire et arrêt d’urgence.
- Paquet autonome 0.6.0 avec PNG, TTF et licence OFL ; modèles communs inchangés.
- 95 contrôles, test du paquet complet et courte sortie audio WinMM réelle réussis.
  Aucun enregistrement de microphone. Voir SPOKEN_REPLIES.md.

## 0.5.1 — 27 octobre 2026 — Saisie du chat et signal vocal

- Zone Votre message multiligne, distincte de l’historique ; rédaction disponible
  pendant chargement et génération. Brouillons conservés, Entrée/Maj+Entrée.
- Push-to-talk arrêté au relâchement ou à 30 s, sans coupure sur hésitation.
  Capture souris perdue vérifiée après MouseUp ; répétition Espace limitée.
- Préparation ASR conservatrice des voix faibles, correction DC, marges 250 ms,
  normalisation bornée et indication de niveau/saturation sans log audio.
- 87 contrôles ordinaires ; modèle réel et cas Whisper synthétique faible testés.
  Le test synthétique ne remplace pas une mesure avec le microphone réel.
- Version autonome dans un nouveau dossier 0.5.1 ; ancienne version non écrasée.
  Fermer l’ancienne instance avant de relancer Lancer-IRIS.cmd.

## 0.5.0 — 27 octobre 2026 — Personnage animé, non publiée

- 25 spritesheets utilisateur découvertes et validées ; résolution signalée de
  spritesheets/ vers sprites/, sans modification des ressources fournies.
- Lecture à la demande, images réduites et libération des feuilles, alpha par
  pixel, proportions et bas-centre conservés ; position/échelle persistées.
- Personnage indépendant, clignement, sommeil/réveil local, déplacement explicite,
  réactions, thinking de génération et talk pendant la lecture audio.
- mouth isolé en prévisualisation uniquement : aucun double corps, pas de lipsync avancé.
- Prévisualisation sans IA : sélection, lecture/arrêt, FPS, cadence observée,
  mémoire retenue et diagnostics. Marche/perch demeurent des prototypes.
- 84 contrôles ordinaires, dont 22 WinForms ; détails et limites dans ANIMATIONS.md.
- Aucune nouvelle dépendance ni fonction autonome de contrôle du PC.

## 0.3.0 — 27 octobre 2026 — Souvenirs SQLite consentis, non publiée

- Nouveau module Memory, SQLite fourni par Windows, sans téléchargement.
- Défaut désactivé ; ajouts manuels et consentement distinct pour l’IA locale.
- Panneau de consultation, recherche accentuée, ajout, correction, suppression,
  désactivation, rétention et export/import JSON. Mutations confirmées et réservées à l’UI.
- Schéma versionné, transactions et rollback, révisions optimistes, SQL paramétré,
  expiration, bornes de volume et refus de bases corrompues/futures sans remplacement.
- Import tout ou rien sans écrasement ni activation de permission ; export en
  clair vers un nouveau fichier. Aucune promesse d’effacement physique des copies.
- Contexte de souvenirs borné et non fiable pour le modèle ; invalidation après
  correction, suppression ou expiration et réponse écartée si la révision change.
- 52 contrôles sans interface et 10 WinForms réussis ; essai réel du modèle
  retrouvant une donnée synthétique après réouverture, puis l’oubliant après suppression.
- Aucun historique automatique des conversations, microphone ou moteur vocal ajouté.

## 0.2.0 — 27 octobre 2026 — Conversation locale, non publiée

- SDK Microsoft .NET 10.0.403 installé après accord, archive et signature vérifiées.
- Compilation complète Release sans erreur ni avertissement.
- Confirmation native annulable, fermeture automatique et arrêt accessible ;
  expiration de la file et d’un service de confirmation qui ne répond pas.
- Verrou d’instance par dossier ; paramètres et raccourcis testés sans frappe réelle.
- Qwen3-4B-Instruct-2507 Q4_K_M et llama.cpp b11901 Vulkan acquis après accord,
  empreintes vérifiées et notices conservées hors dépôt dans les données IRIS.
- Chargement explicite, fichiers vérifiés/verrouillés, écoute loopback liée au PID,
  authentification éphémère, proxy/redirections désactivés et limites HTTP.
- Processus créé atomiquement dans un Job Object kill-on-close, sans shell ;
  fermeture normale, arrêt d’urgence et crash du propriétaire testés réellement.
- Fenêtre de conversation française, contexte borné en RAM, humour réglable,
  effacement de session, interruption et déchargement.
- 34 contrôles sans interface et 9 WinForms réussis ; essais réels modèle/interface
  réussis. Pas de streaming, mémoire durable, voix, rappels ni agenda pour l’instant.

## 0.1.0 — 27 octobre 2026 — Fondation, non publiée

- Audit statique de Capri v0.7.2 et conservation de l’historique/licence/README.
- Nouvelle solution indépendante de six projets C#, sans ressources Capri.
- Politique centralisée, confirmations ponctuelles, anti-rejeu, audit borné et arrêt.
- Gestionnaires date/heure Europe/Paris ; paramètres locaux validés.
- Fenêtre sombre IRIS, substitut visuel et réglages, raccourci d’arrêt configurable
  implémentés mais validation WinForms bloquée par l’absence de SDK .NET.
- 25 contrôles non graphiques exécutés via Roslyn/PowerShell sur .NET 10.0.11.
- Aucun moteur IA/audio, contrôle Windows générique, cloud ou téléchargement activé.
- Aucun paquet portable, résultat de performance ni MVP complet livré.

## 0.4.0 — Voix locale partielle

- Contrats audio, PCM/WAV borné, mesure RMS, gain, silence et calcul WER.
- Capture/lecture WinMM annulables, consentement éphémère et push-to-talk visible.
- ASR français Windows et Whisper Small CPU ; acquisition autorisée et vérifiée,
  WAV et transcription par pipes, sans enregistrement temporaire du microphone.
- Synthèse féminine française SAPI de secours, volume/vitesse et interruption.
- Transcription modifiable, transfert manuel vers le brouillon, envoi séparé.
- 75 contrôles ordinaires réussis ; modèle réel et cycle de vie revérifiés.
- Comparaison réelle sur six phrases synthétiques : détails dans VOICE.md.
- Matériel audio, naturel TTS, voix neuronale et synchronisation labiale restent
  à valider ou implémenter ; ce jalon ne constitue pas le MVP complet.


## Correction du lancement local

Version autonome win-x64 générée dans out/IRIS-portable-win-x64, runtime .NET et
Windows Desktop 10.0.14 inclus. Lancer-IRIS.cmd évite entièrement PowerShell ;
Start-IRIS.ps1 choisit cette version sans vérifier le .NET global. Build.ps1 la
régénère après les tests, avec les notices des dépendances. Les paramètres et
modèles restent dans le dossier de données utilisateur existant. Aucune
installation globale ou fonction de contrôle du PC ajoutée.


## Révision de distribution 0.5.2 — 28 octobre 2026

Le paquet est maintenant vérifié avec son dossier voisin IRIS-engine-data, sans
cache utilisateur requis. Mode explicite IRIS.exe --self-check : modèle CPU,
conversation réelle, synthèse en mémoire, transcription Whisper et transfert au
brouillon ; rapport passed=true. Les questions exactes heure/date sont traitées
via le registre existant en lecture seule, jamais via des commandes du modèle.
Voir DEPLOYMENT_CHECK.md pour le défaut, les preuves et les limites matérielles.

