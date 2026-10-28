# Révision du paquet livré — 28 octobre 2026

## Défaut constaté

La capture utilisateur montrait « Fichiers IA absents » au chargement. Les
versions précédentes emportaient .NET et les PNG, mais les modèles, moteurs et
archives n’existaient que dans le cache LocalAppData utilisé pendant le
développement. Les tests de composants et de l’interface utilisaient ce cache ;
ils ne validaient pas une distribution complète. Capri possède sa propre gestion
d’installation de moteur/modèle ; isoler IRIS exigeait de livrer ces ressources
et de valider leurs chemins depuis l’exécutable final.

## Correction 0.5.2

- out/IRIS-portable-win-x64-0.5.2 : application, runtime .NET/Windows Desktop,
  ressources graphiques et notices.
- out/IRIS-engine-data : modèles Qwen/Whisper, moteurs llama.cpp/whisper.cpp,
  archives épinglées et notices déjà acquises après autorisation.
- Aucun paramètre, souvenir ou texte de conversation n’est copié dans ce dossier.
  Ces données restent dans le profil privé précédent.
- Prepare-EngineAssets.ps1 copie seulement les ressources connues, vérifie les
  SHA-256 des archives/modèles et chaque fichier natif contre l’archive ; un
  fichier différent existant est conservé et rejeté. Aucun téléchargement ajouté.
- EngineAssets résout depuis AppContext.BaseDirectory, indépendamment du dossier
  courant ; le séparateur final est normalisé. Les tests comprennent un profil
  utilisateur vide. La vérification complète d’intégrité reste obligatoire à
  chaque chargement, avec les verrous et Job Objects existants.
- Les lanceurs ciblent 0.5.2 et ne retombent plus silencieusement sur une ancienne
  version. Le bouton Diagnostic IA indique le dossier et les fichiers manquants.
- Les questions exactes sur heure/date, dont la phrase de la capture, passent par
  clock.time/clock.date et le moteur de permissions en lecture seule. Aucun outil
  n’est donné au modèle, aucune commande intégrée dans une phrase ne se transforme
  en action. Le texte modèle ne déclenche toujours rien.

## Test de l’exécutable livré

```powershell
./scripts/Build.ps1
$app = Start-Process ./out/IRIS-portable-win-x64-0.5.2/IRIS.exe -ArgumentList '--self-check' -PassThru
$app.WaitForExit()
Get-Content ./out/IRIS-portable-win-x64-0.5.2/diagnostics/self-check.json
```

Ce mode explicite teste les fenêtres réelles du produit avec Qwen en CPU, SAPI
en synthèse mémoire, Whisper, puis transfert manuel dans le brouillon. Il échoue
si le chemin résolu dépend encore du cache utilisateur. Aucune capture d’écran,
mémoire personnelle, frappe physique, microphone ou sortie haut-parleur.

Observé : passed=true, version 0.5.2.0, engineRoot=out/IRIS-engine-data ; modèle
chargé, réponse française réelle, huit mots de fixture transcrits sans erreur,
transfert vers le brouillon et processus arrêté. Rapport conservé à côté du
paquet. Les 89 contrôles ordinaires passent également. Le résultat ASR synthétique
ne démontre pas la précision sur le microphone ou le bruit de l’utilisateur.

Pour l’utilisation normale : fermer l’ancienne instance, puis Lancer-IRIS.cmd.
Vérifier le titre IRIS 0.5.2. Aucun modèle ne démarre au simple lancement ; cliquer
Charger IA pour une conversation générale. Heure/date fonctionnent sans ce chargement.
La voix reste sur consentement et maintien du bouton, avec correction du texte
et envoi séparé. Les fonctions futures du cahier des charges (agenda, voix
neuronale naturelle, contrôle Windows supplémentaire) ne sont pas déclarées réalisées.
