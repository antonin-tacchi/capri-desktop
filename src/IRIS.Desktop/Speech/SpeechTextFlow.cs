using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using IRIS.Core.Contracts;

namespace IRIS.Desktop.Speech;

internal sealed record SpeechChunk(int Start, string Text);
internal static class SpeechTextFlow
{
    public static IReadOnlyList<SpeechChunk> Chunks(string text)
    {
        var chunks = new List<SpeechChunk>(); int start = 0;
        while (start < text.Length)
        {
            int length = Math.Min(640, text.Length - start);
            if (length < text.Length - start)
            {
                int cut = -1;
                for (int i = length - 1; i > length / 2; i--)
                    if (text[start + i] is '.' or '!' or '?' or '\n' && char.IsWhiteSpace(text[start + i + 1])) { cut = i + 1; break; }
                if (cut < 0) for (int i = length - 1; i > 0; i--) if (char.IsWhiteSpace(text[start + i])) { cut = i + 1; break; }
                if (cut > 0) length = cut;
                else if (char.IsHighSurrogate(text[start + length - 1])) length--;
            }
            chunks.Add(new(start, text.Substring(start, length))); start += length;
        }
        return chunks;
    }
}
internal sealed class SpeechProgressTimeline
{
    private readonly string text;
    private readonly SpeechWordBoundary[] words;
    private readonly int[] elements;
    private readonly TimeSpan duration;
    public SpeechProgressTimeline(string text, IReadOnlyList<SpeechWordBoundary> words, TimeSpan duration)
    {
        this.text = text; this.duration = duration; elements = StringInfo.ParseCombiningCharacters(text);
        this.words = words.Where(w => w.Start >= 0 && w.Length > 0 && w.Start + (long)w.Length <= text.Length && w.Position >= TimeSpan.Zero && w.Position <= duration).OrderBy(w => w.Position).ToArray();
    }
    public int At(TimeSpan position)
    {
        int end = 0;
        for (int i = 0; i < words.Length; i++)
        {
            SpeechWordBoundary mark = words[i]; if (mark.Position > position) break;
            int length = mark.Length;
            if (length > 28)
            {
                TimeSpan until = i + 1 < words.Length ? words[i + 1].Position : duration;
                double ratio = Math.Clamp((position - mark.Position).TotalSeconds / Math.Max(0.01, (until - mark.Position).TotalSeconds), 0, 1);
                length = (int)Math.Ceiling(length * ratio);
            }
            end = Math.Max(end, mark.Start + length);
        }
        if (end >= text.Length) return text.Length;
        int found = Array.BinarySearch(elements, end);
        return found >= 0 ? end : ~found < elements.Length ? elements[~found] : text.Length;
    }
}
