# Voix française — jalon 0.5.1

Reconnaissance et synthèse locales implémentées. Les tests automatiques emploient
l’audio synthétique ou des doublures ; aucun microphone ni haut-parleur réel n’a
été ouvert. La capture et la lecture matérielles restent à vérifier manuellement.
La voix système est un secours ; son naturel n’a pas été validé par écoute humaine.

## Utilisation

Ouvrir **Voix**, choisir microphone et moteur, puis cocher l’autorisation valable
pour cette fenêtre. Maintenir le bouton ou Espace lorsque le bouton a le focus.
Relâcher ferme la capture avant la transcription. Perte de focus, annulation,
fermeture et arrêt d’urgence interrompent la capture. Limite : 30 secondes par
pression. Une pause ou une voix faible ne termine plus la prise pendant le
maintien. La répétition automatique d’Espace ne démarre pas une autre prise sans
relâchement.

Niveau RMS et saturation visibles. Gain logiciel 50–200 %, seuil parole 0,1–20 %.
Corriger la transcription, cliquer **Placer le texte dans le chat**, puis envoyer
séparément. Une transcription ne déclenche aucune action Windows. Un brouillon
existant différent n’est pas écrasé.

Les préférences de périphérique, moteur, gain, seuil, volume et vitesse sont
persistées ; le consentement ne l’est jamais. Un périphérique mémorisé absent
n’est pas remplacé silencieusement. Aucun téléchargement depuis l’application.
Voix féminine française SAPI disponible ici : Microsoft Hortense Desktop. Les voix
OneCore ne sont pas forcées par le registre. Volume 0–100 et vitesse -5 à +5 ;
texte TTS limité à 700 caractères et 120 secondes.

## Moteurs et acquisition

Windows utilise le moteur français hors ligne installé (MS-1036-80-DESK ici),
avec un flux PCM explicitement fourni, sans microphone par défaut. System.Speech
10.0.14 est verrouillé par packages.lock.json ; signatures Microsoft et NuGet
vérifiées. Le texte de synthèse est brut, jamais interprété comme SSML.

Whisper Small multilingue et whisper.cpp b5454 x64 CPU acquis après autorisation.
Pour une autre installation :

```powershell
./scripts/Install-SpeechModel.ps1 -AcceptDownload
```

| Fichier | Taille en octets | SHA-256 |
|---|---:|---|
| whisper-bin-x64.zip b5454 | 8 928 640 | `6ba69e3482d7826214f90a6a9c84ca07782aec1e1d0c6a7c30c994fd5d816ccb` |
| ggml-small.bin | 487 601 967 | `1be3a9b2063867b937e64e2ec7483364a79917e157fa98c5d94b5c1fffea987b` |

