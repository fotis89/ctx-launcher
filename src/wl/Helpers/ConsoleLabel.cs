namespace wl.Helpers;

internal static class ConsoleLabel
{
    private const int LabelWidth = 13;
    private const string Indent = "  ";

    public static void WriteLine(string label, string value)
        => Console.WriteLine($"{Indent}{label.PadRight(LabelWidth)} {value}");

    public static void WriteContinuation(string value)
        => Console.WriteLine($"{Indent}{new string(' ', LabelWidth)} {value}");
}
