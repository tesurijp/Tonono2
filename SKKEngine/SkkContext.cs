using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using Tonono2.SKKEngine.States;

namespace Tonono2.SKKEngine;

public class CompositionBuffer(Action NotifyChanged)
{
    private readonly StringBuilder buffer = new();
    private CompositionBuffer WrapAction(Action action)
    {
        action();
        NotifyChanged();
        return this;
    }
    public CompositionBuffer Clear() => WrapAction(() => buffer.Clear());
    public CompositionBuffer Append(string st) => WrapAction(() => buffer.Append(st));
    public CompositionBuffer Append(char ch) => WrapAction(() => buffer.Append(ch));
    public CompositionBuffer Remove(int start, int length) => WrapAction(() => buffer.Remove(start, length));
    public override string ToString() => buffer.ToString();
    public int Length => buffer.Length;
    public char First => buffer[0];
}

public class SkkContext : INotifyPropertyChanged
{
    public Func<SkkEngine, SkkKeyCommand, bool> ProcessKey { get; set; } = DisabledState.ProcessKey;
    public SkkContext()
    {
        RomajiBuffer  = new(NotifyBufferChanged);
        CompositionBuffer  = new(NotifyBufferChanged);
        NotifyBufferChanged();
    }

    public CompositionBuffer RomajiBuffer { get; }
    public CompositionBuffer CompositionBuffer { get; }


    private void SetProperty<T>(ref T current, T newvalue, params IEnumerable<string> names)
    {
        if (current is null || (!current.Equals(newvalue)))
        {
            current = newvalue;
            foreach (var name in names)
            {
                OnPropertyChanged(name);
            }
        }
    }

    public SkkState State
    {
        get => field;
        set => SetProperty(ref field, value, nameof(StatusText), nameof(IsVisible));
    } = SkkState.Disabled;


    public bool IsConversionMode
    {
        get => field;
        set => SetProperty(ref field, value, nameof(Composition), nameof(IsVisible));
    }

    public bool IsAbbreviationMode
    {
        get => field;
        set => SetProperty(ref field, value, nameof(Composition));
    }

    public string? OkuriPrefix
    {
        get => field;
        set => SetProperty(ref field, value, nameof(Composition));
    }

    public string ReadingBeforeOkuri
    {
        get => field;
        set => SetProperty(ref field, value, nameof(Composition));
    } = "";

    public List<string> Candidates
    {
        get => field;
        set => SetProperty(ref field, value, nameof(Composition), nameof(CandidateList));
    } = [];

    public int CandidateIndex
    {
        get => field;
        set => SetProperty(ref field, value, nameof(Composition), nameof(CandidateList));
    } = -1;

    public List<string> Completions
    {
        get => field;
        set => SetProperty(ref field, value, nameof(Composition));
    } = [];

    public int CompletionIndex
    {
        get => field;
        set => SetProperty(ref field, value, nameof(Composition));
    } = -1;

    public string OriginalReadingBeforeCompletion { get; set; } = "";

    public int RecursionDepth
    {
        get => field;
        set => SetProperty(ref field, value, nameof(StatusText));
    }

    public bool IsInRegistrationMode
    {
        get => field;
        set => SetProperty(ref field, value, nameof(IsVisible), nameof(IsInRegistrationMode));
    }

    public string RegistrationReading
    {
        get => field;
        set => SetProperty(ref field, value, nameof(RegistrationReading));
    } = "";

    public string RegistrationWord
    {
        get => field;
        set => SetProperty(ref field, value, nameof(RegistrationWord));
    } = "";


    public string StatusText => $"[{GetStateDisplay()}]{(RecursionDepth > 0 ? $":{RecursionDepth}" : "")}";

    public string Composition
    {
        get
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
    }

    public bool ListConversion => CandidateIndex >= 4;
    public static int ListPageSize => 7;

    public string CandidateList
    {
        get
        {
            if (ListConversion)
            {
                var sb = new StringBuilder();
                var pageStart = (CandidateIndex / ListPageSize) * ListPageSize ;
                for (var i = 0; i < ListPageSize; i++)
                {
                    var idx = pageStart + i;
                    if (idx >= Candidates.Count)
                    {
                        break;
                    }
                    var label = (i + 1).ToString(System.Globalization.CultureInfo.CurrentCulture);
                    var mark = (idx == CandidateIndex) ? "*" : " ";
                    sb.Append(label);
                    sb.Append(':');
                    sb.Append(Candidates[idx]);
                    sb.Append(mark);
                    sb.Append(' ');
                }
                return sb.ToString();
            }
            return string.Empty;
        }
    }

    public bool IsVisible => IsInRegistrationMode || CompositionBuffer.Length > 0 || RomajiBuffer.Length > 0;

    private string GetStateDisplay() => State switch
    {
        SkkState.Hiragana => "あ",
        SkkState.Katakana => "ア",
        SkkState.Zenkaku => "全",
        _ => "？"
    };

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

    public void NotifyBufferChanged()
    {
        OnPropertyChanged(nameof(IsVisible));
        OnPropertyChanged(nameof(Composition));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
