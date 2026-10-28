# Tests — 27 octobre 2026

Jalon 0.6.0 : 95 contrôles ordinaires réussis (52 socle, 30 WinForms, 13 voix),
compilation finale sans erreur ni avertissement. Démarrage réel de IRIS.exe avec
les assets, puis CloseMainWindow : sortie normale code 0. Aucun processus
llama-server ou whisper-cli lancé lors de cet essai. Rendu visuel de la
prévisualisation inspecté dans out/ui/animations.png. Voir ANIMATIONS.md.

## Suite ordinaire

`./scripts/Build.ps1` compile la solution Release puis exécute :

- 52 contrôles de permissions, schémas, refus/confirmation, expiration, rejeu,
  injections simulées, arrêt actif/en file, paramètres, instance unique, journal,
  protocole HTTP local, conversation et mémoire SQLite (consentement, CRUD,
  révisions, rétention, JSON, injection SQL, corruption/version future,
  export sans écrasement, annulation/rollback et contexte du modèle).
- 30 contrôles WinForms : démarrage, horloge, paramètres/persistance, panneau
  mémoire avec confirmations, conversation avec double IA, confirmations natives,
  annulation, arrêt et réouverture.
- 13 contrôles vocaux : PCM/WAV, formats rejetés, silence, gain/saturation, WER,
  consentement, relâchement, annulation et libération des buffers. Les contrôles
  WinForms vérifient aussi le transfert manuel et l’arrêt de pilote non confirmé.
- Neuf des contrôles WinForms concernent les animations : 25 spritesheets réelles,
  alpha et proportions, FPS/boucles, erreurs et chemins ambigus, annulation et arrêt,
  prévisualisation, ancrage multi-écran, transitions du personnage, dessin natif
  sur fenêtre du test et accès sans modèle depuis le menu principal.

SDK .NET 10.0.403, Windows Desktop Runtime 10.0.14. Résultat observé : compilation
sans erreur ni avertissement et tous ces contrôles réussis. La CI distante n’a
pas été exécutée ici et n’est pas déclarée réussie.

Les suites sont des exécutables autonomes, sans package de framework de test.
`dotnet test` seul ne les exécute pas. Commandes séparées :

```powershell
dotnet run --project tests/IRIS.SecurityTests -c Release
dotnet run --project tests/IRIS.DesktopTests -c Release -- out/ui
```

Aucun vrai écran capturé, microphone enregistré ou clavier piloté. Les contrôles
utilisent des dossiers temporaires et les fenêtres qu’ils créent, rendues avec
DrawToBitmap. Les raccourcis sont simulés par IHotkeyRegistrar. Les paramètres du
profil réel ne sont pas modifiés par ces suites. Les sources Capri ne sont jamais lancées.

Un mode de secours Roslyn/PowerShell .NET 10 reste disponible :
`pwsh -NoProfile -File ./scripts/Test-Security.ps1 -UsePowerShellCompiler`.
Il ne valide pas MSBuild, les frontières d’assemblages ou WinForms.

## Essais réels explicitement autorisés

Les fichiers modèle/runtime ont été acquis après accord. Ces essais ne font pas
partie de Build.ps1 ni de la CI par défaut :

```powershell
dotnet run --project tests/IRIS.IntegrationTests -c Release -- --local-model
dotnet run --project tests/IRIS.IntegrationTests -c Release -- --local-memory
dotnet run --project tests/IRIS.DesktopTests -c Release -- out/ui-real --local-model
```

Observé : fichiers vérifiés et verrouillés, modèle chargé, réponse française
réelle, écoute loopback liée au PID et requête authentifiée, arrêt d’urgence,
fermeture de conversation sans moteur restant, puis arrêt brutal d’un propriétaire
de test entraînant la fin du moteur enfant via le Job Object. Le test de crash
termine uniquement le propriétaire qu’il a créé, sans appel Kill(entireProcessTree)
sur celui-ci ; sinon il ne vérifierait pas le mécanisme Job Object.

