# Audit initial de Capri — 27 octobre 2026

Suivi : le SDK manquant décrit lors de cet audit a ensuite été installé avec
l’accord de l’utilisateur. Le jalon 0.2.0 compile et dispose d’une conversation
locale testée ; voir SDK_SETUP.md, MIGRATION.md et TESTING.md.

## Périmètre et provenance

Audit statique du dépôt local, propre avant intervention, HEAD
`dd7a2a4c4c043ef68e3583c1f6bc41d5e9a8222a`, tag `v0.7.2`.
Remote constaté : `https://github.com/antonin-tacchi/capri-desktop.git`.
Source amont déclarée : `https://github.com/lachydotmcg/capri-desktop`.
Branche initiale : `development/iris-2.0` ; travail isolé sur
`codex/iris-secure-foundation`. Aucun push, téléchargement de binaire ou de modèle,
ni lancement de Capri. Historique et sources historiques conservés.

Ont été lus : README, licence, CONTRIBUTING, script Windows, workflows, projet
cross et les parties pertinentes des fichiers recensés ci-dessous. Le README
historique est conservé intégralement dans `CAPRI_README.md`.
Il ne s’agit ni d’un audit exhaustif ligne par ligne ni d’une validation dynamique.

## Cartographie et décisions

| Sources | Comportement constaté | Décision IRIS |
|---|---|---|
| `app/Capri.cs` | Main STA, mutex par utilisateur, WinForms, compagnon, paramètres, orchestrateur, mises à jour et nombreux smoke tests dans une même classe | Nouveau point d’entrée ; aucune référence à cette classe |
| `app/Brain.cs` | Conversation, appels HTTP locaux/cloud, outils, mémoire Markdown `me.md`, historique JSON | Réécrire les contrats et le contexte ; étudier le protocole local séparément |
| `app/Computer.cs` | UI Automation, OCR/écran, coordonnées, frappe, souris, curseur ; filtres lexicaux pour les opérations sensibles | Exclu ; aucune capacité de contrôle générique dans le jalon |
| `app/Hands.cs` | Dispatch d’outils, découverte d’applications, URI/chemins, médias, fenêtres, arrêt/redémarrage | Exclu ; registre explicite de gestionnaires C# |
| `app/Engine.cs` | Téléchargements taille/SHA-256, llama.cpp Vulkan, choix modèles, repli CPU, processus et Job Object | Réutilisation conceptuelle possible après extraction et revue |
| `app/Ears.cs` | Capture WinMM, périphériques, System.Speech, sherpa-onnx natif, modèle Parakeet anglais et extraction tar | Capture à réécrire ; pas de reconnaissance française validée |
| `app/Voice.cs` | ElevenLabs HTTP, synthèse locale sherpa/Kokoro, lecture audio, DPAPI CurrentUser côté Windows | Ne pas reprendre le cloud ; évaluer séparément les voix françaises |
| `app/Agent.cs` | Création/configuration d’agent et outils ElevenLabs, URL signée, WebSocket et audio | Exclu du chemin IRIS |
| `app/Dictation.cs` | Windows.Media.SpeechRecognition et dictée WinRT | Revoir disponibilité hors ligne et consentement ; pas repris |
| `app/Mainframe.cs` | Paramètres liés au compagnon, clés, cloud, mises à jour | Interface IRIS indépendante |
| `app/Setup.cs` | Extraction/installateur, registre HKCU de désinstallation, raccourcis, installation vocale, mise à jour et suppression | Exclu ; portable prévu ultérieurement |
| `app/Talk.cs`, `app/Ledges.cs` | Rendu, polices embarquées, affichage et énumération des fenêtres | Idées de rendu à extraire plus tard ; aucun accès aux fenêtres dans IRIS |
| `ci/build-windows.ps1` | csc Framework, WinRT/SDK Windows, embarque images/sons/police, fabrique et peut lancer application et installateur de test | Script historique bloqué ; utiliser `scripts/Build.ps1` |
| `cross/Capri.Cross.csproj` | .NET 10 et Avalonia 11.3.22 ; partage Brain/Engine/Hands/Voice/VoicePack | Confirme une migration partielle existante, mais ne constitue pas le WinForms IRIS |
| `site/`, `worker/` | Site et composant web annexes | Hors solution et distribution IRIS ; revue approfondie encore nécessaire si réutilisation |

## Risques prioritaires

1. `Computer.cs` inclut des instructions autorisant envoi, suppression et installation
   lorsque le travail utilisateur semble les demander. Les regex anglaises `Risky`,
   `Told`, etc. et les prompts ne sont pas une frontière de sécurité. Les titres et
   contenus de fenêtres demeurent non fiables, même lorsque le prompt le précise.
2. `Hands.Do` distribue directement des noms d’outils vers des effets Windows.
   `Hands.Launch` accepte chemins et protocoles, et utilise aussi `explorer.exe`
   avec `shell:AppsFolder`. Pas de liste d’applications explicitement approuvées
   par l’utilisateur au sens de la politique IRIS.
3. `Hands.Turn` invoque `shutdown.exe` ; fermeture de fenêtre, touches et capture
   ne sont pas des opérations informatives. Les contrôles actuels ne doivent pas
   être copiés tels quels.
4. `Capri.cs` vérifie périodiquement les mises à jour depuis sa boucle ; l’updater
   télécharge un installateur et le lance. Certaines vérifications de hash et de
   signature existent : cela ne justifie pas une mise à jour distante implicite.
5. Les chemins cloud dans Brain, Voice, Agent et la capture d’écran peuvent
   transmettre texte, audio ou image selon la configuration. IRIS ne les importe pas.
