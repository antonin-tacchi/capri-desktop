# IRIS — Intelligent Responsive Interface System

Assistant personnel Windows 11, en C# / .NET 10 / WinForms, dérivé de Capri
v0.7.2. Usage personnel, expérimental et non commercial.

Le jalon 0.7.1 compile et permet une véritable conversation française avec
Qwen3-4B-Instruct-2507 Q4_K_M, exécuté localement par llama.cpp Vulkan.
Le modèle est chargé sur demande. Il ne dispose d’aucun outil Windows.
Une mémoire SQLite consentie conserve les souvenirs ajoutés explicitement ; le
contexte de conversation reste en RAM et le MVP complet est en cours.

La fenêtre **Voix** propose le push-to-talk, Whisper Small français hors ligne
ou le moteur Windows, une transcription corrigeable et une voix système féminine
de secours. Le microphone exige un consentement par ouverture et le maintien du
bouton. **Placer le texte dans le chat** prépare un brouillon ; **Envoyer** reste
un clic séparé. Le microphone réel et le naturel de la voix restent à évaluer
manuellement. Voir [VOICE](docs/VOICE.md).

Les réponses du chat sont lues automatiquement par la voix française Siwis.
La bulle compacte fournie dans assets/bulle_discution.png affiche le texte progressivement
avec Share Tech Mono, près du personnage. Les réponses longues sont paginées sans
troncature. **Interrompre** ou cliquer sur la bulle arrête la lecture ; **Relire**
rejoue la dernière réponse. Décochez **Lire les réponses à voix haute** pour couper
la lecture automatique. Volume, vitesse et voix se règlent dans **Voix**.
Voir [Réponses parlées et bulle](docs/SPOKEN_REPLIES.md).

Les spritesheets IRIS sont intégrées : personnage transparent indépendant sur
le bureau, clignement, repos, sommeil/réveil, parole liée à la lecture audio et
expressions. **Animations** ouvre la prévisualisation sans charger l’IA : sélection,
lecture/arrêt, FPS du manifeste et fréquence affichée. Le menu droit du personnage
propose déplacement explicite, danse et disparition ; glisser le personnage le
déplace. Marche et poses perch restent des prototypes. Voir [ANIMATIONS](docs/ANIMATIONS.md).

## Démarrer sur ce poste

Double-cliquez **Lancer-IRIS.cmd**, ou ouvrez directement
`out\IRIS-portable-win-x64-0.7.1\IRIS.exe`. Cette version contient son runtime .NET 10
x64 et Windows Desktop : aucun SDK ni .NET global n’est requis pour la lancer.
Conservez le dossier entier avec ses DLL et assets, ainsi que **out\IRIS-engine-data**
à côté : ce second dossier contient Qwen, Whisper, les moteurs et les archives
de vérification. Une application seule sans ce dossier n’est pas une distribution
complète. **Build.ps1** régénère cette
version autonome après les tests ; **Start-IRIS.ps1** la choisit en priorité sans
exécuter Initialize-DotNet.ps1.

Le SDK officiel Microsoft 10.0.403 et les fichiers IA ont été installés après
vos autorisations, avec vérification des empreintes. Lancez :

```powershell
./scripts/Start-IRIS.ps1
```

Dans IRIS : **Conversation**, puis **Charger IA**. Saisissez votre message et
cliquez **Envoyer**. En cas d’échec Vulkan, cochez **CPU uniquement** avant de
recharger. **Interrompre** annule la génération et décharge le moteur.
Fermer la conversation décharge également le modèle.

Les questions exactes comme « quel heure est il ? » et « quelle est la date ? »
utilisent l’horloge locale en lecture seule, via les permissions existantes, même
sans modèle chargé. Les autres messages nécessitent **Charger IA**. Le bouton
**Diagnostic IA** indique le dossier réellement utilisé et les fichiers absents.

Écrivez dans la zone blanche **Votre message**, sous l’historique en lecture seule.
La saisie reste disponible pendant le chargement et la génération ; un nouveau
brouillon n’est pas effacé par la réponse précédente. Entrée envoie, Maj+Entrée
ajoute une ligne. L’envoi reste explicite et nécessite le modèle chargé.

L’humour est réglable dans les paramètres (sobre, discret ou sarcasme doux).
L’initiative reste passive. Aucun historique de conversation n’est enregistré.
**Effacer la session** vide le contexte et l’affichage de la fenêtre.

Pour les souvenirs : **Mémoire → Autoriser la mémorisation explicite → Appliquer
les choix**, puis confirmer. Ajoutez chaque souvenir volontairement. L’option
**Utiliser ces souvenirs dans les réponses de l’IA locale** est un consentement
séparé, désactivé par défaut. Le panneau permet recherche, correction, suppression,
durée de conservation et export/import JSON. Voir [MEMORY](docs/MEMORY.md).

