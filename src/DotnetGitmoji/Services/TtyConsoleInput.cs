using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DotnetGitmoji.Services;

/// <summary>
/// Replaces the process stdin handle with the platform's TTY device so that
/// interactive prompts work even when stdin has been redirected (e.g. during git hooks).
/// This mirrors gitmoji-cli's <c>exec &lt; /dev/tty</c> at the .NET level.
/// </summary>
internal static partial class TtyConsoleInput
{
    /// <summary>
    /// Replaces the native stdin handle with the terminal device.
    /// Returns true if the handle was successfully replaced.
    /// </summary>
    public static bool TryReopenStdin()
    {
        try
        {
            return OperatingSystem.IsWindows()
                ? TryReopenStdinWindows()
                : TryReopenStdinUnix();
        }
        catch
        {
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool TryReopenStdinWindows()
    {
        const uint GENERIC_READ = 0x80000000;
        const uint GENERIC_WRITE = 0x40000000;
        const uint FILE_SHARE_READ_WRITE = 0x03;
        const uint OPEN_EXISTING = 3;
        const int STD_INPUT_HANDLE = -10;

        IntPtr handle = CreateFileW(
            "CONIN$",
            GENERIC_READ | GENERIC_WRITE,
            FILE_SHARE_READ_WRITE,
            IntPtr.Zero,
            OPEN_EXISTING,
            0,
            IntPtr.Zero);

        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
        {
            return false;
        }

        if (!SetStdHandle(STD_INPUT_HANDLE, handle))
        {
            return false;
        }

        if (!GetConsoleMode(handle, out _))
        {
            return false;
        }

        return true;
    }

    private static bool TryReopenStdinUnix()
    {
        const int O_RDONLY = 0;

        int fd = Open("/dev/tty", O_RDONLY);
        if (fd < 0)
        {
            return false;
        }

        if (Dup2(fd, 0) < 0)
        {
            return false;
        }

        return true;
    }

    [SupportedOSPlatform("windows")]
    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [SupportedOSPlatform("windows")]
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetStdHandle(int nStdHandle, IntPtr hHandle);

    [SupportedOSPlatform("windows")]
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [LibraryImport("libc", EntryPoint = "open", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Open(string path, int flags);

    [LibraryImport("libc", EntryPoint = "dup2")]
    private static partial int Dup2(int oldfd, int newfd);
}