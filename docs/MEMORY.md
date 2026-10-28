# Mémoire locale — jalon 0.3.0

## Consentement et utilisation

La mémoire persistante est **désactivée par défaut**. Le bouton **Mémoire** de la
fenêtre principale ouvre son panneau. Cocher une option ne suffit pas : cliquer
**Appliquer les choix**, puis confirmer dans la fenêtre C# indépendante du modèle.

Deux choix distincts : autoriser l’ajout explicite de souvenirs, et autoriser leur
utilisation par l’IA locale. Aucune conversation ni réponse du modèle n’est
automatiquement enregistrée comme souvenir. Le modèle ne peut ni écrire,
corriger, importer, supprimer un souvenir, ni modifier ces choix.

Pour ajouter : activer la mémorisation, cliquer **Nouveau**, saisir un titre et
le contenu, choisir une catégorie, cliquer **Ajouter ce souvenir** puis confirmer.
La correction, la suppression, l’effacement global, l’import et la politique
passent aussi par le moteur central de permissions. Ils exigent l’origine UI,
un schéma validé et une confirmation liée à l’opération. Les corrections et
suppressions vérifient la révision du souvenir pour refuser une sélection périmée.

Désactiver conserve les souvenirs non expirés, permet encore de les consulter,
exporter ou supprimer, bloque les nouveaux ajouts/corrections/imports et retire
leur utilisation par le modèle. Le contrôle de partage n’ouvre aucun accès cloud.

## Stockage et conservation

Base `%LOCALAPPDATA%\IRIS\memory.db`, indépendante de Capri et du journal technique.
SQLite provient de `winsqlite3.dll` dans System32 : aucun nouveau package ou
binaire n’a été téléchargé. Version observée lors des tests : **3.53.4**.
Le composant système est entretenu par Windows ; il n’est pas copié dans le dépôt.

Schéma versionné par `user_version=1` et identifiant applicatif IRIS. Une base
corrompue, étrangère ou d’une version future est refusée et conservée ; aucune
réinitialisation automatique. Tables utiles uniquement : politique, révision
globale et souvenirs. Les tables de conversations/rappels/agenda ne sont pas
créées avant l’implémentation des fonctions correspondantes.

Requêtes préparées, paramètres liés et transactions ; aucune donnée importée
n’est interprétée comme SQL. Handles libérés, attente occupée bornée à deux
secondes, annulation coopérative avec interrupt et rollback. Travail hors thread
UI. Pas de timer de surveillance : les expirations sont traitées aux accès.

Limites : 1 000 souvenirs, titre de 1 à 80 caractères, contenu de 1 à 1 000.
La durée est de 90 jours par défaut, configurable entre 1 et 3 650 jours. Les
souvenirs contextuels durent au maximum sept jours ; les préférences et souvenirs
durables utilisent la durée choisie. Les dates sont stockées en UTC, l’expiration
est affichée en Europe/Paris. Une durée correspond à des périodes de 24 heures.

Une correction explicite renouvelle la durée du souvenir corrigé. Réduire la
conservation raccourcit les échéances existantes et peut rendre des souvenirs
immédiatement expirés ; le dialogue le précise. Augmenter la durée ne prolonge
pas rétroactivement les souvenirs existants. Les expirés ne sont jamais proposés
au modèle ; si IRIS reste fermée, la purge sur disque attend le prochain accès.

Les données et exports ne sont **pas chiffrés**. Des formats usuels de clés,
mots de passe et jetons sont refusés, mais ce filtre n’est pas un détecteur
exhaustif de secrets. Ce stockage n’est pas un gestionnaire de mots de passe.
DPAPI reste nécessaire avant toute future prise en charge de secrets persistants.

`secure_delete=ON` est demandé et vérifié. Cela ne garantit pas l’effacement
physique sur SSD, dans les sauvegardes, snapshots, exports, caches ou la RAM.
La suppression concerne la base active ; les copies restent à gérer séparément.

## Recherche et contexte conversationnel