## Compiler et tester

Prérequis : Windows 11 x64 et SDK .NET 10. Aucun Docker, Python ou Node.js.

```powershell
./scripts/Build.ps1
```

Cette commande compile les douze projets puis exécute 52 contrôles du socle,
30 contrôles WinForms et 13 contrôles vocaux, avec doublures pour le modèle,
les raccourcis et les périphériques audio. Aucun microphone ni haut-parleur réel
n’est utilisé par ces tests.
Aucune capture du vrai écran ni frappe clavier n’est utilisée : les images de QA
sont des rendus des fenêtres créées par les tests.

Les essais du modèle réel restent explicites :

```powershell
dotnet run --project tests/IRIS.IntegrationTests -c Release -- --local-model
dotnet run --project tests/IRIS.IntegrationTests -c Release -- --local-memory
dotnet run --project tests/IRIS.DesktopTests -c Release -- out/ui-real --local-model
```

Ils ont été exécutés sur ce poste : génération française, écoute loopback liée
au PID, fermeture, arrêt d’urgence, arrêt brutal du propriétaire et souvenir
synthétique retrouvé puis supprimé dans SQLite. Voir
[TESTING](docs/TESTING.md) et [BENCHMARKS](docs/BENCHMARKS.md).

Pour un autre poste, l’acquisition du modèle est séparée et volontaire :

```powershell
./scripts/Install-LocalModel.ps1 -AcceptDownload
```

Lire [MODELS](docs/MODELS.md) avant de télécharger environ 2,54 Go.
Aucun téléchargement n’est effectué par l’application au démarrage.

## Vie privée et limites

Données sous `%LOCALAPPDATA%\IRIS\`, indépendantes de Capri. Client IA uniquement
sur `127.0.0.1`, authentifié par clé éphémère, sans proxy ni redirections.
Archive, modèle et fichiers natifs vérifiés et verrouillés pendant l’utilisation.
Processus lié à un Job Object dès sa création ; ce mécanisme gère sa durée de vie
et ne constitue pas une sandbox Windows.

Le microphone reste inactif sans consentement et maintien explicites. Capture
d’écran, cloud, mise à jour distante, shell et contrôle générique du
bureau sont absents du chemin IRIS. Le registre propose la date et l’heure en
lecture seule ; les modifications de mémoire sont réservées à l’interface avec
confirmation native. Le texte du modèle ne déclenche aucune action.

L’arrêt d’urgence bloque la session jusqu’au redémarrage : bouton ou
Ctrl+Alt+F12, configurable entre F6 et F12. La frappe physique du raccourci reste
à vérifier manuellement ; les tests emploient une doublure.

La base des souvenirs et les exports sont non chiffrés. N’y stockez aucun secret.
La suppression concerne la base active, sans garantie d’effacement physique des
copies, sauvegardes ou supports.

Non implémentés : naturel vocal validé par l’utilisateur, rappels, agenda,
réponse en flux, profils de performance complets et distribution publiée.
Aucun secret persistant n’est accepté ; SecretStore DPAPI reste nécessaire avant
l’ajout de clés persistantes. Pas de synchronisation cloud.

## Documentation et provenance

- [État des lots et migration](docs/MIGRATION.md)
- [Audit Capri](docs/CAPRI_AUDIT.md), [architecture](docs/ARCHITECTURE.md)
- [Sécurité](docs/SECURITY.md), [menaces](docs/THREAT_MODEL.md)
- [Modèles](docs/MODELS.md), [voix](docs/VOICE.md), [installation du SDK](docs/SDK_SETUP.md)
- [Mémoire SQLite et politique des données](docs/MEMORY.md)
- [Animations et prévisualisation du personnage](docs/ANIMATIONS.md)
- [Changements](docs/CHANGELOG.md), [conditions de réutilisation](docs/LEGAL.md), [avis tiers](THIRD_PARTY_NOTICES.md)
- [README Capri historique](docs/CAPRI_README.md)

Référence Capri : `dd7a2a4c4c043ef68e3583c1f6bc41d5e9a8222a`, tag `v0.7.2`,
[projet de Lachy](https://github.com/lachydotmcg/capri-desktop).
Les sources historiques sont conservées et exclues de `IRIS.sln` ; aucun média
Capri n’est embarqué. Aucun push ni publication n’a été effectué.

Required Notice: Copyright 2026 Lachy (https://github.com/lachydotmcg)

Licence [PolyForm Noncommercial 1.0.0](LICENSE.md), conservée intégralement.
Les licences des modèles et moteurs restent distinctes.
