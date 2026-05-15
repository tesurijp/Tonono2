using System.Collections.Generic;
using System.Text;

namespace Tonono2.SKKEngine;

public record class RegistrationContext(string Reading, SkkState PreviousState)
{
    public StringBuilder WordBuffer { get; } = new();
}

public class DictionaryRegistrar(SkkDicManager dictionary)
{
    private readonly Stack<RegistrationContext> _regStack = new();

    public int RecursionDepth => _regStack.Count;
    public bool IsInRegistrationMode => _regStack.Count > 0;
    
    public string RegistrationReading => IsInRegistrationMode ? _regStack.Peek().Reading : "";
    public string RegistrationWord => IsInRegistrationMode ? _regStack.Peek().WordBuffer.ToString() : "";

    public void Start(string reading, SkkState currentState) => _regStack.Push(new(reading, currentState));

    public SkkState? Cancel()
    {
        if (_regStack.Count > 0)
        {
            var ctx = _regStack.Pop();
            return ctx.PreviousState;
        }
        return null;
    }

    public (SkkState prevState, string reading, string word)? Finish()
    {
        if (_regStack.Count > 0)
        {
            var ctx = _regStack.Pop();
            var word = ctx.WordBuffer.ToString();
            if (!string.IsNullOrWhiteSpace(word))
            {
                dictionary.AddWord(ctx.Reading, word);
                return (ctx.PreviousState, ctx.Reading, word);
            }
            return (ctx.PreviousState, "", "");
        }
        return null;
    }

    public void AppendBuffer(string text)
    {
        if (IsInRegistrationMode)
        {
            _regStack.Peek().WordBuffer.Append(text);
        }
    }

    public void RemoveLastBuffer()
    {
        if (IsInRegistrationMode)
        {
            var ctx = _regStack.Peek();
            if (ctx.WordBuffer.Length > 0)
            {
                ctx.WordBuffer.Remove(ctx.WordBuffer.Length - 1, 1);
            }
        }
    }
}
