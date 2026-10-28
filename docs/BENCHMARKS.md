# Premiers essais — 27 octobre 2026

Machine constatée par CIM : AMD Ryzen 9 5900XT, 16 cœurs / 32 processeurs logiques ;
AMD Radeon RX 7600, pilote Windows 32.0.32015.7029 ; RAM physique 34 281 054 208
octets. .NET 10.0.14, SDK 10.0.403.

Qwen3-4B-Instruct-2507 Q4_K_M (SHA-256 dans MODELS.md), llama.cpp b11901 Vulkan,
contexte 4 096, quatre threads, 512 tokens de sortie maximum, température 0,6.
Offload GPU demandé (-ngl 99), mais utilisation réelle/VRAM non instrumentées.

| Mesure observée | Premier essai | Essai après lancement atomique dans le Job Object |
|---|---:|---:|
| Vérification des fichiers + chargement + santé | 9,35 s | 5,29 s |
| Réponse complète au message de présentation | 1,72 s | 1,42 s |
| Working set du processus après réponse | 3 000 Mio | 2 946 Mio |

Essais ponctuels, caches et état de la machine non contrôlés. La différence entre
ces valeurs ne prouve pas une optimisation ni une accélération du code. La RAM
mesurée est celle du processus, pas la VRAM ni le total système.
Le temps avant premier token et le débit tokens/s ne sont pas mesurés, car la
réponse est actuellement reçue en une fois.

Message fixe : demande de présentation en français et d’explication de la capacité
à contrôler le PC. Les réponses réelles se présentent comme IRIS et indiquent
l’absence d’accès système. L’interface a également affiché une génération réelle,
puis fermé le moteur correctement. Ce n’est ni un corpus d’évaluation français,
ni une mesure de cohérence sur longue durée ou de robustesse aux hallucinations.

Les derniers détails d’un essai explicite sont écrits dans
`out/local-model-smoke.json` (ignoré par Git). Aucun dialogue personnel n’est utilisé
pour ces essais. Restent à mesurer : débit, premier token, VRAM, CPU/GPU au repos,
mode CPU, conversations longues, jeux et qualité française sur corpus diversifié.

## Reconnaissance française synthétique — 0.4.0

Six phrases, 61 mots de référence synthétisés par Hortense, sans bruit, microphone
ou haut-parleur. Windows : 18 erreurs, WER 29,5 %, 0,46 s/phrase en moyenne.
Whisper Small CPU : 5 erreurs, WER 8,2 %, 3,83 s/phrase, vérification et démarrage
inclus. Les nombres en chiffres ne sont pas assimilés aux nombres en lettres.
Corpus trop court pour conclure sur une voix humaine ou le naturel du TTS.
Protocole, commandes et empreintes dans VOICE.md ; données dans
out/speech/benchmark.json et phrase synthétique dans out/speech/hortense-exemple.wav.

