# Modèles locaux — jalon 0.2.0

Acquisition autorisée explicitement par l’utilisateur le 27 octobre 2026.
Aucun téléchargement au démarrage de l’application.

| Élément | Version / provenance | Taille / empreinte SHA-256 |
|---|---|---|
| Moteur | [llama.cpp b11901 Vulkan x64](https://github.com/ggml-org/llama.cpp/releases/tag/b11901), archive publiée par ggml-org | 37 052 720 octets ; `6e3a253f45117d1aec2adb2de0699ded0f6dccb683a798802d9e0dabae291f2c` |
| Modèle | [Qwen3-4B-Instruct-2507 Q4_K_M GGUF Unsloth](https://huggingface.co/unsloth/Qwen3-4B-Instruct-2507-GGUF), révision `a06e946bb6b655725eafa393f4a9745d460374c9` | 2 497 281 120 octets ; `3605803b982cb64aead44f6c1b2ae36e3acdb41d8e46c8a94c6533bc4c67e597` |

Le moteur est sous MIT ; le modèle source Qwen et la fiche de quantification
annoncent Apache-2.0. Licence MIT, texte Apache-2.0 et notice LLVM/OpenMP fournie
par l’archive ont été conservés dans `%LOCALAPPDATA%\IRIS\licenses`.
Les notices de chaque binaire devront encore être réconciliées avant redistribution
portable ; aucune distribution n’est réalisée dans ce jalon.

## Installation et lancement

`./scripts/Install-LocalModel.ps1 -AcceptDownload` télécharge uniquement ces
versions, contrôle SHA-256 avant activation et conserve un fichier existant
différent plutôt que l’écraser. Les fichiers sont placés sous `%LOCALAPPDATA%\IRIS` :
`packages`, `runtimes/llama-b11901`, `models`, `licenses`.
Le téléchargement n’est pas reprenable ; un échec peut laisser un fichier `.part`
non activé. Aucun exécutable n’est lancé par ce script d’acquisition.

Au chargement demandé dans IRIS, l’archive est revérifiée, puis chaque fichier
installé est comparé à son entrée d’archive. Tout fichier natif supplémentaire
bloque le lancement. Le modèle complet est rehaché ; les verrous de lecture sont
conservés jusqu’à l’arrêt, pour limiter le remplacement après vérification.

Le serveur est créé dans un Job Object kill-on-close, sans shell ni fenêtre
console. Il reçoit un environnement réduit, une clé éphémère par environnement,
`--host 127.0.0.1`, un port dynamique, `--offline`, `--no-webui`, un contexte de
4 096 tokens et quatre threads. Proxy, cookies et redirections HTTP sont désactivés.
La table TCP vérifie le PID propriétaire et l’absence d’autre écoute du moteur.

La réponse est actuellement reçue en une fois, avec limite de 512 tokens et
délai de deux minutes. La conversation accepte 4 096 caractères par message,
borne le contexte récent et ne conserve que les échanges réussis en mémoire vive.
Les demandes d’outils du modèle sont rejetées. Le texte affiché ne commande jamais Windows.

Depuis 0.3.0, la fenêtre peut recevoir des souvenirs depuis SQLite uniquement
après consentement de mémorisation et de partage local. Ils restent dans un
bloc de données non fiable, avec budget de contexte. Une suppression, correction
ou expiration invalide le contexte ; aucune écriture automatique de souvenirs
n’est déclenchée par le modèle. Voir [MEMORY](MEMORY.md).

## Personnalité et performances

Humour : 0–24 sobre, 25–59 discret, 60–100 sarcasme doux. Ces choix sélectionnent
des instructions documentées, sans garantie absolue sur la sortie probabiliste.
L’initiative est passive ; aucun déclencheur spontané n’est implémenté.

Le moteur Vulkan et le modèle ont été utilisés réellement sur la machine cible.
Voir [BENCHMARKS](BENCHMARKS.md). Le mode CPU est disponible comme repli manuel ;
son débit et sa qualité n’ont pas encore été mesurés. Il n’existe pas de preuve de
qualité générale en français fondée sur ces seuls échantillons.

À faire : réponse en flux, mesure premier token/tokens par seconde/VRAM, essais
longs, repli automatique éventuel, gestionnaire multi-modèle avec licences et
empreintes, supervision avancée. Ne pas charger plusieurs gros modèles sans raison.