Artefacts locaux ignorés par Git : out/local-model-smoke.json, out/ui/results.txt,
out/ui/*.png et out/ui-real/conversation-real.png. Les rendus réels affichent la
réponse produite ; les rendus de la suite ordinaire indiquent leur fournisseur simulé.
Le premier essai est synthétisé dans BENCHMARKS.md ; il ne prouve pas une qualité
générale du français ni une performance soutenue.

Le point d’entrée de production IRIS.exe a aussi été ouvert puis fermé
gracieusement : titre IRIS attendu, demande de fermeture acceptée, code de sortie 0.
Ce contrôle a révélé et corrigé un lanceur qui masquait la fenêtre ; Start-IRIS.ps1
lance désormais directement l’apphost graphique visible. Aucune frappe n’a été
injectée. Les données de conversation et paramètres du profil ne sont pas modifiés
par ce contrôle de démarrage ; un fichier de verrou vide peut rester dans le dossier IRIS.

## Reste à vérifier

Raccourci physique, DPI multiples, accessibilité/lecteurs d’écran, absence d’accès
réseau sortant observée par trace système, repli CPU réel, sessions longues,
corruption du modèle en copie isolée et diagnostic de pilote absent. Les schémas
et limites HTTP sont testés avec un handler mémoire, sans serveur externe.
Tests DPAPI, rappels/agenda, ASR/TTS et publication
viendront avec les composants correspondants. Ne pas présenter ces capacités
comme opérationnelles avant leur implémentation et leur vérification.

## Souvenirs SQLite (0.3.0)

Le module Memory ajoute les vérifications de défaut désactivé, consentements
séparés, CRUD/persistance, corrections périmées, injections SQL littérales,
recherche française accentuée, rétention/expiration, imports invalides et conflits
sans écrasement, exports nouveaux, champs de secrets usuels, concurrence,
corruption/version future conservée, budget Unicode et rollback après une écriture.
Le panneau est testé avec une base temporaire ; le journal technique est vérifié
sans contenu des souvenirs. Le rendu de sa fenêtre est `out/ui/memory.png`.

L’option réelle `--local-memory` a été exécutée avec les fichiers IA déjà acquis.
Une base temporaire a enregistré une préférence synthétique, puis a été réouverte :
le modèle a répondu `azur-iris-742`. Après suppression et invalidation du contexte,
il a répondu « Je ne sais pas. ». Le résultat figure dans
`out/local-memory-smoke.json`, sans donnée personnelle. La base a été supprimée
et le processus arrêté. Ce test ne mesure pas une fiabilité générale du rappel.

Les sélecteurs de fichiers natifs restent à vérifier manuellement ; leurs routines
de transfert sont testées séparément sans accès à de vrais fichiers personnels.

## Voix — validation du jalon 0.4.0

Build.ps1 exécute aussi IRIS.VoiceTests (10 contrôles, audio simulé). La comparaison
réelle des moteurs reste une commande explicite :

```powershell
dotnet run --project tests/IRIS.VoiceTests -c Release -- --benchmark
```

Les deux moteurs reçoivent les mêmes six clips synthétisés par SAPI, jamais un
microphone. Chaque résultat contient référence, transcription, WER et durée.
Un moteur indisponible, une sortie vide ou une erreur fait échouer la comparaison.
Aucune lecture sur les haut-parleurs. Le WAV d’exemple ne contient que du texte de
fixture synthétisé. Reste manuel : périphérique exact, gain/saturation, relâchement,
perte de focus, déconnexion matérielle, arrêt, fermeture et écoute subjective TTS.

L’annulation réelle de Whisper pendant la présence du processus a également été
vérifiée : annulation propagée et processus terminé en moins de cinq secondes.
Le test n’ouvre aucun périphérique audio.

## Validation du lancement autonome

Lancer-IRIS.cmd et Start-IRIS.ps1 ont chacun lancé la version autonome avec PATH
limité à System32, DOTNET_ROOT/DOTNET_ROOT_X64 pointant vers un dossier inexistant
et DOTNET_MULTILEVEL_LOOKUP=0. Les modules chargés hostfxr.dll, hostpolicy.dll et
coreclr.dll proviennent tous de out/IRIS-portable-win-x64. Fermeture normale : code
0 dans les deux cas. Aucun modèle lancé. Les 84 contrôles ordinaires passent avec
le verrou NuGet incluant win-x64 ; compilation et publication autonomes réussies.

## Corrections saisie et ASR — 0.5.1

Les tests WinForms maintiennent le chargement puis la génération en attente avec
des doubles : saisie activée et modifiable avant/pendant, transfert de brouillon
vocal pendant le chargement et conservation du nouveau brouillon après réponse ou
annulation. Le compositeur multiligne et son libellé sont rendus dans
out/ui/conversation.png. Test réel du modèle depuis la fenêtre de conversation
réexécuté : chargement, réponse française et fermeture sans moteur restant.

Trois contrôles du signal : phrase faible conservée/amplifiée sans écrêtage ni
mutation de la source, pause interne longue conservée et composante continue
seule rejetée. Mode --quiet-regression réel avec Whisper décrit dans VOICE.md.
Lancement autonome 0.5.1 testé via les deux lanceurs, avec PATH/DOTNET_ROOT invalides,
modules runtime locaux et sortie normale code 0. Aucun microphone humain enregistré.

## Validation du paquet 0.5.2

L’exécutable final a exécuté --self-check avec les ressources de out/IRIS-engine-data,
indépendamment du cache utilisateur. Rapport : modèle CPU chargé et réponse réelle,
Whisper dans la fenêtre vocale avec audio synthétique, transfert au brouillon,
arrêt du processus ; passed=true. La résolution avec séparateur final et profil
vide est testée. La question exacte de la capture « quel heure est il ? » obtient
l’horloge via le pipeline de permissions sans modèle ; une instruction intégrée
à une autre phrase ne route pas vers les actions. Voir DEPLOYMENT_CHECK.md.

## Test de la lecture — 0.6.0

Les six groupes de tests SpeechChecks valident la connexion chat/voix/bulle,
image RGBA, police privée réelle, pagination, annulation, remplacement, absence
 de chevauchement, mute/relecture, invalidation mémoire et drainage avant capture.
Le paquet --self-check utilise réellement SAPI avec une sortie factice ; le
mode explicite --speech-check a aussi joué une courte phrase sur la sortie audio
réelle, sans microphone. Les deux rapports sont passed=true. Voir SPOKEN_REPLIES.md.