Recherche SQLite simple et déterministe, sans base vectorielle : normalisation
des accents et de la casse, recherche littérale dans titre/contenu. Le panneau
accepte une recherche de 200 caractères. Pour l’IA, six mots significatifs au
maximum sélectionnent jusqu’à cinq souvenirs récents correspondants, dans une
enveloppe de contenu et de contexte bornée. Ce n’est pas une recherche sémantique :
une question sans mot commun peut ne retrouver aucun souvenir pertinent.

Les souvenirs sont envoyés dans un message de **données non fiables**, identifié
`iris_memory_context`, jamais incorporés aux instructions système. Ils ne donnent
aucun outil et ne modifient jamais une permission. Cela ne prouve pas que le
modèle résiste à toute injection : la frontière d’exécution reste indépendante.

Toute modification ou expiration change la révision globale. Au prochain envoi,
le contexte récent est réinitialisé pour ne pas réinjecter d’anciens souvenirs
par les réponses passées. Si la révision change pendant une génération, sa réponse
est écartée. Les modifications depuis le panneau annulent aussi la demande en
cours et effacent l’affichage de conversation ouvert ; recharger l’IA peut alors
être nécessaire. Une requête déjà envoyée au moteur local ne peut pas être retirée
rétroactivement. Les textes anciennement affichés après une purge automatique
restent visibles jusqu’à l’effacement de session, sans être repris dans le contexte.

## Exports, imports et sauvegardes

**Exporter JSON en clair** ouvre un sélecteur de destination volontaire. Un fichier
existant n’est jamais écrasé. Le fichier temporaire appartenant à l’opération est
retiré sur échec normal ; un crash peut néanmoins laisser une copie temporaire.
Les disques locaux/amovibles sont acceptés, les chemins réseau UNC ou lecteurs
réseau sont refusés. Un dossier synchronisé par un logiciel externe reste sous
la responsabilité de ce logiciel et du choix de destination de l’utilisateur.

Format JSON version 1 : `Version`, `ExportedAt`, `Items`, avec identifiants,
catégories, textes, dates et révisions. Import limité à 8 Mio et 1 000 souvenirs,
UTF-8 strict, profondeur limitée, champs inconnus refusés. Il ne contient aucune
permission à rétablir. Le document est validé puis présenté pour confirmation.

Import tout ou rien : une entrée invalide, expirée, un identifiant répété ou déjà
présent refuse l’ensemble ; aucun écrasement et aucune activation implicite. Les
échéances originales sont conservées ou raccourcies selon la politique actuelle,
jamais renouvelées par import. Un export ancien n’est pas une autorisation de
ressusciter un souvenir expiré. Il n’existe pas d’import de base SQLite brute.

L’export sert de sauvegarde manuelle ; aucune copie automatique ou distante.
Avant toute réparation manuelle d’une base, fermer IRIS et conserver une copie
locale. Ne jamais supprimer une base existante pour masquer une erreur de migration.

## Validation effectuée et limites

Tests sur bases temporaires : défauts de consentement, CRUD/persistance,
révisions, données SQL malveillantes, recherche accentuée, expiration, rétention,
JSON invalide/conflit/volume, refus de secrets courants, annulation/rollback,
concurrence, corruption/version future, export sans écrasement, contexte borné
et invalidation pendant la génération. Le panneau a été testé et son rendu inspecté.

Essai réel opt-in : `dotnet run --project tests/IRIS.IntegrationTests -c Release -- --local-memory`.
Le modèle a retrouvé la donnée synthétique `azur-iris-742` après réouverture de
SQLite ; après suppression, il a répondu « Je ne sais pas. ». La base temporaire
a été supprimée et le moteur arrêté. Ce sont deux échantillons, pas une preuve
générale de qualité de rappel ou d’absence d’hallucination.

Non implémentés : historique persistant des conversations, mémorisation
automatique, recherche sémantique, données d’agenda et rappels, chiffrement de la
base et sauvegarde automatique. Le contexte de session reste distinct des souvenirs.

Références : [paramètres préparés SQLite](https://www.sqlite.org/c3ref/bind_blob.html),
[pragmas et limites de secure_delete](https://sqlite.org/pragma.html),
[sécurité des bases non fiables](https://www.sqlite.org/security.html).
