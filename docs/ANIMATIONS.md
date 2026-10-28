# Animations IRIS — 0.5.0 — 27 octobre 2026

## Inventaire et provenance

Inspection récursive du dossier assets : 25 spritesheets PNG dans sprites/, plus
trois PNG de documentation dans readme/ qui ne sont pas des animations. Le manifeste
animations.json indique spritesheets/ : le résolveur utilise le nom de fichier
uniquement lorsqu’une correspondance unique existe dans assets. Chaque correction
de chemin est affichée dans le diagnostic de la prévisualisation. Aucune image ni
le manifeste fourni n’a été modifié. Les sources historiques app/ et cross/ restent
exclues de la solution IRIS.

Le moteur est adapté des principes de Clip, Play/Advance et DrawLayered dans
app/Capri.cs et de Clip dans cross/Pet.cs : temps écoulé, découpage horizontal et
dessin alpha. Les comptes/FPS ne sont plus codés en dur ; les ressources ne sont
plus toutes décodées au démarrage. Aucune fonction de contrôle du PC issue du
monolithe n’est importée. Licence et mentions de Capri conservées.

## Comportements

| Animation | Intégration réelle |
|---|---|
| idle | Repos en boucle, 16 images à 8 FPS |
| blink | Clignement complet entre 3 et 7 secondes de repos, retour idle |
| talk | Personnage complet pendant la lecture audio TTS ; retour thinking ou idle |
| thinking | Attente d’une réponse locale, arrêt à la fin/annulation |
| happy | Réponse textuelle reçue avec succès, puis retour au repos ; ce n’est pas une analyse émotionnelle du texte |
| sad | Échec du traitement de conversation ; également sélectionnable manuellement |
| grin | Double-clic sur le personnage ; également sélectionnable manuellement |
| sleep / wake | Après 120 s sans interaction dans IRIS, sommeil ; interaction locale ou commande de réveil, puis idle |
| arrive / leave | Apparition à l’affichage ; disparition explicitement demandée puis masquage |
| walk | Déplacement explicite gauche/droite du personnage seul ; prototype de balancement, pas une vraie marche dessinée |
| dangle | Pendant le glissement du personnage ; prototype conservé tel quel |
| dance | Commande explicite ou prévisualisation ; boucle jusqu’au retour normal |
| angry / surprised | Expressions manuelles dans la prévisualisation ; pas de déclenchement inventé depuis le texte du modèle |
| glitch / blink_glitch | Lecture manuelle, sans effet ajouté au contenu fourni |
| perch et perch_* | Lecture manuelle de prototypes ; aucune vraie assise ou détection de fenêtre étrangère |
| mouth | Calque isolé dans la prévisualisation uniquement, jamais superposé au personnage |

Les 25 fichiers sont lisibles. Les expressions et poses restent des prototypes
artistiques ; leur chargement correct ne garantit pas le naturel des mouvements.
L’examen du PNG mouth montre une petite bouche sur fond transparent. Même si un
futur fichier mouth contient un personnage entier, le moteur n’a aucune voie de
composition de bouche : il ne peut pas créer deux corps. La parole emploie talk.
La synchronisation phonétique ou par amplitude n’est pas implémentée.

Les animations non bouclées reviennent au comportement normal sur le bureau,
sauf leave qui masque le personnage. Dans la prévisualisation, elles restent sur
leur dernière image pour inspection. Une boucle demandée manuellement sur le
bureau reste active jusqu’à **Rétablir le comportement normal**. La parole audio
peut reprendre la priorité. L’arrêt d’urgence arrête les timers et les chargements,
et ferme la prévisualisation ; aucune transition ne redémarre la session.

## Transparence, placement et mémoire

Cellules originales 512 × 768, origine bottom-center, proportions 2:3 conservées.
Pas de découpage automatique des marges, pas de fond rempli dans les frames, pas
de couleur clé. Images réduites en Format32bppPArgb et UpdateLayeredWindow alpha
par pixel. Le damier de prévisualisation est uniquement un fond du contrôle.

Hauteur du canevas bureau 256 pixels à 100 %, réglable de 128 à 512 pixels. Taille
et changement d’animation conservent le bas-centre. Position enregistrée après
déplacement volontaire, puis bornée à la zone de travail de l’écran le plus proche,
y compris coordonnées négatives. Un écran trop petit entraîne une réduction
proportionnelle. Les changements de configuration écran repositionnent la fenêtre.
La hauteur native des poses et leurs marges transparentes restent celles des PNG.

