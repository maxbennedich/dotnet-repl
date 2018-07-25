namespace Scripting
{
    /// <summary>See https://en.wikipedia.org/wiki/ANSI_escape_code</summary>
    public static class AnsiUtils
    {
        public static string Color(this string text, AnsiColor color) => Color(text, color.ColorCode);

        public static string Color(this string text, int colorCode)
        {
            return $"\x1B[38;5;{colorCode}m{text}\x1B[0m";
        }
    }

    /// <summary>See https://en.wikipedia.org/wiki/ANSI_escape_code</summary>
    public class AnsiColor
    {
        public int ColorCode { get; }

        public static readonly AnsiColor Red = new AnsiColor(9);
        public static readonly AnsiColor Green = new AnsiColor(40);
        public static readonly AnsiColor Gray = new AnsiColor(244);

        private AnsiColor(int colorCode)
        {
            ColorCode = colorCode;
        }
    }
}