6. DPAPI existe déjà pour les clés Windows (`Voice.Protect/Unprotect`) ; il ne
   chiffre pas toute la mémoire/historique. Pas de migration automatique des données.
7. Les téléchargements Engine ont des empreintes épinglées et une écoute IPv4
   loopback : points positifs. Le lancement observé n’ajoute pas de clé API au
   serveur. Un autre processus local reste une menace. Le port libre est sondé puis
   relâché avant lancement : course possible. Revalider ces points avant activation.
8. Le Job Object utilise kill-on-close pour gérer la durée de vie ; ce n’est pas
   une sandbox. La capture/exclusion de fenêtres sensibles ne garantit pas non plus
   une isolation.

## Processus et accès Windows recensés

- Llama-server, géré par Engine ; création de Job Object et terminaison.
- `tar.exe` pour extraire le modèle ASR dans Ears.
- `explorer.exe`, applications/URI résolues par Hands ; `shutdown.exe`.
- Navigateur via `Process.Start(page)` ; `notepad.exe` pour la mémoire.
- Installateur de mise à jour, relance de Capri et copie temporaire du
  désinstallateur dans Setup.
- Compilation historique : `csc.exe`, exécutable Capri et installateur avec options
  de vérification. Ces derniers ne sont pas exécutés pendant cet audit.
- Win32 : fenêtres/premier plan, simulation d’entrées, curseur, captures GDI,
  raccourcis globaux, alimentation/verrouillage, audio WinMM ; OCR/dictée WinRT,
  UIAutomation, médias et registre selon le composant.
- Manifeste historique `asInvoker`, `uiAccess=false` ; registre installateur dans
  HKCU. Aucune nécessité de privilèges administrateur démontrée pour le MVP.
  Le contrôle de fenêtres élevées ne doit pas être contourné par élévation.

## Réseau et fichiers

Destinations trouvées : localhost Ollama (`127.0.0.1:11434`) et llama-server,
OpenAI, Anthropic, DeepSeek, NVIDIA, ElevenLabs HTTP/WebSocket, GitHub releases,
Hugging Face (modèles), liens d’aide/Discord, recherche DuckDuckGo et liens vers
YouTube/Spotify/Google/Twitch/Reddit/Wikipedia/Roblox/GitHub depuis Hands.
Ce recensement statique n’atteste pas toutes les destinations effectives ni
l’absence de télémétrie d’un binaire natif. Aucun de ces clients n’est lié à IRIS.

Données historiques sous `%LOCALAPPDATA%\Capri` : `settings.json` et fichier
temporaire `.new`, mémoire `me.md`, historique sérialisé par Brain, packs vocaux,
modèles, archives et fichiers partiels, moteur extrait, marqueur CPU, cache de
mise à jour et marqueurs d’installation/curseur. Setup peut créer des raccourcis
et supprimer ses fichiers installés. Les tests historiques produisent de nombreux
artefacts dans leur dossier de smoke. IRIS utilise un dossier distinct et ne les lit pas.

## Ressources, dépendances et licence

`LICENSE.md` contient PolyForm Noncommercial 1.0.0 et la mention requise
Copyright 2026 Lachy. La licence et les avis sont conservés. L’usage personnel
non commercial demandé correspond aux finalités décrites dans ce texte ; les droits
sur les médias tiers restent à établir indépendamment. Voir `LEGAL.md` et les notices.

Polices : licence OFL locale présente. README annonce Kokoro Apache-2.0,
sherpa-onnx Apache-2.0 et Parakeet CC-BY-4.0 ; ces annonces ne remplacent pas la
vérification des versions et des fichiers distribués. Sprites, voix WAV, icône et
références Cyberpunk ne sont pas réputés libres de droits. Aucun média Capri
n’est embarqué dans les nouveaux projets. Aucun modèle ni runtime natif n’est ajouté.

## Tests existants et limites

Smoke tests intégrés surtout à `Capri.cs`, vérification installateur dans Setup,
tests cross dans `cross/Smoke.cs`, CI Windows/Linux/macOS. Certaines vérifications
emploient des doubles, d’autres peuvent capturer réellement l’écran ou charger
la dictée du poste : ne pas lancer cette suite sur le poste principal pendant l’audit.
Les messages PASS historiques ne prouvent aucun succès sur ce poste. Aucun de
ces tests n’a été exécuté ici.

## Obstacles .NET 10 et inconnues

Pas de SDK `dotnet` dans PATH ni dans les deux dossiers Program Files usuels.
PowerShell 7.6.5 embarque toutefois .NET 10.0.11 et Roslyn : il permet les tests
du nouveau code sans interface, pas une compilation MSBuild/WinForms.
La découverte des outils n’est pas un inventaire exhaustif des disques.

Migration à examiner : System.Speech, références WinRT/Windows.winmd,
System.Runtime.WindowsRuntime, marshaling P/Invoke, durée de vie des handles,
DllImport sherpa et chemins de chargement, threads UI, DPI, transparence et
ressources. HttpWebRequest est ancien ; le futur client utilisera HttpClient
avec proxy/redirections maîtrisés et destinations explicitement autorisées.

Non vérifiés : fonctionnement dynamique original, qualité française, compatibilité
réelle Radeon/Vulkan, temps/ressources, licences de chaque modèle et média,
WinForms IRIS sur le poste. Aucune mesure de performance ni recommandation
définitive de modèle n’est produite.

## Suite concrète

Voir `MIGRATION.md`. Le prochain verrou est la compilation .NET 10 complète,
puis la vérification manuelle du démarrage, paramètres, raccourci et fermeture.
Les lots IA, voix et contrôle Windows restent fermés tant que cette validation
et les protections propres à ces capacités ne sont pas terminées.
