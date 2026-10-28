using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using IRIS.Core.Contracts;
using IRIS.Security.Policies;

namespace IRIS.Desktop;

internal static class LocalClockQuery
{
    public static string? Match(string text)
    {
        if (text.Length > 4096) return null;
        string plain;
        try { plain = Regex.Replace(text.Normalize(NormalizationForm.FormD), "\\p{Mn}", "").ToLowerInvariant(); }
        catch (ArgumentException) { return null; }
        plain = Regex.Replace(Regex.Replace(plain, "[^a-z0-9]+", " "), " +", " ").Trim();
        foreach (string question in new[] { "quelle heure est il", "quel heure est il", "il est quelle heure", "quelle est l heure", "donne moi l heure" })
        {
            if (plain == question) return "clock.time";
            if (plain == question + " avec les secondes") return "clock.time.seconds";
            if (plain == question + " avec le fuseau horaire" || plain == question + " et le fuseau horaire") return "clock.time.zone";
            if (plain == question + " avec les secondes et le fuseau horaire") return "clock.time.seconds.zone";
        }
        return plain switch
        {
            "quel est le fuseau horaire" or "quel fuseau horaire utilises tu" or "dans quel fuseau horaire es tu" => "clock.zone",
            "quelle est la date" or "quel jour sommes nous" or "quelle date sommes nous" or "on est quel jour" => "clock.date",
            _ => null
        };
    }
    public static async Task<string> ExecuteAsync(ActionPolicyEngine policy, string action, CancellationToken token)
    {
        if (action is not ("clock.time" or "clock.date" or "clock.time.seconds" or "clock.time.zone" or "clock.time.seconds.zone" or "clock.zone")) throw new InvalidOperationException("Question locale inconnue.");
        string operation = action == "clock.date" ? "clock.date" : "clock.time";
        var request = new ActionRequest(Guid.NewGuid(), Guid.NewGuid(), operation, new Dictionary<string, string>(), RequestOrigin.UserInterface,
            "clock/Europe-Paris", DateTimeOffset.UtcNow.AddSeconds(30));
        ActionResult result = await policy.ExecuteAsync(request, token);
        if (!result.Succeeded) throw new InvalidOperationException("Lecture de l’horloge refusée.");
        if (action == "clock.date") return "Nous sommes le " + result.Message + ".";
        if (action == "clock.zone") return "Le fuseau horaire utilisé est Europe/Paris.";
        return FormatTime(result.Message, action.Contains("seconds", StringComparison.Ordinal), action.EndsWith(".zone", StringComparison.Ordinal));
    }
    internal static string FormatTime(string value, bool seconds = false, bool zone = false)
    {
        if (!TimeOnly.TryParseExact(value, "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly time))
            throw new InvalidOperationException("Heure locale invalide.");
        string hour = time.Hour == 0 ? "minuit" : time.Hour == 12 ? "midi" : time.Hour + (time.Hour == 1 ? " heure" : " heures");
        string answer = "Il est " + hour;
        if (time.Minute > 0) answer += " " + time.Minute;
        if (seconds) answer += (time.Minute > 0 ? (time.Minute == 1 ? " minute et " : " minutes et ") : " et ") + time.Second + (time.Second == 1 ? " seconde" : " secondes");
        if (zone) answer += ", heure de Paris";
        return answer + ".";
    }
}
