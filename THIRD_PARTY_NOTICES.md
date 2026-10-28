# Avis des composants tiers

Required Notice: Copyright 2026 Lachy (https://github.com/lachydotmcg)

| Composant | Provenance / licence constatée | Inclusion dans IRIS actuel |
|---|---|---|
| Sources Capri v0.7.2 | lachydotmcg/capri-desktop ; PolyForm Noncommercial 1.0.0, texte intégral dans LICENSE.md | Sources historiques conservées ; aucune compilation dans IRIS.sln |
| Share Tech Mono | Carrois Type Design / Ralph du Carrois ; SIL OFL 1.1 dans assets/fonts/OFL-ShareTechMono.txt | Embarquée sans modification pour la bulle ; chargée en privé, notice copiée avec la police |
| Sprites, icônes, WAV Capri | Droits spécifiques à vérifier ; ne pas supposer une autorisation des ayants droit du personnage | Conservés historiquement ; non embarqués |
| Avalonia 11.3.22 | Dépendance de cross/ ; licence exacte et transitives à revoir avant réutilisation | Non référencé par IRIS |
| llama.cpp / archive Vulkan b11669 historique | Référencé dans Engine, non repris par IRIS | Ni téléchargé ni inclus |
| sherpa-onnx | README Capri : Apache-2.0, à vérifier pour la version distribuée | Ni téléchargé ni inclus |
| Kokoro-82M | README Capri : Apache-2.0 pour les poids annoncés ; voix à contrôler séparément | Ni téléchargé ni inclus |
| Parakeet TDT 0.6B v2 | README / Ears : CC-BY-4.0, modèle anglais ; export sherpa | Ni téléchargé ni inclus |
| Autres modèles GGUF référencés dans Engine | Licences de chaque modèle/quantification non revues | Ni téléchargés ni inclus |
| .NET et Windows Desktop Runtime | Microsoft, version 10.0.14 x64 ; paquets officiels NuGet signés, MIT et notices tierces copiées dans licenses/ | Inclus dans la version autonome locale, aucune publication externe |
| SQLite système Windows (winsqlite3.dll) | Composant fourni et maintenu par Windows, chargé depuis System32 ; version observée 3.53.4 | Utilisé pour les souvenirs ; aucune DLL copiée ou redistribuée |
| llama.cpp b11901 Vulkan x64 | ggml-org, MIT ; archive SHA-256 épinglée dans MODELS.md | Acquis localement après accord ; pas de redistribution |
| LLVM/OpenMP fourni dans l’archive | Notice LICENSE-LLVM-OpenMP incluse par le fournisseur | Notice copiée dans le dossier local licenses ; obligations des binaires à réconcilier avant distribution |
| Qwen3-4B-Instruct-2507 Q4_K_M | Modèle Qwen et quantification Unsloth, Apache-2.0 annoncé ; révision et SHA-256 dans MODELS.md | Acquis localement après accord ; texte Apache conservé, pas de redistribution |

Cette liste distingue inventaire historique et dépendances réellement livrées.
Ce n’est pas encore un SBOM de distribution. Ne pas déclarer les modèles ou les
médias approuvés sur la seule base des annonces du README historique.

## Dépendances vocales du jalon 0.4.0

- System.Speech 10.0.14 : Microsoft, MIT, package NuGet exact et verrouillé par
  packages.lock.json ; signatures auteur Microsoft et dépôt NuGet vérifiées.
  Source dotnet/runtime au commit 0427b9abbead3fa64c6655eb4290c537397131fa.
- Moteur et voix SAPI : composants installés de Windows ; aucune voix copiée ou
  redistribuée. Hortense Desktop sert de secours sur le poste de développement.
- whisper.cpp b5454 : ggml-org, MIT ; archive officielle acquise après accord,
  SHA-256 épinglée dans VOICE.md et le script d’installation. Licence conservée
  localement dans licenses/whisper.cpp-b5454-MIT.txt.
- Whisper Small multilingue converti GGML : ggerganov/whisper.cpp, origine OpenAI
  Whisper ; MIT annoncé pour code/poids. Révision et empreinte dans VOICE.md.
  Poids acquis localement, aucune redistribution. Avant distribution, conserver
  aussi les notices applicables aux poids d’origine et aux conversions.

La synthèse neuronale a été acquise ultérieurement au jalon 0.7.0 ci-dessous.

## Personnage IRIS — 0.5.0

Les 25 spritesheets et animations.json ont été fournis par l’utilisateur et
intégrés tels quels. Aucun média historique dans assets/readme, aucune police ou
voix historique n’est ajouté au livrable pour cette intégration. Les principes de
Clip/Advance et le présentateur alpha DrawLayered de Capri sont adaptés au nouveau
module Desktop/Animation ; la licence et les mentions du code dérivé restent
applicables. La provenance utilisateur des PNG est consignée, sans leur attribuer
une nouvelle licence ou prétendre à une création du codeur. Pas de publication.

## Révision 0.5.2 — paquet complet

La distribution locale comprend désormais out/IRIS-engine-data, séparé des profils
privés. Qwen3-4B-Instruct-2507 Q4_K_M, Whisper Small et leurs moteurs épinglés sont
copiés et vérifiés depuis les acquisitions déjà autorisées ; leurs versions,
empreintes et notices restent celles documentées dans MODELS.md et VOICE.md.
Aucune nouvelle acquisition et aucune publication externe. Les notices de ces
ressources se trouvent aussi dans out/IRIS-engine-data/licenses.

## Voix Siwis et Piper — 0.7.0

Acquisition locale explicitement autorisée, sans publication externe :
- Piper 2023.11.14-2, Michael Hansen, MIT :
  https://github.com/rhasspy/piper/blob/2023.11.14-2/LICENSE.md
- Voix française Siwis medium, dépôt rhasspy/piper-voices annoncé MIT, modèle
  dérivé des données SIWIS CC-BY 4.0. Carte originale conservée :
  https://huggingface.co/rhasspy/piper-voices/blob/d5de91c972ba5a13cea0e77a63fda1623faac2c1/fr/fr_FR/siwis/medium/MODEL_CARD
  https://creativecommons.org/licenses/by/4.0/
- espeak-ng embarqué, GPL-3.0 ; texte dans licenses/espeak-ng-GPL-3.txt.
  Source : https://github.com/espeak-ng/espeak-ng
- ONNX Runtime embarqué, Microsoft, MIT ; DLL observée
  1.14.20230224.5.c57cf37 ; texte MIT dans licenses/onnxruntime-MIT.txt.

Archives, modèle et configuration sont épinglés par SHA-256 dans VOICE.md et les
scripts. Les dépendances natives restent celles de l’archive vérifiée. Les notices
sont copiées dans out/IRIS-engine-data/licenses. Préserver ces attributions et
réexaminer les obligations de redistribution (dont espeak-ng GPL et sources)
avant une publication externe. Aucun service cloud ajouté.
