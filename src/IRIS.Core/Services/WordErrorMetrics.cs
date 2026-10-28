using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace IRIS.Core.Services;

public sealed record WordErrorResult(int ReferenceWords, int Substitutions, int Deletions, int Insertions)
{
    public double? Rate => ReferenceWords == 0 ? null : (Substitutions + Deletions + Insertions) / (double)ReferenceWords;
}
public static class WordErrorMetrics
{
    public static WordErrorResult Compare(string reference, string recognized)
    {
        static string[] Words(string text) => Regex.Matches(text.ToLowerInvariant(), @"[\p{L}\p{N}]+").Select(m => m.Value).ToArray();
        string[] expected = Words(reference), actual = Words(recognized);
        if (expected.Length > 1000 || actual.Length > 1000) throw new ArgumentException("Comparaison limitée à 1000 mots par texte.");
        var distance = new int[expected.Length + 1, actual.Length + 1];
        for (int i = 0; i <= expected.Length; i++) distance[i, 0] = i;
        for (int j = 0; j <= actual.Length; j++) distance[0, j] = j;
        for (int i = 1; i <= expected.Length; i++)
            for (int j = 1; j <= actual.Length; j++)
                distance[i, j] = Math.Min(distance[i - 1, j - 1] + (expected[i - 1] == actual[j - 1] ? 0 : 1), Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1));
        int row = expected.Length, col = actual.Length, substitutions = 0, deletions = 0, insertions = 0;
        while (row > 0 || col > 0)
        {
            if (row > 0 && col > 0 && expected[row - 1] == actual[col - 1] && distance[row, col] == distance[row - 1, col - 1]) { row--; col--; }
            else if (row > 0 && col > 0 && distance[row, col] == distance[row - 1, col - 1] + 1) { substitutions++; row--; col--; }
            else if (row > 0 && distance[row, col] == distance[row - 1, col] + 1) { deletions++; row--; }
            else { insertions++; col--; }
        }
        return new(expected.Length, substitutions, deletions, insertions);
    }
}
