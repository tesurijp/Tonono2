using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Tonono2.Win32;
using static Tonono2.Win32.NativeConstants;

namespace Tonono2.SKKEngine;

public enum SkkState
{
    Disabled,
    Hiragana,
    Katakana,
    Zenkaku,
    Hankaku
}

public class SkkEngine(Dictionary<string, string> romajiTable, Dictionary<string, string> zenkakuTable, SkkDicManager dictionary, Action UpdateUI)
{
    public SkkState State { get; private set; } = SkkState.Disabled;
    private readonly StringBuilder romajiBuffer = new();
    private readonly StringBuilder compositionBuffer = new();
    private bool isConversionMode;
    private bool isAbbreviationMode;
    private string? okuriPrefix;
    private string readingBeforeOkuri = "";

    private readonly KanaConverter kanaConverter = new(romajiTable);
    private readonly DictionaryRegistrar registrar = new(dictionary);
    private List<string> candidates = [];
    private int candidateIndex = -1;

    private readonly Action StateChanged = UpdateUI;
    private readonly Action BufferChanged = UpdateUI;

    public SkkDicManager Dictionary => dictionary;

    public string CurrentInput => romajiBuffer.ToString();
    public int RecursionDepth => registrar.RecursionDepth;
    public bool IsInRegistrationMode => registrar.IsInRegistrationMode;

    public string RegistrationReading => registrar.RegistrationReading;
    public string RegistrationWord => registrar.RegistrationWord;

    public void UpdateConfig(AppConfig config)
    {
        kanaConverter.UpdateTable(config.RomajiTable);
        zenkakuTable = config.ZenkakuTable;
        dictionary.Reload(config.DictionaryPaths);
        BufferChanged();
    }

    public string Composition
    {
        get
        {
            if (candidateIndex >= 0 && candidateIndex < candidates.Count)
            {
                var result = (candidateIndex >= 4) ? "▼" : "▼" + candidates[candidateIndex];
                if (okuriPrefix != null)
                {
                    var okuriDisplay = string.Concat(compositionBuffer.ToString().AsSpan(readingBeforeOkuri.Length), romajiBuffer.ToString());
                    result += "[" + okuriDisplay + "]";
                }
                return result;
            }
            var prefix = isAbbreviationMode ? " /" : (isConversionMode ? "▽" : "");
            return prefix + compositionBuffer.ToString() + romajiBuffer.ToString();
        }
    }

    public string CandidateList
    {
        get
        {
            if (candidateIndex >= 4)
            {
                var sb = new StringBuilder();
                var pageStart = (candidateIndex / 7) * 7;
                for (var i = 0; i < 7; i++)
                {
                    var idx = pageStart + i;
                    if (idx >= candidates.Count) break;
                    var label = (i + 1).ToString(CultureInfo.CurrentCulture);
                    var mark = (idx == candidateIndex) ? "*" : " ";
                    sb.Append(label);
                    sb.Append(':');
                    sb.Append(candidates[idx]);
                    sb.Append(mark);
                    sb.Append(' ');
                    //sb.Append($"{label}:{candidates[idx]}{mark} ");
                }
                return sb.ToString();
            }
            return string.Empty;
        }
    }