Un lecteur conserve une seule animation réduite. La grande spritesheet est ouverte
et décodée dans une tâche, les frames sont redimensionnées puis la feuille et le
flux sont immédiatement libérés. Au changement, les anciennes frames sont disposées
après installation des nouvelles. Un seul décodeur par lecteur ; requêtes anciennes
annulées. Aucun cache de toutes les planches. La prévisualisation et le personnage
ont chacun leur lecteur. À 256 px, la plus longue animation conservée fait environ
4 Mio ; idle fait environ 2,7 Mio. À 384 px, idle fait 6 Mio. Les allocations
transitoires de décodage sont plus grandes : ce n’est pas une mesure du working set.

Bornes : manifeste 256 Kio, 128 entrées, 512 PNG, fichier 64 Mio, 120 frames,
FPS 1–60, cellules jusqu’à 2048 px, feuille jusqu’à 16 777 216 pixels, images
réduites jusqu’à 32 Mio par lecteur. En-tête RGBA 8 bits et dimensions vérifiés
avant décodage ; erreurs GDI+/décodage remontées à l’interface.

Un fichier absent ou incohérent est signalé par animation. Sans manifeste, les
comptes/FPS ne sont pas devinés : le panneau principal indique l’indisponibilité.
Les chemins hors assets et les correspondances ambiguës sont refusés. Les PNG
absents du manifeste sont inventoriés, sans animation inventée. Les fichiers sont
copiés dans assets/ à la compilation/publication ; pas de téléchargement.

## Tester

```powershell
./scripts/Build.ps1
./scripts/Start-IRIS.ps1
```

1. Ouvrir **Animations** sans charger l’IA.
2. Sélectionner une entrée, puis **Lire depuis le début** et **Arrêter / figer**.
3. Comparer les FPS annoncés aux changements d’images par seconde affichés après
   stabilisation. La cadence observée peut baisser si Windows retarde le thread UI ;
   la timeline saute alors les frames en retard, sans ralentir l’animation.
4. Vérifier transparence sur damier, dernière frame des clips sans boucle, et les
   avertissements prototype. mouth reste isolé ; son application au bureau est désactivée.
5. **Jouer sur le bureau** applique la sélection. **Rétablir le comportement normal**
   restaure le repos ou réaffiche le personnage masqué si son affichage est autorisé.
6. Menu droit du personnage : marcher, danser, réveiller, disparaître. Glisser pour
   positionner, double-cliquer pour grin. Paramètres : visibilité, échelle, premier plan.
7. Une lecture TTS depuis **Voix** active talk ; la simple synthèse ou le micro ne
   le déclenchent pas. Une réponse IA active thinking puis happy, sans action PC.

Tests automatiques dans tests/IRIS.DesktopTests/AnimationChecks.cs. Ils décodent les
25 fichiers livrés et dessinent seulement leurs propres fenêtres. Pas de capture
d’écran, frappe physique, microphone ou haut-parleur. Rendus dans out/ui/animations.png
et out/ui/animation-*.png. L’affichage avec plusieurs DPI physiques, le déplacement
manuel entre écrans et le naturel artistique restent des vérifications manuelles.

## Fichiers de cette intégration

- src/IRIS.Desktop/Animation/ : catalogue, frames, timeline/lecteur, prévisualisation,
  personnage, présentateur natif alpha et détection d’interaction locale.
- src/IRIS.Desktop/ : MainWindow, Program, ConversationWindow, VoiceWindow,
  ObservedAudioPlayback, SettingsWindow, projet et manifeste.
- src/IRIS.Infrastructure/LocalSettingsStore.cs : position facultative validée.
- tests/IRIS.DesktopTests/AnimationChecks.cs et Program.cs : tests et connexions
  réelles aux callbacks de génération et lecture audio, avec fournisseurs simulés.
- Directory.Build.props, README.md et documentation : version et état du jalon.

Aucune modification des spritesheets, du pipeline de permissions, des gestionnaires
d’actions Windows ou des moteurs IA/ASR. Aucun paquet supplémentaire.
