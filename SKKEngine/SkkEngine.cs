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

public class SkkEngine(Dictionary<string, string> romajiTable, Dictionary<string, string> zenkakuTable, SkkDicManager dictionary)
{
    public SkkContext Context { get; } = new();

    public SkkState State => Context.State;
    private StringBuilder romajiBuffer => Context.RomajiBuffer;
    private StringBuilder compositionBuffer => Context.CompositionBuffer;

    private readonly KanaConverter kanaConverter = new(romajiTable);
    private readonly DictionaryRegistrar registrar = new(dictionary);

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
        Dictionary.Reload(config.DictionaryPaths);
        Context.NotifyBufferChanged();
    }

    public string Composition => Context.Composition;

    public string CandidateList => Context.CandidateList;

    public bool ProcessKey(int vkCode, bool isKeyDown)
    {
        if (!isKeyDown)
        {
            return false;
        }

        var (ctrlPressed, shiftPressed) = Keyboard.GetMetaKeyState();

        if (ctrlPressed)
        {
            return HandleCtrlKey(vkCode);
        }

        // ESC: Ctrl+G と同様のキャンセル動作 (vi互換アプリ向けの透過送信は SkkController で処理済み)
        if (vkCode == SkkKeyConstants.VkEscape)
        {
            if (compositionBuffer.Length > 0 || romajiBuffer.Length > 0 || Context.CandidateIndex != -1 || IsInRegistrationMode || Context.IsConversionMode)
            {
                if (IsInRegistrationMode)
                {
                    CancelRegistration();
                }
                else
                {
                    ResetBuffers();
                }
                return true;
            }
            return false;
        }

        if (Context.State == SkkState.Disabled)
        {
            return false;
        }

        // TAB 補完の処理
        if (vkCode == SkkKeyConstants.VkTab && !shiftPressed)
        {
            if ((Context.IsConversionMode || Context.IsAbbreviationMode) && Context.CandidateIndex == -1) // ▽または / モードで読み入力中
            {
                if (Context.CompletionIndex == -1)
                {
                    // 補完開始
                    Context.OriginalReadingBeforeCompletion = compositionBuffer.ToString();
                    Context.Completions = [ .. Dictionary.GetCompletions(Context.OriginalReadingBeforeCompletion) ];
                    if (Context.Completions.Count > 0)
                    {
                        Context.CompletionIndex = 0;
                    }
                }
                else
                {
                    // 次の候補へ
                    Context.CompletionIndex = (Context.CompletionIndex + 1) % Context.Completions.Count;
                }

                Context.NotifyBufferChanged();
                return true;
            }
            // 補完中以外、または候補がない場合はパススルー
            return false;
        }

        // 補完中かつ TAB/Space 以外のキーが押された場合は補完を破棄
        if (Context.CompletionIndex >= 0 && vkCode != SkkKeyConstants.VkTab && vkCode != SkkKeyConstants.VkSpace)
        {
            Context.CompletionIndex = -1;
            Context.Completions.Clear();
            // compositionBuffer は originalReadingBeforeCompletion に戻さず、そのまま継続（仕様通り）
            // ただし、Composition プロパティで表示を切り替えているので、明示的に書き戻す必要があるかもしれないが、
            // 仕様では「補完候補を捨て、もともと入力されていた文字の続きへの入力とし」とある。
            // 補完中は compositionBuffer 自体は書き換えず表示のみ変えていたので、何もしなくて良い。
        }

        // Mode transitions
        if (vkCode == SkkKeyConstants.VkL && !shiftPressed && Context.State != SkkState.Zenkaku && !Context.IsAbbreviationMode) // l -> ASCII
        {
            CommitAll();
            ChangeState(SkkState.Disabled);
            return true;
        }

        if (vkCode == SkkKeyConstants.VkL && shiftPressed && Context.State != SkkState.Zenkaku && !Context.IsAbbreviationMode) // L -> Zenkaku
        {
            CommitAll();
            ChangeState(SkkState.Zenkaku);
            return true;
        }

        // Direct Full-width Alphanumeric input
        if (Context.State == SkkState.Zenkaku)
        {
            if (vkCode == SkkKeyConstants.VkBack || vkCode == SkkKeyConstants.VkReturn || vkCode == SkkKeyConstants.VkEscape || (vkCode >= 0x21 && vkCode <= 0x28))
            {
                return false; // Pass through: BS, Enter, Esc, Arrows, Home/End, PgUp/Dn
            }

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

        if (vkCode == SkkKeyConstants.VkReturn && !shiftPressed)
        {
            if (IsInRegistrationMode && compositionBuffer.Length == 0 && romajiBuffer.Length == 0 && Context.CandidateIndex == -1)
            {
                FinishRegistration();
                return true;
            }
            if (compositionBuffer.Length > 0 || romajiBuffer.Length > 0 || Context.CandidateIndex != -1)
            {
                CommitAll();
                return true;
            }
            return false;
        }

        if (Context.CandidateIndex >= 4 && vkCode >= 0x31 && vkCode <= 0x37)
        {
            var selection = vkCode - 0x31;
            var pageStart = (Context.CandidateIndex / 7) * 7;
            var targetIdx = pageStart + selection;
            if (targetIdx < Context.Candidates.Count)
            {
                Context.CandidateIndex = targetIdx;
                CommitAll();
                return true;
            }
        }

        if (vkCode == SkkKeyConstants.VkSpace && !shiftPressed)
        {
            // 補完確定からの漢字変換開始
            if (Context.CompletionIndex >= 0)
            {
                compositionBuffer.Clear();
                compositionBuffer.Append(Context.Completions[Context.CompletionIndex]);
                Context.CompletionIndex = -1;
                Context.Completions.Clear();
                StartConversion();
                Context.NotifyBufferChanged();
                return true;
            }

            if ((Context.IsConversionMode || Context.IsAbbreviationMode) && (compositionBuffer.Length > 0 || romajiBuffer.Length > 0))
            {
                if (Context.CandidateIndex == -1)
                {
                    StartConversion();
                }
                else
                {
                    Context.CandidateIndex++;
                    if (Context.CandidateIndex >= Context.Candidates.Count)
                    {
                        StartRegistration(GetDictionaryKey());
                    }
                }
                Context.NotifyBufferChanged();
                return true;
            }
        }

        if (vkCode == 0x51)
        {
            if (Context.CandidateIndex >= 0)
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
            if (!Context.IsConversionMode && !Context.IsAbbreviationMode && compositionBuffer.Length == 0 && Context.State != SkkState.Zenkaku)
            {
                Context.IsAbbreviationMode = true;
                Context.NotifyBufferChanged();
                return true;
            }
        }

        if (vkCode == SkkKeyConstants.VkBack)
        {
            if (Context.CandidateIndex >= 0)
            {
                Context.CandidateIndex = -1;
                Context.NotifyBufferChanged();
                return true;
            }
            if (romajiBuffer.Length > 0)
            {
                romajiBuffer.Remove(romajiBuffer.Length - 1, 1);
                Context.NotifyBufferChanged();
                return true;
            }
            if (compositionBuffer.Length > 0)
            {
                compositionBuffer.Remove(compositionBuffer.Length - 1, 1);
                if (compositionBuffer.Length == 0)
                {
                    Context.IsConversionMode = false;
                    Context.IsAbbreviationMode = false;
                    Context.OkuriPrefix = null;
                }
                else if (Context.OkuriPrefix != null && compositionBuffer.Length < Context.ReadingBeforeOkuri.Length)
                {
                    Context.OkuriPrefix = null;
                }
                Context.NotifyBufferChanged();
                return true;
            }
            if (IsInRegistrationMode)
            {
                registrar.RemoveLastBuffer();
                SyncRegistrationState();
                Context.NotifyBufferChanged();
                return true;
            }
            return false;
        }

        var c =Keyboard.VkToChar(vkCode, shiftPressed);
        if (c != '\0')
        {
            if (Context.CandidateIndex >= 0)
            {
                CommitAll();
            }
            if (Context.IsAbbreviationMode)
            {
                compositionBuffer.Append(c);
                Context.NotifyBufferChanged();
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
                    if (!Context.IsConversionMode)
                    {
                        Context.IsConversionMode = true;
                        Context.OkuriPrefix = null;
                        Context.ReadingBeforeOkuri = "";
                    }
                    else if (Context.OkuriPrefix == null && compositionBuffer.Length > 0)
                    {
                        if (romajiBuffer.Length == 1 && romajiBuffer[0] == 'n')
                        {
                            HandleKanaProduced("ん");
                            romajiBuffer.Clear();
                        }
                        Context.OkuriPrefix = char.ToLower(c, CultureInfo.CurrentCulture).ToString();
                        Context.ReadingBeforeOkuri = compositionBuffer.ToString();
                    }
                }

                romajiBuffer.Append(char.ToLower(c, CultureInfo.CurrentCulture));
                TryConvertRomaji();
                Context.NotifyBufferChanged();
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
        if (vkCode == SkkKeyConstants.VkJ) // Ctrl+J
        {
            if (Context.State == SkkState.Disabled)
            {
                ChangeState(SkkState.Hiragana);
            }
            else if (Context.State == SkkState.Zenkaku)
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

        bool hasActiveComposition = compositionBuffer.Length > 0 || romajiBuffer.Length > 0 || Context.CandidateIndex != -1 || IsInRegistrationMode;

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
        if ((vkCode == SkkKeyConstants.VkN || vkCode == SkkKeyConstants.VkP) && Context.CandidateIndex >= 0 && Context.Candidates.Count > 0)
        {
            if (vkCode == SkkKeyConstants.VkN)
            {
                Context.CandidateIndex++; // Ctrl+N
            }
            else
            {
                Context.CandidateIndex = (Context.CandidateIndex - 1 + Context.Candidates.Count) % Context.Candidates.Count; // Ctrl+P
            }

            if (Context.CandidateIndex >= Context.Candidates.Count)
            {
                StartRegistration(GetDictionaryKey());
            }
            Context.NotifyBufferChanged();
            return true;
        }

        // Ctrl+X: 漢字変換（候補選択）中のみ
        if (vkCode == SkkKeyConstants.VkX && Context.CandidateIndex >= 0 && Context.CandidateIndex < Context.Candidates.Count)
        {
            var word = Context.Candidates[Context.CandidateIndex];
            Dictionary.RemoveWord(GetDictionaryKey(), word);
            Context.Candidates.RemoveAt(Context.CandidateIndex);
            if (Context.Candidates.Count == 0)
            {
                Context.CandidateIndex = -1;
            }
            else
            {
                Context.CandidateIndex %= Context.Candidates.Count;
            }
            Context.NotifyBufferChanged();
            return true;
        }

        return false;
    }

    private void StartRegistration(string reading)
    {
        registrar.Start(reading, Context.State);
        ResetBuffers();
        ChangeState(SkkState.Hiragana);
        SyncRegistrationState();
    }

    private void CancelRegistration()
    {
        var prevState = registrar.Cancel();
        if (prevState.HasValue)
        {
            ChangeState(prevState.Value);
            ResetBuffers();
            SyncRegistrationState();
            Context.NotifyBufferChanged();
        }
    }

    private void FinishRegistration()
    {
        var result = registrar.Finish();
        if (result.HasValue)
        {
            ChangeState(result.Value.prevState);
            ResetBuffers();
            SyncRegistrationState();
            CommitProducedText(result.Value.word);
        }
        else if (IsInRegistrationMode) // Cancel case
        {
            var prevState = registrar.Cancel();
            if (prevState.HasValue)
            {
                ChangeState(prevState.Value);
            }
            ResetBuffers();
            SyncRegistrationState();
        }
        Context.NotifyBufferChanged();
    }

    private void SyncRegistrationState()
    {
        Context.RecursionDepth = registrar.RecursionDepth;
        Context.IsInRegistrationMode = registrar.IsInRegistrationMode;
        Context.RegistrationReading = registrar.RegistrationReading;
        Context.RegistrationWord = registrar.RegistrationWord;
    }

    private void StartConversion()
    {
        if (romajiBuffer.ToString() == "n")
        {
            HandleKanaProduced("ん");
            romajiBuffer.Clear();
        }
        var key = GetDictionaryKey();
        Context.Candidates = [ .. Dictionary.GetCandidates(key) ];
        if (Context.Candidates.Count > 0)
        {
            Context.CandidateIndex = 0;
        }
        else
        {
            DebugLogger.Log($"No candidates found for: {key}");
            StartRegistration(key);
        }
    }

    private string GetDictionaryKey() => Context.OkuriPrefix != null ? Context.ReadingBeforeOkuri + Context.OkuriPrefix : compositionBuffer.ToString();

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
                if (Context.IsConversionMode && Context.OkuriPrefix != null && Context.CandidateIndex == -1)
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
                if (Context.IsConversionMode && Context.OkuriPrefix != null && Context.CandidateIndex == -1)
                {
                    StartConversion();
                }
                continue;
            }

            if (kanaConverter.IsPotentialPrefix(romaji))
            {
                break;
            }
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
        if (Context.State == SkkState.Katakana)
        {
            kana = KanaConverter.HiraToKatakana(kana);
        }

        if (Context.IsConversionMode)
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
        if (Context.State == SkkState.Hiragana)
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
        if (Context.State == SkkState.Hiragana)
        {
            ChangeState(SkkState.Katakana);
        }
        else if (Context.State == SkkState.Katakana)
        {
            ChangeState(SkkState.Hiragana);
        }
    }

    public void RegisterWordAndCommit(string reading, string word)
    {
        Dictionary.AddWord(reading, word);
        CommitProducedText(word);
        ResetBuffers();
    }

    public void CommitAll()
    {
        string? committedText;
        if (Context.CandidateIndex >= 0 && Context.CandidateIndex < Context.Candidates.Count)
        {
            committedText = Context.Candidates[Context.CandidateIndex];
            Dictionary.AddWord(GetDictionaryKey(), committedText);
            if (Context.OkuriPrefix != null)
            {
                var bufferStr = compositionBuffer.ToString();
                var start = Math.Min(Context.ReadingBeforeOkuri.Length, bufferStr.Length);
                var okuriKana = bufferStr[start..];
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
        Context.OkuriPrefix = null;
        Context.ReadingBeforeOkuri = "";
        Context.IsConversionMode = false;
        Context.IsAbbreviationMode = false;
        Context.CandidateIndex = -1;
        Context.Candidates.Clear();
        Context.CompletionIndex = -1;
        Context.Completions.Clear();
        compositionBuffer.Clear();
        romajiBuffer.Clear();
        Context.NotifyBufferChanged();
    }

    private void ChangeState(SkkState newState)
    {
        if (Context.State != newState)
        {
            Context.State = newState;
        }
    }

    private void CommitProducedText(string text)
    {
        if (IsInRegistrationMode)
        {
            registrar.AppendBuffer(text);
            SyncRegistrationState();
            Context.NotifyBufferChanged();
        }
        else
        {
            OutputManager.SendString(text);
        }
    }
}