Sources : [whisper.cpp b5454](https://github.com/ggml-org/whisper.cpp/releases/tag/b5454),
[poids GGML révision 5359861c739e955e79d9a303bcbc70fb988958b1](https://huggingface.co/ggerganov/whisper.cpp/tree/5359861c739e955e79d9a303bcbc70fb988958b1).
Code et poids annoncés sous MIT ; aucune redistribution dans ce jalon.
Licence du moteur conservée dans le dossier local licenses.

Avant chaque transcription Whisper, archive, modèle et tous les fichiers extraits
sont vérifiés puis verrouillés. Processus CPU (4 threads, langue française), WAV
par pipe en entrée, texte par pipe en sortie ; aucun fichier audio temporaire ni
serveur. Délai 90 secondes, texte 4 096 caractères, sortie brute 32 Kio. Job Object
lié au parent et à l’annulation ; aucune écoute TCP autorisée pour ce PID.

## Vie privée et arrêt

Capture WinMM en PCM 16 kHz mono 16 bits ; audio en mémoire, sans journalisation
ni sauvegarde de la capture. Buffers possédés libérés/effacés au mieux, sans
promesse d’effacement de toute copie RAM ou système. Si le pilote ne confirme pas
son arrêt, IRIS conserve l’indicateur d’audio potentiellement actif, bloque la
session et demande de quitter ; il ne prétend pas que le périphérique est fermé.
Bouton d’arrêt et raccourci global annulent capture, ASR et lecture. Le raccourci
physique et les pilotes restent à tester manuellement.

## Comparaison reproductible

```powershell
dotnet run --project tests/IRIS.VoiceTests -c Release -- --benchmark
```

Six phrases synthétisées par Hortense, 61 mots de référence, sans microphone,
haut-parleur, bruit ou locuteur humain. WER = (substitutions + suppressions +
insertions) / mots de référence ; casse/ponctuation normalisées. Les nombres écrits
en chiffres restent différents des nombres en lettres.

| Moteur | Erreurs / 61 mots | WER agrégé | Temps moyen par phrase |
|---|---:|---:|---:|
| Windows français | 18 | 29,5 % | 0,46 s |
| Whisper Small CPU | 5 | 8,2 % | 3,83 s |

Temps Whisper incluant vérification, démarrage et chargement par phrase. Résultats
locaux : out/speech/benchmark.json ; out/speech/hortense-exemple.wav contient une
phrase synthétique uniquement. Ce petit corpus ne prouve ni la qualité sur une
voix humaine ni le naturel de Hortense. À comparer : bruit, voix basse/rapide,
phrases longues et noms propres, puis voix neuronales après revue des licences et
accord d’acquisition. Cette mesure précède l’acquisition Siwis du jalon 0.7.0.


## Préparation du signal — 0.5.1

Le microphone est fermé avant tout traitement ASR. Suppression de la composante
continue, seuil de découpage adapté au niveau maximum des trames, marges de
250 ms pour préserver les débuts/fins de mots. Les pauses internes sont conservées.
Le seuil demandé ne peut plus effacer une phrase dont le niveau est trop faible
pour le seuil fixe précédent. Un signal quasi nul ou continu seul est rejeté.

Le niveau du clip est ajusté pour l’ASR seulement : cible RMS 0,06, amplification
maximum ×8 et crête limitée à 0,95. Ce traitement n’est ni un débruitage ni une
réparation de la saturation ; il ne change aucun volume Windows. Les buffers
possédés sont effacés au mieux comme auparavant. Analyse et audio restent en RAM.

La fenêtre affiche durée, niveau faible/correct/saturé et conseil de réglage ; les
valeurs RMS/crête/gain ASR sont disponibles en survol. Vérifier le microphone
sélectionné et réduire le gain en cas de saturation. La qualité dans le bruit et
sur la voix humaine de l’utilisateur reste à mesurer. La transcription est toujours
corrigeable et le transfert au chat reste manuel.

```powershell
dotnet run --project tests/IRIS.VoiceTests -c Release -- --quiet-regression
```

Cas synthétique : deux phrases Hortense atténuées à 2,5 % de leur amplitude,
séparées par deux secondes de silence. Ancien découpage à 0,5 % : zéro octet
conservé. Nouveau traitement : huit mots reconnus par Whisper, zéro erreur sur
ce seul cas. Rapport out/speech/quiet-regression.json. Aucun microphone ni
haut-parleur utilisé ; ce résultat ne prédit pas la précision sur une voix réelle.

## Synthèse Siwis locale — 0.7.0

Acquise après accord utilisateur : Piper 2023.11.14-2 Windows x64 + voix
fr_FR-siwis-medium, révision d5de91c972ba5a13cea0e77a63fda1623faac2c1.
Origines : https://github.com/rhasspy/piper/releases/tag/2023.11.14-2 et
https://huggingface.co/rhasspy/piper-voices/tree/d5de91c972ba5a13cea0e77a63fda1623faac2c1/fr/fr_FR/siwis/medium

| Fichier | SHA-256 |
|---|---|
| piper_windows_amd64.zip | f3c58906402b24f3a96d92145f58acba6d86c9b5db896d207f78dc80811efcea |
| fr_FR-siwis-medium.onnx | 641d1ab097da2b81128c076810edb052b385decc8be3381814802a64a73baf99 |
| fr_FR-siwis-medium.onnx.json | 39479916c2db192b5ac9764daddd0c744d83e023ad890c6976c0633ae4df8959 |

Installation explicite : scripts/Install-NaturalVoice.ps1 -AcceptDownload.
La version distribuée sélectionne Siwis par défaut ; Windows reste sélectionnable.
Comparaison locale sur une phrase synthétique : Hortense 9,05 s audio / 0,19 s de
préparation ; Siwis 6,84 s audio / 1,83 s de préparation. Une seule mesure, aucun
jugement humain ni conclusion générale de performance. Siwis produit du PCM
22050 Hz ; l’ASR reste strictement 16000 Hz. Détails : SPOKEN_REPLIES.md.