    public bool ProcessKey(int vkCode, bool isKeyDown)
    {
        if (!isKeyDown) return false;

        var (ctrlPressed , shiftPressed) = Keyboard.GetMetaKeyState();

        if (ctrlPressed)
        {
            return HandleCtrlKey(vkCode);
        }

        if (State == SkkState.Disabled)
        {
            return false;
        }

        // Mode transitions
        if (vkCode == 0x4C && !shiftPressed && State != SkkState.Zenkaku) // l -> ASCII
        {
            CommitAll();
            ChangeState(SkkState.Disabled);
            return true;
        }

        if (vkCode == 0x4C && shiftPressed && State != SkkState.Zenkaku) // L -> Zenkaku
        {
            CommitAll();
            ChangeState(SkkState.Zenkaku);
            return true;
        }

        // Direct Full-width Alphanumeric input
        if (State == SkkState.Zenkaku)
        {
            if (vkCode == 0x08 || vkCode == 0x0D || vkCode == 0x1B || (vkCode >= 0x21 && vkCode <= 0x28))
                return false; // Pass through: BS, Enter, Esc, Arrows, Home/End, PgUp/Dn

            var cz = Keyboard.VkToChar(vkCode, shiftPressed);
            if (cz != '\0')
            {
                if (zenkakuTable.TryGetValue(cz.ToString(), out var zenkaku))
                {
                    CommitProducedText(zenkaku);
                }
                else
                {
                    CommitProducedText(cz.ToString());
                }
                return true;
            }
            return false;
        }

        if (vkCode == 0x0D && !shiftPressed)
        {
            if (IsInRegistrationMode && compositionBuffer.Length == 0 && romajiBuffer.Length == 0 && candidateIndex == -1)
            {
                FinishRegistration();
                return true;
            }
            if (compositionBuffer.Length > 0 || romajiBuffer.Length > 0 || candidateIndex != -1)
            {
                CommitAll();
                return true;
            }
            return false;
        }

        if (candidateIndex >= 4 && vkCode >= 0x31 && vkCode <= 0x37)
        {
            var selection = vkCode - 0x31;
            var pageStart = (candidateIndex / 7) * 7;
            var targetIdx = pageStart + selection;
            if (targetIdx < candidates.Count)
            {
                candidateIndex = targetIdx;
                CommitAll();
                return true;
            }
        }

        if (vkCode == 0x20 && !shiftPressed)
        {
            if ((isConversionMode || isAbbreviationMode) && (compositionBuffer.Length > 0 || romajiBuffer.Length > 0))
            {
                if (candidateIndex == -1)
                {
                    StartConversion();
                }
                else
                {
                    candidateIndex++;
                    if (candidateIndex >= candidates.Count)
                    {
                        StartRegistration(GetDictionaryKey());
                    }
                }
                BufferChanged();
                return true;
            }
        }

        if (vkCode == 0x51)
        {
            if (candidateIndex >= 0)
            {
                CommitAll();
                return true;
            }
            if (compositionBuffer.Length > 0 || romajiBuffer.Length > 0)
            {
                FlipAndCommit();
            }
            else
            {
                ToggleHiraganaKatakana();
            }
            return true;
        }

        if (vkCode == 0xBF && !shiftPressed)
        {
            if (!isConversionMode && !isAbbreviationMode && compositionBuffer.Length == 0 && State != SkkState.Zenkaku)
            {
                isAbbreviationMode = true;
                BufferChanged();
                return true;
            }
        }

        if (vkCode == 0x08)
        {
            if (candidateIndex >= 0)
            {
                candidateIndex = -1;
                BufferChanged();
                return true;
            }
            if (romajiBuffer.Length > 0)
            {
                romajiBuffer.Remove(romajiBuffer.Length - 1, 1);
                BufferChanged();
                return true;
            }
            if (compositionBuffer.Length > 0)
            {
                compositionBuffer.Remove(compositionBuffer.Length - 1, 1);
                if (compositionBuffer.Length == 0)
                {
                    isConversionMode = false;
                    isAbbreviationMode = false;
                    okuriPrefix = null;
                }
                else if (okuriPrefix != null && compositionBuffer.Length < readingBeforeOkuri.Length)
                {
                    okuriPrefix = null;
                }
                BufferChanged();
                return true;
            }
            if (IsInRegistrationMode)
            {
                registrar.RemoveLastBuffer();
                BufferChanged();
                return true;
            }
            return false;
        }

        var c =Keyboard.VkToChar(vkCode, shiftPressed);
        if (c != '\0')
        {
            if (candidateIndex >= 0)
            {
                CommitAll();
            }
            if (isAbbreviationMode)
            {
                compositionBuffer.Append(c);
                BufferChanged();
                return true;
            }

            var isSymbol = !char.IsLetter(c) && !char.IsDigit(c);
            var canMatch = kanaConverter.ToKana(c.ToString()) != string.Empty || kanaConverter.IsPotentialPrefix(c.ToString());
            if (isSymbol && !canMatch)
            {
                CommitAll();
                CommitProducedText(c.ToString());
                return true;
            }

            if (c != ' ')
            {
                if (char.IsUpper(c) && char.IsLetter(c))
                {
                    if (!isConversionMode)
                    {
                        isConversionMode = true;
                        okuriPrefix = null;
                        readingBeforeOkuri = "";
                    }
                    else if (okuriPrefix == null && compositionBuffer.Length > 0)
                    {
                        okuriPrefix = char.ToLower(c, CultureInfo.CurrentCulture).ToString();
                        readingBeforeOkuri = compositionBuffer.ToString();
                    }
                }

                romajiBuffer.Append(char.ToLower(c, CultureInfo.CurrentCulture));
                TryConvertRomaji();
                BufferChanged();
                return true;
            }
            else
            {
                CommitAll();
                CommitProducedText(c.ToString());
                return true;
            }
        }
        return false;
    }

