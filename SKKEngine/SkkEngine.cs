using System;
using System.Collections.Generic;
using Tonono2.SKKEngine.States;
using Tonono2.Win32;

namespace Tonono2.SKKEngine;

public record class SkkKeyCommand(int VkCode, bool Shift, bool Control, char? Ch);

public enum SkkState : int
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

    internal readonly KanaConverter kanaConverter = new(romajiTable);
    private readonly DictionaryRegistrar registrar = new(dictionary);
    internal Dictionary<string, string> zenkakuTable = zenkakuTable;

    public SkkDicManager Dictionary { get; } = dictionary;

    public bool IsInRegistrationMode => registrar.IsInRegistrationMode;

    public void UpdateConfig(AppConfig config)
    {
        kanaConverter.UpdateTable(config.RomajiTable);
        zenkakuTable = config.ZenkakuTable;
        Dictionary.Reload(config.DictionaryPaths);
    }

    public bool ProcessKey(int vkCode, bool isKeyDown)
    {
        if (!isKeyDown)
        {
            return false;
        }

        var (ctrlPressed, shiftPressed) = Keyboard.GetMetaKeyState();
        var ch = Keyboard.VkToChar(vkCode, shiftPressed);
        var command = new SkkKeyCommand(vkCode, shiftPressed, ctrlPressed, ch == '\0' ? null : ch);

        var result = Context.ProcessKey(this, command);
        result.Invoke(this);
        return result.IsHandled;
    }

    internal void StartRegistration(string reading)
    {
        registrar.Start(reading, Context.State);
        ResetBuffers();
        ChangeState(SkkState.Hiragana);
        SyncRegistrationState();
    }

    internal void CancelRegistration()
    {
        var prevState = registrar.Cancel();
        if (prevState.HasValue)
        {
            ChangeState(prevState.Value);
            ResetBuffers();
            SyncRegistrationState();
        }
    }

    internal void FinishRegistration()
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
    }

    internal void HandleRegistrationBackspace()
    {
        if (IsInRegistrationMode)
        {
            registrar.RemoveLastBuffer();
            SyncRegistrationState();
        }
    }

    private void SyncRegistrationState()
    {
        Context.RecursionDepth = registrar.RecursionDepth;
        Context.IsInRegistrationMode = registrar.IsInRegistrationMode;
        Context.RegistrationReading = registrar.RegistrationReading;
        Context.RegistrationWord = registrar.RegistrationWord;
    }

    internal void StartConversion()
    {
        if (Context.RomajiBuffer.ToString() == "n")
        {
            HandleKanaProduced("ん");
            Context.RomajiBuffer.Clear();
        }
        var key = GetDictionaryKey();
        Context.Candidates = [ .. Dictionary.GetCandidates(key) ];
        if (Context.Candidates.Count > 0)
        {
            Context.CandidateIndex = 0;
            ChangeState(Context.State); // Transitions to ConversionState
        }
        else
        {
            DebugLogger.Log($"No candidates found for: {key}");
            StartRegistration(key);
        }
    }

    internal string GetDictionaryKey() => Context.OkuriPrefix != null ? Context.ReadingBeforeOkuri + Context.OkuriPrefix : Context.CompositionBuffer.ToString();

    internal void TryConvertRomaji()
    {
        while (Context.RomajiBuffer.Length > 0)
        {
            var romaji = Context.RomajiBuffer.ToString();

            var kana = kanaConverter.ToKana(romaji);
            if (!string.IsNullOrEmpty(kana))
            {
                HandleKanaProduced(kana);
                Context.RomajiBuffer.Clear();
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
                    Context.RomajiBuffer.Remove(0, 1);
                    continue;
                }
                else if (next == 'n')
                {
                    HandleKanaProduced("ん");
                    Context.RomajiBuffer.Remove(0, 2);
                    continue;
                }
            }

            if (romaji.Length >= 2 && romaji[0] == romaji[1] && romaji[0] != 'n' && char.IsLetter(romaji[0]))
            {
                HandleKanaProduced("っ");
                Context.RomajiBuffer.Remove(0, 1);
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
                DebugLogger.Log($"No match in romaji table for: {Context.RomajiBuffer}. Flushing: {Context.RomajiBuffer[0]}");
                HandleKanaProduced(Context.RomajiBuffer[0].ToString());
                Context.RomajiBuffer.Remove(0, 1);
            }
        }
    }

    internal void HandleKanaProduced(string kana)
    {
        if (Context.State == SkkState.Katakana)
        {
            kana = KanaConverter.HiraToKatakana(kana);
        }

        if (Context.IsConversionMode)
        {
            Context.CompositionBuffer.Append(kana);
            ChangeState(Context.State); // Ensure we are in CompositionState
        }
        else
        {
            CommitProducedText(kana);
        }
    }

    internal void FlipAndCommit()
    {
        var text = Context.CompositionBuffer.ToString();
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

    internal void ToggleHiraganaKatakana()
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

    internal void CommitAll()
    {
        string? committedText;
        if (Context.CandidateIndex >= 0 && Context.CandidateIndex < Context.Candidates.Count)
        {
            committedText = Context.Candidates[Context.CandidateIndex];
            Dictionary.AddWord(GetDictionaryKey(), committedText);
            if (Context.OkuriPrefix != null)
            {
                var bufferStr = Context.CompositionBuffer.ToString();
                var start = Math.Min(Context.ReadingBeforeOkuri.Length, bufferStr.Length);
                var okuriKana = bufferStr[start..];
                committedText += okuriKana;
                committedText += Context.RomajiBuffer.ToString();
            }
        }
        else
        {
            committedText = Context.CompositionBuffer.ToString() + Context.RomajiBuffer.ToString();
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

    internal void ResetBuffers()
    {
        Context.ResetBuffers();
        ChangeState(Context.State);
    }

    internal void ChangeState(SkkState newState)
    {
        Context.State = newState;
        if (IsInRegistrationMode)
        {
            Context.ProcessKey = RegistrationState.ProcessKey;
        }
        else if (Context.CandidateIndex >= 0)
        {
            Context.ProcessKey = ConversionState.ProcessKey;
        }
        else if (Context.IsConversionMode || Context.IsAbbreviationMode || Context.CompositionBuffer.Length > 0 || Context.RomajiBuffer.Length > 0)
        {
            Context.ProcessKey = CompositionState.ProcessKey;
        }
        else
        {
            Context.ProcessKey = newState switch
            {
                SkkState.Disabled => DisabledState.ProcessKey,
                SkkState.Hiragana => IdleState.ProcessKey,
                SkkState.Katakana => IdleState.ProcessKey,
                SkkState.Zenkaku => ZenkakuState.ProcessKey,
                _ => Context.ProcessKey
            };
        }
    }

    internal void CommitProducedText(string text)
    {
        if (IsInRegistrationMode)
        {
            registrar.AppendBuffer(text);
            SyncRegistrationState();
        }
        else
        {
            OutputManager.SendString(text);
        }
    }
}
