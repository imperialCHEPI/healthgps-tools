using System;
using System.IO;

namespace HealthGPS.Tools;

public static class TextWriterExtensions
{
    const string DefaultForegroundColor = "\x1B[39m\x1B[22m";
    const string DefaultBackgroundColor = "\x1B[49m";

    public static void WriteWithColor(
        this TextWriter textWriter,
        string message,
        ConsoleColor? backgroundColor,
        ConsoleColor? foregroundColor)
    {
        // Order:
        //   1. background colour
        //   2. foreground colour
        //   3. message
        //   4. reset foreground colour
        //   5. reset background colour
        SetTextColor(textWriter, backgroundColor, foregroundColor);
        textWriter.Write(message);
        ResetTextColor(textWriter, backgroundColor, foregroundColor);
    }

    public static void WriteLineWithColor(
        this TextWriter textWriter,
        string message,
        ConsoleColor? backgroundColor,
        ConsoleColor? foregroundColor)
    {
        // Order:
        //   1. background colour
        //   2. foreground colour
        //   3. message
        //   4. reset foreground colour
        //   5. reset background colour
        SetTextColor(textWriter, backgroundColor, foregroundColor);
        textWriter.WriteLine(message);
        ResetTextColor(textWriter, backgroundColor, foregroundColor);
    }

    private static void SetTextColor(TextWriter textWriter, ConsoleColor? backgroundColor, ConsoleColor? foregroundColor)
    {
        if (backgroundColor.HasValue)
        {
            textWriter.Write(GetBackgroundColorEscapeCode(backgroundColor.Value));
        }

        if (foregroundColor.HasValue)
        {
            textWriter.Write(GetForegroundColorEscapeCode(foregroundColor.Value));
        }
    }

    private static void ResetTextColor(TextWriter textWriter, ConsoleColor? backgroundColor, ConsoleColor? foregroundColor)
    {
        if (backgroundColor.HasValue)
        {
            textWriter.Write(DefaultBackgroundColor);
        }

        if (foregroundColor.HasValue)
        {
            textWriter.Write(DefaultForegroundColor);
        }
    }

    private static string GetForegroundColorEscapeCode(ConsoleColor color) =>
        color switch
        {
            ConsoleColor.Black => "\x1B[30m",
            ConsoleColor.DarkRed => "\x1B[31m",
            ConsoleColor.DarkGreen => "\x1B[32m",
            ConsoleColor.DarkYellow => "\x1B[33m",
            ConsoleColor.DarkBlue => "\x1B[34m",
            ConsoleColor.DarkMagenta => "\x1B[35m",
            ConsoleColor.DarkCyan => "\x1B[36m",
            ConsoleColor.Gray => "\x1B[37m",
            ConsoleColor.Red => "\x1B[1m\x1B[31m",
            ConsoleColor.Green => "\x1B[1m\x1B[32m",
            ConsoleColor.Yellow => "\x1B[1m\x1B[33m",
            ConsoleColor.Blue => "\x1B[1m\x1B[34m",
            ConsoleColor.Magenta => "\x1B[1m\x1B[35m",
            ConsoleColor.Cyan => "\x1B[1m\x1B[36m",
            ConsoleColor.White => "\x1B[1m\x1B[37m",

            _ => DefaultForegroundColor
        };

    private static string GetBackgroundColorEscapeCode(ConsoleColor color) =>
        color switch
        {
            ConsoleColor.Black => "\x1B[40m",
            ConsoleColor.DarkRed => "\x1B[41m",
            ConsoleColor.DarkGreen => "\x1B[42m",
            ConsoleColor.DarkYellow => "\x1B[43m",
            ConsoleColor.DarkBlue => "\x1B[44m",
            ConsoleColor.DarkMagenta => "\x1B[45m",
            ConsoleColor.DarkCyan => "\x1B[46m",
            ConsoleColor.Gray => "\x1B[47m",

            _ => DefaultBackgroundColor
        };
}
