using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace Tonono2.SKKEngine;

public class KanaConverter(Dictionary<string, string> romajiTable)
{
    public Dictionary<string, string> RomajiToKana { get; set; } = romajiTable;

    public void UpdateTable(Dictionary<string, string> romajiTable) => RomajiToKana = romajiTable;

    public bool IsPotentialPrefix(string romaji) => !string.IsNullOrEmpty(romaji) && RomajiToKana.Keys.Any(k => k.StartsWith(romaji, StringComparison.CurrentCulture));

    public string ToKana(string romaji) => RomajiToKana.TryGetValue(romaji, out var hiragana) ? hiragana : string.Empty;

    public static string HiraToKatakana(string hiragana) => KanaToKana(hiragana, c => (c >= 'ぁ' && c <= 'ゖ'), 0x60);
    public static string KataToHiragana(string katakana) => KanaToKana(katakana, c => (c >= 'ァ' && c <= 'ヶ'), -0x60);
    private static string KanaToKana(string kana, Func<char, bool> rangePredict, int offset)
    {
        var sb = new StringBuilder();
        foreach (var c in kana)
        {
            if (rangePredict(c))
                sb.Append((char)(c + offset));
            else
                sb.Append(c);
        }
        return sb.ToString();
    }

}
