# Formulation naturelle — 0.7.1

La voix et ses réglages ne changent pas. La correction concerne le texte affiché
et prononcé, qui reste identique dans le chat et la bulle.

L’horloge conserve ses données précises en interne, mais répond par défaut :
« Il est 2 heures 28. » Aucun fuseau ni seconde non demandés. À l’heure pleine,
« Il est 1 heure. », « Il est midi. » ou « Il est minuit. »

Demandes explicites reconnues sans charger le modèle :
- Quelle heure est-il ?
- Quelle heure est-il avec les secondes ?
- Quelle heure est-il avec le fuseau horaire ?
- Quelle heure est-il avec les secondes et le fuseau horaire ?
- Quel fuseau horaire utilises-tu ?

Ces variantes passent par la même action d’horloge en lecture seule et sa politique
de sécurité. Le routage reste une liste exacte, sans extraction de commandes libres.

Le prompt de conversation demande un français courant, des phrases fluides, une
réponse courte aux questions simples et des détails seulement lorsqu’ils servent
la demande. Présentations répétées, métadonnées inutiles, préambules et relances
automatiques sont découragés. Les consignes de sécurité et les limites d’accès
restent en place. Ces consignes orientent le modèle local ; elles ne garantissent
pas toutes ses formulations et ne le rendent pas identique à ChatGPT.

Fichiers : src/IRIS.Desktop/LocalClockQuery.cs,
src/IRIS.AI/Conversation/ConversationService.cs,
tests/IRIS.DesktopTests/Program.cs, Directory.Build.props,
Lancer-IRIS.cmd et scripts de lancement/publication.
