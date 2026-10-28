# Architecture IRIS — 0.5.0

Solution compilée de douze projets, composition par injection de constructeurs :

| Projet | Responsabilité |
|---|---|
| Core | Commandes immuables, métadonnées de confiance, contrats d’audit/confirmation et arrêt d’urgence |
| Security | Permissions, validation, expiration, confirmation ponctuelle, anti-rejeu et exécution séquentielle |
| Actions | Date et heure Europe/Paris, aucune mutation Windows |
| AI | Client local borné/authentifié, contrat de fournisseur, personnalité et contexte de session |
| Memory | SQLite système, souvenirs et politique versionnés, transactions, rétention, JSON et gestionnaires de mutations confirmées |
| Voice | Capture/lecture WinMM, reconnaissance Windows, synthèse SAPI et orchestration annulable |
| VoiceTests | PCM, WER, consentement et annulation simulés ; comparaison synthétique réelle sur option --benchmark |
| Infrastructure | Paramètres, journal technique, verrou d’instance, vérification de fichiers, processus/Job Object et inspection TCP Windows |
| Desktop | Composition et fenêtres WinForms, paramètres, conversation, confirmation native et raccourci d’arrêt |
| SecurityTests | Tests exécutables sans framework tiers, doubles HTTP/IA et dossiers temporaires |
| DesktopTests | Contrôles WinForms avec doubles des raccourcis/IA, rendus des fenêtres de test ; mode réel explicite |
| IntegrationTests | Génération/cycle de vie et mémoire réels, uniquement avec les options --local-model ou --local-memory |

Core ne dépend pas de Windows. Security et Actions dépendent de Core ; AI définit
le contrat de génération et le contexte ; Infrastructure dépend de AI pour
implémenter le moteur supervisé. Desktop compose les services. Les API natives
Windows sont marquées comme spécifiques à Windows. Aucun lien Compile, ressource
ou référence vers les sources ou médias Capri. Voice dépend de Core et du package
Microsoft System.Speech 10.0.14 verrouillé. Memory dépend
uniquement de Core ; AI reçoit son fournisseur par IMemoryContextProvider, sans
référence au stockage SQLite.

## Frontières de confiance

Une requête copie ses paramètres dans un dictionnaire en lecture seule. Risque,
permission, cible, application, effet et conséquences viennent uniquement des
descripteurs de gestionnaires enregistrés par le code. Le modèle ne les choisit
jamais. Une confirmation appartient à l’invocation et à la commande immuable ;
les approbations ne sont jamais persistées ni transférées.

Le moteur exige une expiration de deux minutes maximum, sérialise les demandes,
consomme leur identifiant (plafond 10 000 par instance) et refuse les rejouements.
L’attente en file et la confirmation sont annulées à l’expiration ou à l’arrêt.
Un service de confirmation qui ignore son token ne bloque plus la file.
Les futurs gestionnaires à effets doivent coopérer à l’annulation et vérifier
le résultat ; une annulation ne peut pas annuler rétroactivement un effet déjà réalisé.

## Inférence et conversation

LocalInferenceService possède le processus, son Job Object, le client HTTP et
les verrous de lecture sur les fichiers vérifiés. Le chargement est explicite.
WindowsJobLauncher crée le processus atomiquement dans le Job Object via
PROC_THREAD_ATTRIBUTE_JOB_LIST et n’hérite que d’un handle NUL pour les entrées/
sorties. Aucun shell, aucune console ni journal de sortie du serveur.

Le PID doit posséder uniquement l’écoute 127.0.0.1:port attendue. HTTP local avec
clé éphémère, proxy/cookies/redirections désactivés, réponses et délais bornés.
Le texte du modèle ne traverse jamais le dispatcher d’actions ; les demandes
structurées d’outils éventuelles sont rejetées.

ConversationService ne conserve que les échanges réussis de la fenêtre, borne le
contexte et reconstruit l’instruction d’identité depuis PersonalityProfile.
L’humour sélectionne un style ; l’initiative reste passive. La génération ne
s’effectue que sur clic d’envoi. Il n’existe pas encore de streaming ni de
persistance des conversations. Fermer la fenêtre décharge le modèle.

Le personnage indépendant est une fenêtre WinForms layered à alpha par pixel,
avec déplacement limité à sa propre fenêtre. AnimationCatalog découvre les PNG et
valide animations.json ; AnimationPlayer décode à la demande, SpriteFrames conserve
les images réduites. Le principe Clip/Advance et DrawLayered de Capri est adapté
sans réactiver le monolithe. Voir ANIMATIONS.md.

## Mémoire persistante

MemoryService ouvre le SQLite système pour chaque opération sérialisée, réalise
les purges autorisées et le travail dans une transaction, puis ferme les handles.
Le schéma versionne séparément la politique, les souvenirs et une révision
globale. MemoryActionHandler déclare des opérations fixes confirmées, limitées à
l’origine UI ; aucune API de modification n’est donnée au modèle.

IMemoryContextProvider fournit un instantané borné au service conversationnel,
avec sa révision. Changement/expiration : contexte récent invalidé. Changement
pendant la réponse : résultat écarté. Le texte n’est jamais promu au rôle système.
Les imports/exports sont du JSON de données sans autorisations, distinct des
paramètres généraux et du journal technique. Voir MEMORY.md.

## Audio local

Whisper réutilise le vérificateur de fichiers et le lanceur supervisé de
l’inférence. Le lanceur accepte une liste explicite de handles de pipes ; le
serveur llama conserve NUL. Aucun shell ni héritage arbitraire de handles.
VoiceWorkflow ferme la capture avant l’ASR et possède le clip jusqu’à sa libération.
La fenêtre de voix sépare capture, correction, transfert du brouillon et envoi.
Les préférences ne stockent aucune permission. Voir VOICE.md pour les bornes,
licences, mesures et limites des pilotes.