    private bool HandleCtrlKey(int vkCode)
    {
        // 常に許可
        if (vkCode == 0x4A) // Ctrl+J
        {
            if (State == SkkState.Disabled)
            {
                ChangeState(SkkState.Hiragana);
            }
            else if (State == SkkState.Zenkaku)
            {
                CommitAll();
                ChangeState(SkkState.Hiragana);
            }
            else
            {
                CommitAll();
            }
            return true;
        }

        bool hasActiveComposition = compositionBuffer.Length > 0 || romajiBuffer.Length > 0 || candidateIndex != -1 || IsInRegistrationMode;

        // Ctrl+G: 入力・変換中ならキャンセル
        if (vkCode == 0x47 && hasActiveComposition)
        {
            if (IsInRegistrationMode)
            {
                CancelRegistration();
                return true;
            }
            ResetBuffers();
            return true;
        }

        // Ctrl+N / Ctrl+P: 変換中のみ
        if ((vkCode == 0x4E || vkCode == 0x50) && candidateIndex >= 0 && candidates.Count > 0)
        {
            if (vkCode == 0x4E) candidateIndex++; // Ctrl+N
            else candidateIndex = (candidateIndex - 1 + candidates.Count) % candidates.Count; // Ctrl+P

            if (candidateIndex >= candidates.Count)
            {
                StartRegistration(GetDictionaryKey());
            }
            BufferChanged();
            return true;
        }

        // Ctrl+X: 漢字変換（候補選択）中のみ
        if (vkCode == 0x58 && candidateIndex >= 0 && candidateIndex < candidates.Count)
        {
            var word = candidates[candidateIndex];
            dictionary.RemoveWord(GetDictionaryKey(), word);
            candidates.RemoveAt(candidateIndex);
            if (candidates.Count == 0) candidateIndex = -1;
            else candidateIndex %= candidates.Count;
            BufferChanged();
            return true;
        }

        return false;
    }

    private void StartRegistration(string reading)
    {
        registrar.Start(reading, this.State);
        ResetBuffers();
        ChangeState(SkkState.Hiragana);
    }

    private void CancelRegistration()
    {
        var prevState = registrar.Cancel();
        if (prevState.HasValue)
        {
            ChangeState(prevState.Value);
            ResetBuffers();
            BufferChanged();
        }
    }

    private void FinishRegistration()
    {
        var result = registrar.Finish();
        if (result.HasValue)
        {
            ChangeState(result.Value.prevState);
            ResetBuffers();
            CommitProducedText(result.Value.word);
        }
        else if (IsInRegistrationMode) // Cancel case
        {
            var prevState = registrar.Cancel();
            if (prevState.HasValue) ChangeState(prevState.Value);
            ResetBuffers();
        }
        BufferChanged();
    }

    private void StartConversion()
    {
        if (romajiBuffer.ToString() == "n")
        {
            HandleKanaProduced("ん");
            romajiBuffer.Clear();
        }
        var key = GetDictionaryKey();
        candidates = [ .. dictionary.GetCandidates(key) ];
        if (candidates.Count > 0)
        {
            candidateIndex = 0;
        }
        else
        {
            DebugLogger.Log($"No candidates found for: {key}");
            StartRegistration(key);
        }
    }

    private string GetDictionaryKey()
    {
        if (okuriPrefix != null)
        {
            return readingBeforeOkuri + okuriPrefix;
        }
        return compositionBuffer.ToString();
    }

