using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Xml.Serialization;
using Tonono2.SKKEngine.States;

namespace Tonono2.SKKEngine;

public class StrBuf
{
    private readonly StringBuilder sb = new();
    private StrBuf WrapAction(Action act)
    {
        act();
        return this;
    }
    public StrBuf Append(string str) => WrapAction(() => sb.Append(str));
    public StrBuf Append(char ch) => WrapAction(() => sb.Append(ch));
    public StrBuf Remove(int st, int len) => WrapAction(() => sb.Remove(st, len));
    public StrBuf Clear() => WrapAction(() => sb.Clear());
    public int Length => sb.Length;
    public char? First => sb.Length>0 ? sb[0] : null;
    public override string ToString() => sb.ToString();

    public static implicit operator string(StrBuf current) => current.ToString();
}

public class SkkContext : INotifyPropertyChanged
{
    public Func<SkkEngine, SkkKeyCommand, SkkActionResult> ProcessKey { get; set; } = DisabledState.ProcessKey;
    public StrBuf RomajiBuffer { get; } = new();
    public StrBuf CompositionBuffer { get; } = new();
    public SkkState State { get; set; } = SkkState.Disabled;
    public bool IsConversionMode { get; set; }
    public bool IsAbbreviationMode { get; set; }
    public string? OkuriPrefix { get; set; }
    public string ReadingBeforeOkuri { get; set; } = "";
    public List<string> Candidates { get; set; } = [];
    public int CandidateIndex { get; set; } = 1;
    public List<string> Completions { get; set; } = [];
    public int CompletionIndex { get; set; } = -1;
    public string OriginalReadingBeforeCompletion { get; set; } = "";
    public int RecursionDepth { get; set; }
    public bool IsInRegistrationMode { get; set; }
    public string RegistrationReading { get; set; } = "";
    public string RegistrationWord { get; set; } = "";
    public string StatusText { get; set; } = "";
    public string Composition { get; set; } = "";
    public string CandidateList { get; set; } = "";
    public bool ListConversion => CandidateIndex >= 4;
    public static int ListPageSize => 7;
    public int PageStart => (CandidateIndex / ListPageSize) * ListPageSize;

    private string MakeStatusText()
    {
        var sb = new StringBuilder();
        sb.Append('[', RecursionDepth + 1);
        var state = State switch
        {
            SkkState.Hiragana => "あ",
            SkkState.Katakana => "ア",
            SkkState.Zenkaku => "全",
            _ => "？"
        };
        sb.Append(state);
        sb.Append(']', RecursionDepth + 1);
        return sb.ToString();
    }
    private string MakeComposition()
    {
        if (CandidateIndex >= 0 && CandidateIndex < Candidates.Count)
        {
            var sb = new StringBuilder();
            sb.Append(SkkConstants.ConvertPrefix);
            if (CandidateIndex < 4) sb.Append(Candidates[CandidateIndex]);
            if (OkuriPrefix != null)
            {
                string bufferStr = CompositionBuffer;
                var start = Math.Min(ReadingBeforeOkuri.Length, bufferStr.Length);
                var okuriDisplay = string.Concat(bufferStr.AsSpan(start), RomajiBuffer);
                sb.Append('[');
                sb.Append(okuriDisplay);
                sb.Append(']');
            }
            return sb.ToString();
        }

        if (CompletionIndex >= 0 && CompletionIndex < Completions.Count)
        {
            return $"{SkkConstants.CompositionPrefix}{Completions[CompletionIndex]}{RomajiBuffer}";
        }
        return $"{SkkConstants.CompositionPrefix}{CompositionBuffer}{RomajiBuffer}";
    }
    private string MakeCandidateList()
    {
        if (ListConversion)
        {
            var sb = new StringBuilder();
            var pageStart = (CandidateIndex / ListPageSize) * ListPageSize;
            for (var i = 0; i < ListPageSize; i++)
            {
                var idx = pageStart + i;
                if (idx >= Candidates.Count)
                {
                    break;
                }
                var labels = "ASDFJKL";
                var mark = (idx == CandidateIndex) ? $"[{labels[i]}] : " : $" {labels[i]}  : ";
                sb.Append(mark);
                sb.Append(Candidates[idx]);
                sb.Append(' ');
            }
            return sb.ToString();
        }
        return "";
    }
    public bool IsBufferActive => CompositionBuffer.Length > 0 || RomajiBuffer.Length > 0;
    public bool IsVisible => IsInRegistrationMode || IsBufferActive;

    internal void ResetBuffers()
    {
        OkuriPrefix = null;
        ReadingBeforeOkuri = "";
        IsConversionMode = false;
        IsAbbreviationMode = false;
        CandidateIndex = -1;
        Candidates.Clear();
        CompletionIndex = -1;
        Completions.Clear();
        CompositionBuffer.Clear();
        RomajiBuffer.Clear();
    }
    internal void NotifyBufferChanged()
    {
        Composition = MakeComposition();
        CandidateList = MakeCandidateList();
        StatusText = MakeStatusText();
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(RegistrationReading));
        OnPropertyChanged(nameof(RegistrationWord));
        OnPropertyChanged(nameof(Composition));
        OnPropertyChanged(nameof(CandidateList));
        OnPropertyChanged(nameof(IsInRegistrationMode));
        OnPropertyChanged(nameof(IsVisible));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
