using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tonono2.SKKEngine;

public class KanaConverter(AppConfig config)
{
    private Dictionary<string, string> RomajiToKana { get; set; } = config.RomajiTable;
    private Dictionary<string, string> MoraModifier { get; set; } = config.MoraModifier;
    private Dictionary<string, string> MoraAutoComplete { get; set; } = config.MoraAutoComplete;

    public void UpdateTable(AppConfig config)
    {
        RomajiToKana = config.RomajiTable;
        MoraModifier = config.MoraModifier;
    }

    private bool IsPotentialPrefix(string romaji) => RomajiToKana.Keys.Any(k => k.StartsWith(romaji, StringComparison.Ordinal));
    public bool CanMatch(string c) => RomajiToKana.ContainsKey(c) || IsPotentialPrefix(c);
    public (bool StartConversin, string output, string resultRomaji) ToKanaConvert(string romaji, bool start)
    {
        if (RomajiToKana.TryGetValue(romaji, out var kana))
        {
            return (start, kana, "");
        }

        if (MoraModifier.TryGetValue(romaji, out var mora))
        {
            return (false, mora, romaji[1..]);
        }

        if (IsPotentialPrefix(romaji))
        {
            return (false, "", romaji);
        }

        DebugLogger.Log($"No match in romaji table for: {romaji}. Flushing: {romaji[..1]}");
        return (false, romaji[..1], romaji[1..]);
    }

    public bool ToFinish(string romaji, out string? mora) => MoraAutoComplete.TryGetValue(romaji, out mora);
    public static string HiraToKatakana(string hiragana) => KanaToKana(hiragana, c => (c >= 'ぁ' && c <= 'ゖ') ? (char)(c + 0x60) : c);
    public static string KataToHiragana(string katakana) => KanaToKana(katakana, c => (c >= 'ァ' && c <= 'ヶ') ? (char)(c - 0x60) : c);
    private static string KanaToKana(string kana, Func<char,char> addOffset)
    {
        var sb = new StringBuilder();
        foreach (var c in kana)
        {
            var ch = addOffset(c);
            sb.Append(ch);
        }
        return sb.ToString();
    }

}
