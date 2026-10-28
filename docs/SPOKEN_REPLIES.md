# Réponses parlées et bulle — 0.7.0

Fermer l’ancienne instance puis lancer Lancer-IRIS.cmd. Le titre doit afficher
IRIS 0.7.0. Dans **Voix**, Siwis est sélectionnée par défaut. **Tester la voix**
permet de l’entendre sans charger l’IA. Volume et débit restent réglables ;
sélectionner Hortense puis enregistrer conserve ce choix.

La bulle utilise le PNG fourni, son alpha et la police privée Share Tech Mono.
Sa largeur passe de 780 à 560 pixels (hauteur 186), soit environ 28 % de moins.
Elle conserve les proportions, suit le personnage et reste dans la zone de
travail sans prendre le focus. Le TTF et sa licence OFL accompagnent le paquet.

Le texte apparaît progressivement par mots. La position du périphérique audio
est consultée toutes les 25 ms ; un chronomètre sert de secours si nécessaire.
Les repères Windows viennent de SAPI. Siwis ne fournit pas de repères : ceux
d’une référence française Windows sont ajustés à la durée du clip Siwis. Si la
référence manque, une estimation selon les mots et la ponctuation est utilisée.
Le calage Siwis reste approximatif. Il ne s’agit pas de synchronisation labiale.

Les clips prononcés ne sont plus découpés selon les pages de la bulle : un clip
peut couvrir plusieurs pages sans pause entre celles-ci. Au-delà de 640
caractères, le découpage préfère les phrases puis les espaces ; la préparation
du clip suivant peut créer une pause. Tout le contenu est conservé. La dernière
page reste trois secondes. L’animation talk suit la lecture, sans superposer mouth.

Siwis medium fonctionne localement via Piper 2023.11.14-2, en PCM 22050 Hz.
Hortense reste disponible. Aucun service cloud ni téléchargement au démarrage.
L’acquisition autorisée est reproductible : scripts/Install-NaturalVoice.ps1
-AcceptDownload. Les SHA-256 sont épinglés. Build.ps1 vérifie les ressources et
les copie dans out/IRIS-engine-data, près du paquet autonome.

Interrompre, clic dans la bulle, fermeture, nouvelle réponse et arrêt d’urgence
annulent la lecture. Une seule lecture à la fois ; le push-to-talk attend la
fermeture audio avant le microphone. Le texte est envoyé en texte brut ou JSON
sérialisé, sans commandes. Les protections d’empreintes et de Job Object restent
actives. Aucun outil Windows n’est donné au modèle. Conversations et audio
restent en RAM ; aucun enregistrement micro ajouté.

## Vérification

- 52 contrôles du socle, 31 WinForms, 14 vocaux : progression selon les repères,
  graphèmes Unicode, arrêt/remplacement, chat et voix manuelle, alpha/police,
  pagination, format PCM 22050 Hz et refus de ce format dans l’ASR 16000 Hz.
- IRIS.VoiceTests --compare-tts produit deux WAV synthétiques et un rapport dans
  out/speech-comparison, sans microphone ni haut-parleur.
- IRIS.exe --self-check : Qwen réel, Siwis et bulle avec lecture simulée, puis
  Whisper sur fixture Windows 16000 Hz ; diagnostics/self-check.json.
- IRIS.exe --speech-check : courte phrase Siwis réellement jouée, mises à jour du
  texte et position du périphérique ; diagnostics/speech-check.json. Ce mode
  audible est explicite et exclu des tests ordinaires et de la CI.

Le naturel de la nouvelle voix reste à apprécier à l’écoute sur le poste utilisateur.

## Principaux fichiers modifiés

- src/IRIS.Desktop/Speech/BubbleArtwork.cs et SpeechBubbleWindow.cs : dimensions.
- src/IRIS.Desktop/Speech/SpeechTextFlow.cs et ReplySpeechController.cs : progression.
- src/IRIS.Infrastructure/Voice/PiperSynthesisProvider.cs : moteur Siwis vérifié.
- src/IRIS.Voice/CompositeSpeechProvider.cs, WindowsSpeechProviders.cs,
  WinMmAudio.cs : sélection, repères et position réelle de lecture.
- src/IRIS.Core/Contracts/Voice.cs, PcmAudio.cs, Services/SpeechTiming.cs : formats.
- src/IRIS.Desktop/Program.cs, VoiceWindow.cs, InstalledSelfCheck.cs : branchement.
- scripts/Install-NaturalVoice.ps1, Prepare-EngineAssets.ps1,
  Publish-Portable.ps1, Start-IRIS.ps1 et Lancer-IRIS.cmd : paquet 0.7.0.
- tests/IRIS.DesktopTests/SpeechChecks.cs et IRIS.VoiceTests/Program.cs : contrôles.
- Directory.Build.props, README.md, docs/VOICE.md, THIRD_PARTY_NOTICES.md : version et notices.

Les diagnostics du paquet 0.7.0 ont terminé avec passed=true. Le contrôle audible
signale 22050 Hz, position fournie par le périphérique et 11 changements du texte.
L’annulation pendant une véritable synthèse Piper termine aussi son processus.
