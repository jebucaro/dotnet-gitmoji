using System.Reflection;
using System.Text;
using DotnetGitmoji.Services;

namespace DotnetGitmoji.Tests;

public class FuzzySelectorInputRouterTests
{
    [Fact]
    public void Route_WhenTypingSpark_AppendsEveryCharacterIncludingK()
    {
        var typed = new StringBuilder();

        foreach (var character in "spark")
        {
            var action = Route(CreateLetterKeyInfo(character));
            Assert.Equal("AppendCharacter", action.Kind);
            typed.Append(action.Character);
        }

        Assert.Equal("spark", typed.ToString());
    }

    [Fact]
    public void Route_WhenArrowKeysPressed_UsesNavigationActions()
    {
        var upAction = Route(new ConsoleKeyInfo('\0', ConsoleKey.UpArrow, false, false, false));
        var downAction = Route(new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false));

        Assert.Equal("MoveUp", upAction.Kind);
        Assert.Equal("MoveDown", downAction.Kind);
    }

    [Fact]
    public void Route_WhenBackspaceAndEscapePressed_UsesEditActions()
    {
        var backspaceAction = Route(new ConsoleKeyInfo('\b', ConsoleKey.Backspace, false, false, false));
        var escapeAction = Route(new ConsoleKeyInfo('\u001b', ConsoleKey.Escape, false, false, false));

        Assert.Equal("DeleteCharacter", backspaceAction.Kind);
        Assert.Equal("ClearQuery", escapeAction.Kind);
    }

    private static (string Kind, char Character) Route(ConsoleKeyInfo keyInfo)
    {
        var routerType = typeof(PromptService).Assembly.GetType("DotnetGitmoji.Services.FuzzySelectorInputRouter")
                         ?? throw new InvalidOperationException("FuzzySelectorInputRouter type not found.");
        var routeMethod = routerType.GetMethod("Route", BindingFlags.Public | BindingFlags.Static)
                          ?? throw new InvalidOperationException("FuzzySelectorInputRouter.Route method not found.");

        var action = routeMethod.Invoke(null, [keyInfo])
                     ?? throw new InvalidOperationException("FuzzySelectorInputRouter.Route returned null.");
        var actionType = action.GetType();

        var kind = actionType.GetProperty("Kind", BindingFlags.Public | BindingFlags.Instance)?.GetValue(action)
                       ?.ToString()
                   ?? throw new InvalidOperationException("FuzzySelectorInputAction.Kind not found.");
        var character = actionType.GetProperty("Character", BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(action);

        return (kind, character is char value ? value : '\0');
    }

    private static ConsoleKeyInfo CreateLetterKeyInfo(char character)
    {
        var keyName = char.ToUpperInvariant(character).ToString();
        if (!Enum.TryParse<ConsoleKey>(keyName, out var key))
            throw new InvalidOperationException($"Unsupported test key for character '{character}'.");

        var shift = char.IsUpper(character);
        return new ConsoleKeyInfo(character, key, shift, false, false);
    }
}