    private void TryConvertRomaji()
    {
        while (romajiBuffer.Length > 0)
        {
            var romaji = romajiBuffer.ToString();

            var kana = kanaConverter.ToKana(romaji);
            if (!string.IsNullOrEmpty(kana))
            {
                HandleKanaProduced(kana);
                romajiBuffer.Clear();
                if (isConversionMode && okuriPrefix != null && candidateIndex == -1)
                {
                    StartConversion();
                }
                break;
            }

            if (romaji.StartsWith('n') && romaji.Length >= 2)
            {
                var next = romaji[1];
                var isVowel = "aiueoyn".Contains(next);
                if (!isVowel)
                {
                    HandleKanaProduced("ん");
                    romajiBuffer.Remove(0, 1);
                    continue;
                }
                else if (next == 'n')
                {
                    HandleKanaProduced("ん");
                    romajiBuffer.Remove(0, 2);
                    continue;
                }
            }

            if (romaji.Length >= 2 && romaji[0] == romaji[1] && romaji[0] != 'n' && char.IsLetter(romaji[0]))
            {
                HandleKanaProduced("っ");
                romajiBuffer.Remove(0, 1);
                if (isConversionMode && okuriPrefix != null && candidateIndex == -1)
                {
                    StartConversion();
                }
                continue;
            }

            if (kanaConverter.IsPotentialPrefix(romaji)) break;
            else
            {
                DebugLogger.Log($"No match in romaji table for: {romajiBuffer}. Flushing: {romajiBuffer[0]}");
                HandleKanaProduced(romajiBuffer[0].ToString());
                romajiBuffer.Remove(0, 1);
            }
        }
    }

    private void HandleKanaProduced(string kana)
    {
        if (State == SkkState.Katakana)
        {
            kana = KanaConverter.HiraToKatakana(kana);
        }
        if (isConversionMode)
        {
            compositionBuffer.Append(kana);
        }
        else
        {
            CommitProducedText(kana);
        }
    }

    private void FlipAndCommit()
    {
        var text = compositionBuffer.ToString();
        if (State == SkkState.Hiragana)
        {
            text = KanaConverter.HiraToKatakana(text);
        }
        else
        {
            text = KanaConverter.KataToHiragana(text);
        }
        CommitProducedText(text);
        ResetBuffers();
    }

    private void ToggleHiraganaKatakana()
    {
        if (State == SkkState.Hiragana)
        {
            ChangeState(SkkState.Katakana);
        }
        else if (State == SkkState.Katakana)
        {
            ChangeState(SkkState.Hiragana);
        }
    }

    public void RegisterWordAndCommit(string reading, string word)
    {
        dictionary.AddWord(reading, word);
        CommitProducedText(word);
        ResetBuffers();
    }

    public void CommitAll()
    {
        string? committedText;
        if (candidateIndex >= 0 && candidateIndex < candidates.Count)
        {
            committedText = candidates[candidateIndex];
            dictionary.AddWord(GetDictionaryKey(), committedText);
            if (okuriPrefix != null)
            {
                var okuriKana = compositionBuffer.ToString()[readingBeforeOkuri.Length..];
                committedText += okuriKana;
                committedText += romajiBuffer.ToString();
            }
        }
        else
        {
            committedText = compositionBuffer.ToString() + romajiBuffer.ToString();
        }
        if (!string.IsNullOrEmpty(committedText))
        {
            CommitProducedText(committedText);
        }
        ResetBuffers();
    }

    public void CancelAndDisable()
    {
        ResetBuffers();
        ChangeState(SkkState.Disabled);
    }

    private void ResetBuffers()
    {
        compositionBuffer.Clear();
        romajiBuffer.Clear();
        isConversionMode = false;
        isAbbreviationMode = false;
        candidateIndex = -1;
        candidates.Clear();
        okuriPrefix = null;
        readingBeforeOkuri = "";
        BufferChanged();
    }

    private void ChangeState(SkkState newState)
    {
        if (State != newState)
        {
            State = newState;
            StateChanged();
        }
    }

    private void CommitProducedText(string text)
    {
        if (IsInRegistrationMode)
        {
            registrar.AppendBuffer(text);
            BufferChanged();
        }
        else
        {
            OutputManager.SendString(text);
        }
    }
}
