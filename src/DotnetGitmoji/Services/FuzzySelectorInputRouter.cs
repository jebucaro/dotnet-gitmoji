namespace DotnetGitmoji.Services;

internal enum FuzzySelectorInputActionKind
{
    None,
    MoveUp,
    MoveDown,
    Submit,
    DeleteCharacter,
    ClearQuery,
    AppendCharacter
}

internal readonly record struct FuzzySelectorInputAction(FuzzySelectorInputActionKind Kind, char Character = '\0');

internal static class FuzzySelectorInputRouter
{
    public static FuzzySelectorInputAction Route(ConsoleKeyInfo keyInfo)
    {
        return keyInfo.Key switch
        {
            ConsoleKey.UpArrow => new FuzzySelectorInputAction(FuzzySelectorInputActionKind.MoveUp),
            ConsoleKey.DownArrow => new FuzzySelectorInputAction(FuzzySelectorInputActionKind.MoveDown),
            ConsoleKey.Enter => new FuzzySelectorInputAction(FuzzySelectorInputActionKind.Submit),
            ConsoleKey.Backspace or ConsoleKey.Delete => new FuzzySelectorInputAction(FuzzySelectorInputActionKind
                .DeleteCharacter),
            ConsoleKey.Escape => new FuzzySelectorInputAction(FuzzySelectorInputActionKind.ClearQuery),
            _ when !char.IsControl(keyInfo.KeyChar) =>
                new FuzzySelectorInputAction(FuzzySelectorInputActionKind.AppendCharacter, keyInfo.KeyChar),
            _ => new FuzzySelectorInputAction(FuzzySelectorInputActionKind.None)
        };
    }
}