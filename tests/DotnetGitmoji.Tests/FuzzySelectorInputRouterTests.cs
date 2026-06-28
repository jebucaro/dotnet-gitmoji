using System.Reflection;
using System.Text;
using DotnetGitmoji.Services;

namespace DotnetGitmoji.Tests;

public class FuzzySelectorInputRouterTests
{
    [Fact]
    public void Route_WhenTypingSpark_AppendsEveryCharacterIncludingK()
    {
        StringBuilder typed = new();

        foreach (char character in "spark")
        {
            (string Kind, char Character) action = Route(CreateLetterKeyInfo(character));
            Assert.Equal("AppendCharacter", action.Kind);
            typed.Append(action.Character);
        }

        Assert.Equal("spark", typed.ToString());
    }

    [Fact]
    public void Route_WhenArrowKeysPressed_UsesNavigationActions()
    {
        (string Kind, char Character) upAction =
            Route(new ConsoleKeyInfo('\0', ConsoleKey.UpArrow, false, false, false));
        (string Kind, char Character) downAction =
            Route(new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false));

        Assert.Equal("MoveUp", upAction.Kind);
        Assert.Equal("MoveDown", downAction.Kind);
    }

    [Fact]
    public void Route_WhenBackspaceAndEscapePressed_UsesEditActions()
    {
        (string Kind, char Character) backspaceAction =
            Route(new ConsoleKeyInfo('\b', ConsoleKey.Backspace, false, false, false));
        (string Kind, char Character) escapeAction =
            Route(new ConsoleKeyInfo('', ConsoleKey.Escape, false, false, false));

        Assert.Equal("DeleteCharacter", backspaceAction.Kind);
        Assert.Equal("ClearQuery", escapeAction.Kind);
    }

    [Fact]
    public void Route_WhenControlCharacterPressed_ReturnsNoneAction()
    {
        (string Kind, char Character) f1Action = Route(new ConsoleKeyInfo('\0', ConsoleKey.F1, false, false, false));

        Assert.Equal("None", f1Action.Kind);
    }

    private static (string Kind, char Character) Route(ConsoleKeyInfo keyInfo)
    {
        Type routerType = typeof(PromptService).Assembly.GetType("DotnetGitmoji.Services.FuzzySelectorInputRouter")
                          ?? throw new InvalidOperationException("FuzzySelectorInputRouter type not found.");
        MethodInfo routeMethod = routerType.GetMethod("Route", BindingFlags.Public | BindingFlags.Static)
                                 ?? throw new InvalidOperationException(
                                     "FuzzySelectorInputRouter.Route method not found.");

        object action = routeMethod.Invoke(null, [keyInfo])
                        ?? throw new InvalidOperationException("FuzzySelectorInputRouter.Route returned null.");
        Type actionType = action.GetType();

        string kind = actionType.GetProperty("Kind", BindingFlags.Public | BindingFlags.Instance)?.GetValue(action)
                          ?.ToString()
                      ?? throw new InvalidOperationException("FuzzySelectorInputAction.Kind not found.");
        object? character = actionType.GetProperty("Character", BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(action);

        return (kind, character is char value ? value : '\0');
    }

    private static ConsoleKeyInfo CreateLetterKeyInfo(char character)
    {
        string keyName = char.ToUpperInvariant(character).ToString();
        if (!Enum.TryParse<ConsoleKey>(keyName, out ConsoleKey key))
        {
            throw new InvalidOperationException($"Unsupported test key for character '{character}'.");
        }

        bool shift = char.IsUpper(character);
        return new ConsoleKeyInfo(character, key, shift, false, false);
    }
}