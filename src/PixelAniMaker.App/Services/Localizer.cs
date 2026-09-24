using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace PixelAniMaker.App.Services;

/// <summary>
/// UI language. The program is written in Korean; for English, every text is looked up in
/// Assets/Lang/en.json (Korean → English) as it is shown, so views and view models need no changes.
/// Keys with {0}, {1} … are patterns for composed messages; the parts they capture are translated too.
/// The language is chosen at start-up (changing it applies after a restart).
/// </summary>
public static partial class Localizer
{
    public const string Korean = "ko";
    public const string English = "en";

    private static readonly Dictionary<string, string> Exact = [];
    private static readonly List<(Regex Pattern, string Format, int Literal)> Patterns = [];

    public static string Language { get; private set; } = Korean;

    /// <summary>Loads the table and hooks the controls that show text. Call once before any window opens.</summary>
    public static void Initialize(string? language)
    {
        if (language != English)
            return;
        Language = English;
        Load(ReadTable(English));
        TextBlock.TextProperty.Changed.AddClassHandler<TextBlock>((block, _) => Translate(block, TextBlock.TextProperty));
        Window.TitleProperty.Changed.AddClassHandler<Window>((window, _) => Translate(window, Window.TitleProperty));
        Avalonia.Controls.Documents.Run.TextProperty.Changed.AddClassHandler<Avalonia.Controls.Documents.Run>(
            (run, _) => Translate(run, Avalonia.Controls.Documents.Run.TextProperty));
    }

    /// <summary>The text in the current language (unchanged when there is no translation).</summary>
    public static string T(string text) => Language == Korean ? text : Lookup(text, depth: 0);

    /// <summary>Loads a Korean → English table (keys with {n} become patterns).</summary>
    public static void Load(IReadOnlyDictionary<string, string> table)
    {
        Exact.Clear();
        Patterns.Clear();
        foreach (var (ko, en) in table)
        {
            if (!HolePattern().IsMatch(ko))
            {
                Exact[ko] = en;
                continue;
            }
            string regex = "^" + HolePattern().Replace(Regex.Escape(ko).Replace(@"\{", "{"), m => $"(?<h{m.Groups[1].Value}>.*?)") + "$";
            Patterns.Add((new Regex(regex, RegexOptions.Singleline), en, HolePattern().Replace(ko, "").Length));
        }
        Patterns.Sort((a, b) => b.Literal - a.Literal); // most specific first
    }

    private static string Lookup(string text, int depth)
    {
        if (text.Length == 0 || depth > 3 || !HasHangul(text))
            return text;
        string core = text.Trim();
        if (core.Length != text.Length)
        {
            int start = text.IndexOf(core, StringComparison.Ordinal);
            return text[..start] + Lookup(core, depth) + text[(start + core.Length)..];
        }
        if (Exact.TryGetValue(text, out var hit))
            return hit;
        foreach (var (pattern, format, _) in Patterns)
        {
            var m = pattern.Match(text);
            if (m.Success)
                return HolePattern().Replace(format, h => Lookup(m.Groups["h" + h.Groups[1].Value].Value, depth + 1));
        }
        return text;
    }

    private static void Translate(AvaloniaObject target, StyledProperty<string?> property)
    {
        if (target.GetValue(property) is { } text && T(text) is var translated && translated != text)
            target.SetCurrentValue(property, translated); // keeps bindings: the next bound value is translated again
    }

    private static Dictionary<string, string> ReadTable(string language)
    {
        using var stream = AssetLoader.Open(new Uri($"avares://PixelAniMaker/Assets/Lang/{language}.json"));
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? [];
    }

    private static bool HasHangul(string s) => s.Any(c => c is >= '가' and <= '힣');

    [GeneratedRegex(@"\{(\d+)\}")]
    private static partial Regex HolePattern();
}
