using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace Tonono2.SKKEngine;

public class SkkContext : INotifyPropertyChanged
{
    public SkkState State
    {
        get => field;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(IsVisible));
            }
        }
    } = SkkState.Disabled;

    public StringBuilder RomajiBuffer { get; } = new();
    public StringBuilder CompositionBuffer { get; } = new();

    public bool IsConversionMode
    {
        get => field;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Composition));
                OnPropertyChanged(nameof(IsVisible));
            }
        }
    }

    public bool IsAbbreviationMode
    {
        get => field;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Composition));
            }
        }
    }

    public string? OkuriPrefix
    {
        get => field;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Composition));
            }
        }
    }

    public string ReadingBeforeOkuri
    {
        get => field;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Composition));
            }
        }
    } = "";

    public List<string> Candidates
    {
        get => field;
        set
        {
            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Composition));
            OnPropertyChanged(nameof(CandidateList));
        }
    } = [];

    public int CandidateIndex
    {
        get => field;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Composition));
                OnPropertyChanged(nameof(CandidateList));
            }
        }
    } = -1;

    public List<string> Completions
    {
        get => field;
        set
        {
            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Composition));
        }
    } = [];

    public int CompletionIndex
    {
        get => field;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Composition));
            }
        }
    } = -1;

    public string OriginalReadingBeforeCompletion
    {
        get => field;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
            }
        }
    } = "";

    public int RecursionDepth
    {
        get => field;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public bool IsInRegistrationMode
    {
        get => field;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsVisible));
                OnPropertyChanged(nameof(IsInRegistrationMode));
            }
        }
    }

    public string RegistrationReading
    {
        get => field;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
            }
        }
    } = "";

    public string RegistrationWord
    {
        get => field;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
            }
        }
    } = "";


    public string StatusText => $"[{GetStateDisplay()}]{(RecursionDepth > 0 ? $":{RecursionDepth}" : "")}";

    public string Composition
    {
        get
        {
            if (CandidateIndex >= 0 && CandidateIndex < Candidates.Count)
            {
                var result = (CandidateIndex >= 4) ? "▼" : "▼" + Candidates[CandidateIndex];
                if (OkuriPrefix != null)
                {
                    var bufferStr = CompositionBuffer.ToString();
                    var start = Math.Min(ReadingBeforeOkuri.Length, bufferStr.Length);
                    var okuriDisplay = string.Concat(bufferStr.AsSpan(start), RomajiBuffer.ToString());
                    result += "[" + okuriDisplay + "]";
                }
                return result;
            }

            if (CompletionIndex >= 0 && CompletionIndex < Completions.Count)
            {
                var prefix = IsAbbreviationMode ? " /" : "▽";
                return prefix + Completions[CompletionIndex] + RomajiBuffer.ToString();
            }

            var prefixStr = IsAbbreviationMode ? " /" : (IsConversionMode ? "▽" : "");
            return prefixStr + CompositionBuffer.ToString() + RomajiBuffer.ToString();
        }
    }

    public string CandidateList
    {
        get
        {
            if (CandidateIndex >= 4)
            {
                var sb = new StringBuilder();
                var pageStart = (CandidateIndex / 7) * 7;
                for (var i = 0; i < 7; i++)
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

    public void NotifyBufferChanged()
    {
        OnPropertyChanged(nameof(Composition));
        OnPropertyChanged(nameof(IsVisible));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
