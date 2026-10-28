# Installation du SDK de développement

Le 27 octobre 2026, après autorisation explicite de l’utilisateur, le SDK
Microsoft .NET 10.0.403 Windows x64 a été installé par extraction manuelle dans :

`%LOCALAPPDATA%\Microsoft\dotnet`

Source : métadonnées de publication Microsoft
`https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json`,
archive `https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.403/dotnet-sdk-10.0.403-win-x64.zip`.
L’archive a été vérifiée en SHA-512 avec l’empreinte des métadonnées avant extraction.
La signature Authenticode de `dotnet.exe` a été vérifiée valide, éditeur Microsoft
Corporation, avant exécution. Pas d’élévation, d’installateur distant exécuté ou de
modification des protections Windows.

SDK constaté : 10.0.403 ; MSBuild 18.9.15 ; runtimes .NET et Windows Desktop
10.0.14. `DOTNET_ROOT` et le PATH utilisateur ont été configurés. Les nouveaux
terminaux peuvent utiliser `dotnet` ; les scripts du projet savent aussi retrouver
cette installation lorsque l’application hôte conserve un ancien environnement.
Les scripts désactivent la télémétrie du CLI .NET dans leur processus.

Cette installation est un outil de développement approuvé, distinct des dépendances
nécessaires à l’exécution d’un futur paquet autonome. La méthode d’installation
manuelle suit la [documentation Microsoft](https://learn.microsoft.com/en-us/dotnet/core/install/windows#manual-install).

## Correction de la découverte et du lancement

Initialize-DotNet.ps1 résout désormais un chemin explicite vers dotnet.exe x64,
examine les installations utilisateur/système même avec un ancien PATH et vérifie
le composant réellement disponible. La compilation valide le SDK depuis le dépôt
avec global.json ; Start-IRIS valide seulement Windows Desktop Runtime .NET 10
lorsque les binaires existent déjà. DOTNET_ROOT et DOTNET_ROOT_X64 sont définis
pour le processus appelant uniquement. Les scripts invoquent le chemin explicite,
sans dépendre du cache Get-Command ou de la résolution de dotnet sans extension.

Vérification exécutée dans Windows PowerShell 64 et 32 bits, sans profil, avec un
PATH réduit à System32 et DOTNET_ROOT volontairement invalide : SDK 10.0.403 et
runtime retrouvés dans les deux cas. Aucun téléchargement ou réinstallation.

## Lancement autonome, indépendant de la découverte

Le diagnostic précédent ne reproduisait pas l’erreur du terminal utilisateur.
La correction de lancement fournit donc un paquet self-contained Windows x64
avec Microsoft.NETCore.App et Microsoft.WindowsDesktop.App 10.0.14. La configuration
IRIS.runtimeconfig.json déclare includedFrameworks, sans framework global requis.

Double-cliquer Lancer-IRIS.cmd, ou lancer out/IRIS-portable-win-x64/IRIS.exe. Le
script Start-IRIS.ps1 donne la priorité à ce dossier et ne consulte alors ni
Initialize-DotNet.ps1, ni PATH, ni l’installation globale. Le dossier complet est
nécessaire. Le développement et la reconstruction nécessitent toujours le SDK.

scripts/Publish-Portable.ps1 reconstruit ce dossier, copie les PNG et les notices.
Build.ps1 le lance après les contrôles. Les paquets runtime officiels 10.0.14 ont
été vérifiés avec dotnet nuget verify --all : signatures Microsoft et NuGet valides.
Le verrou System.Speech conserve la même version et le même hash ; seule la cible
win-x64 est ajoutée. Aucun moteur IA ou modèle n’est inclus ou démarré par le paquet.
