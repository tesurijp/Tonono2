using System.Collections.Generic;
using System.Text;

namespace Tonono2.SKKEngine;

public record class RegistrationContext(string Reading, SkkState PreviousState)
{
    public string WordBuffer { get; set; } = "";
}

public class DictionaryRegistrar(SkkDicManager dictionary)
{
    private readonly Stack<RegistrationContext> regStack = new();

    public int RecursionDepth => regStack.Count;
    public bool IsInRegistrationMode => regStack.Count > 0;
    public string RegistrationReading => IsInRegistrationMode ? regStack.Peek().Reading : "";
    public string RegistrationWord => IsInRegistrationMode ? regStack.Peek().WordBuffer : "";

    public void Start(string reading, SkkState currentState) => regStack.Push(new(reading, currentState));

    public SkkState? Cancel()
    {
        if (regStack.Count > 0)
        {
            var ctx = regStack.Pop();
            return ctx.PreviousState;
        }
        return null;
    }

    public (SkkState prevState, string reading, string word)? Finish()
    {
        if (regStack.Count > 0)
        {
            var ctx = regStack.Pop();
            var word = ctx.WordBuffer;
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
            regStack.Peek().WordBuffer += text;
        }
    }

    public void RemoveLastBuffer()
    {
        if (IsInRegistrationMode)
        {
            var ctx = regStack.Peek();
            if (ctx.WordBuffer.Length > 0)
            {
                ctx.WordBuffer = ctx.WordBuffer[..^1];
            }
        }
    }
}
