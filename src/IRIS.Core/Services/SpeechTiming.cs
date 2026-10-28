using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using IRIS.Core.Contracts;

namespace IRIS.Core.Services;

public static class SpeechTiming
{
    public static IReadOnlyList<SpeechWordBoundary> Estimate(string text, TimeSpan duration)
    {
        Match[] words = Regex.Matches(text, "\\S+").Cast<Match>().ToArray();
        double weight = words.Sum(w => Math.Max(2, w.Length) + (w.Value.EndsWith('.') || w.Value.EndsWith('?') || w.Value.EndsWith('!') ? 3 : 1));
        double at = 0;
        var marks = new List<SpeechWordBoundary>();
        foreach (Match word in words)
        {
            marks.Add(new(word.Index, word.Length, TimeSpan.FromSeconds(duration.TotalSeconds * at / Math.Max(1, weight))));
            at += Math.Max(2, word.Length) + (word.Value.EndsWith('.') || word.Value.EndsWith('?') || word.Value.EndsWith('!') ? 3 : 1);
        }
        return marks.AsReadOnly();
    }
}
