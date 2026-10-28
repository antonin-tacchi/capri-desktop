using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IRIS.Core.Contracts;

namespace IRIS.Memory;

public sealed class MemoryActionHandler : IActionHandler
{
    private readonly IMemoryService memory;
    private readonly string operation;
    public ActionDescriptor Descriptor { get; }
    public MemoryActionHandler(IMemoryService memory, string operation)
    {
        if (operation is not ("add" or "update" or "delete" or "clear" or "policy" or "import")) throw new ArgumentException("Opération mémoire inconnue.");
        this.memory = memory;
        this.operation = operation;
        string effect = operation switch
        {
            "add" => "Conserver uniquement le souvenir affiché dans la base locale.",
            "update" => "Remplacer le contenu du souvenir sélectionné et renouveler son délai de conservation.",
            "delete" => "Supprimer le souvenir sélectionné de la base active.",
            "clear" => "Supprimer tous les souvenirs de la base active.",
            "policy" => "Appliquer les choix affichés. Les souvenirs expirés seront supprimés de la base active au prochain accès. Réduire la durée peut donc supprimer des souvenirs. Désactiver la mémoire conserve ceux qui ne sont pas expirés.",
            _ => "Importer les souvenirs du document affiché, sans écraser un souvenir existant ni activer une permission."
        };
        Descriptor = new("memory." + operation, operation is "delete" or "clear" ? ActionRisk.Destructive : ActionRisk.Sensitive,
            PermissionLevel.RequiresConfirmation, "memory/local", effect,
            "Le contexte de la conversation ouverte sera réinitialisé. Aucune suppression physique garantie ; les exports et sauvegardes restent indépendants.", "IRIS");
    }
    private static Guid Id(ActionRequest request) => Guid.Parse(request.Parameters["id"]);
    private static long Revision(ActionRequest request) => long.Parse(request.Parameters["revision"], CultureInfo.InvariantCulture);
    private static MemoryKind Kind(ActionRequest request) => (MemoryKind)int.Parse(request.Parameters["kind"], CultureInfo.InvariantCulture);
    private static MemoryPolicy Policy(ActionRequest request) => JsonSerializer.Deserialize<MemoryPolicy>(request.Parameters["policy"], MemoryValidation.JsonOptions) ?? throw new ArgumentException("Politique absente.");
    public bool Validate(ActionRequest request)
    {
        if (request.Origin != RequestOrigin.UserInterface) return false;
        string[] keys = operation switch
        {
            "add" => new[] { "title", "content", "kind" },
            "update" => new[] { "id", "revision", "title", "content", "kind" },
            "delete" => new[] { "id", "revision", "title" },
            "policy" => new[] { "policy" }, "import" => new[] { "document" }, _ => Array.Empty<string>()
        };
        if (request.Parameters.Count != keys.Length || keys.Any(key => !request.Parameters.ContainsKey(key))) return false;
        try
        {
            if (operation is "add" or "update") MemoryValidation.Content(request.Parameters["title"], request.Parameters["content"], Kind(request));
            if (operation is "delete" or "update") { if (Id(request) == Guid.Empty || Revision(request) <= 0) return false; }
            if (operation == "policy") MemoryValidation.Policy(Policy(request));
            if (operation == "import") MemoryValidation.Parse(request.Parameters["document"], DateTimeOffset.UtcNow);
            return true;
        }
        catch (Exception error) when (error is ArgumentException or FormatException or OverflowException or System.IO.InvalidDataException or JsonException) { return false; }
    }
    public async Task<ActionResult> ExecuteAsync(ActionRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            switch (operation)
            {
                case "add": await memory.AddAsync(request.Parameters["title"], request.Parameters["content"], Kind(request), cancellationToken); break;
                case "update": await memory.UpdateAsync(Id(request), Revision(request), request.Parameters["title"], request.Parameters["content"], Kind(request), cancellationToken); break;
                case "delete": await memory.DeleteAsync(Id(request), Revision(request), cancellationToken); break;
                case "clear": await memory.ClearAsync(cancellationToken); break;
                case "policy": await memory.SetPolicyAsync(Policy(request), cancellationToken); break;
                case "import": await memory.ImportAsync(request.Parameters["document"], cancellationToken); break;
            }
            return new(true, "completed", "Mémoire locale mise à jour.");
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.IO.InvalidDataException)
        { return new(false, "memory_refused", error.Message); }
        catch (JsonException) { return new(false, "memory_refused", "Document mémoire invalide."); }
    }
}
