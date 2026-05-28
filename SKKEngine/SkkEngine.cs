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

public class SkkEngine(AppConfig config, SkkDicManager dictionary)
{
    public SkkContext Context { get; } = new();

    public SkkState State => Context.State;

    internal readonly KanaConverter kanaConverter = new(config);
    private readonly DictionaryRegistrar registrar = new(dictionary);
    internal Dictionary<string, string> zenkakuTable = config.ZenkakuTable;

    public SkkDicManager Dictionary { get; } = dictionary;

    public bool IsInRegistrationMode => registrar.IsInRegistrationMode;

    public void UpdateConfig(AppConfig config)
    {
        kanaConverter.UpdateTable(config);
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
        if (kanaConverter.ToFinish(Context.RomajiBuffer, out var fin))
        {
            HandleKanaProduced(fin!);
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

    internal string GetDictionaryKey() => Context.OkuriPrefix != null ? Context.ReadingBeforeOkuri + Context.OkuriPrefix : Context.CompositionBuffer;

    internal void TryConvertRomaji()
    {
        var canstart = Context.IsConversionMode && Context.OkuriPrefix != null && Context.CandidateIndex == -1;
        var (conversion, handleKana, newromaji) = kanaConverter.ToKanaConvert(Context.RomajiBuffer, canstart);

        if (handleKana is not null)
        {
            HandleKanaProduced(handleKana);
        }
        Context.RomajiBuffer.Clear();
        Context.RomajiBuffer.Append(newromaji);
        if (conversion)
        {
            StartConversion();
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
        string text = Context.CompositionBuffer;
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
                string bufferStr = Context.CompositionBuffer;
                var start = Math.Min(Context.ReadingBeforeOkuri.Length, bufferStr.Length);
                var okuriKana = bufferStr[start..];
                committedText += okuriKana;
                committedText += Context.RomajiBuffer;
            }
        }
        else
        {
            committedText = Context.CompositionBuffer + Context.RomajiBuffer;
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
