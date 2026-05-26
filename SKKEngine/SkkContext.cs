using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using Tonono2.SKKEngine.States;

namespace Tonono2.SKKEngine;

public class SkkContext : INotifyPropertyChanged
{
    public Func<SkkEngine, SkkKeyCommand, bool> ProcessKey { get; set; } = DisabledState.ProcessKey;
    public StringBuilder RomajiBuffer { get; } = new();
    public StringBuilder CompositionBuffer { get; } = new();
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
                var bufferStr = CompositionBuffer.ToString();
                var start = Math.Min(ReadingBeforeOkuri.Length, bufferStr.Length);
                var okuriDisplay = string.Concat(bufferStr.AsSpan(start), RomajiBuffer.ToString());
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
    public bool IsVisible => IsInRegistrationMode || CompositionBuffer.Length > 0 || RomajiBuffer.Length > 0;

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
        OnPropertyChanged2(nameof(StatusText));
        OnPropertyChanged2(nameof(RegistrationReading));
        OnPropertyChanged2(nameof(RegistrationWord));
        OnPropertyChanged2(nameof(Composition));
        OnPropertyChanged2(nameof(CandidateList));
        OnPropertyChanged2(nameof(IsInRegistrationMode));
        OnPropertyChanged2(nameof(IsVisible));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged2(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    protected virtual void OnPropertyChanged(string propertyName) { }
}
