using System.Text;

namespace PinguApps.RazorStyle.Core.Running;

/// <summary>
/// Orchestrates scanning, diagnostics, and optional fixes for Razor files.
/// </summary>
public sealed class RazorStyleRunner
{
    private readonly IReadOnlyList<IRazorStyleRule> _rules =
    [
        new AttributeOrderRule(),
        new AttributeWrappingRule(),
        new ChildContentLineRule(),
    ];

    /// <summary>
    /// Analyzes Razor source text.
    /// </summary>
    public RazorStyleFileResult CheckText(string text, string filePath)
    {
        return CheckText(text, filePath, RazorStyleOptions.Default);
    }

    /// <summary>
    /// Analyzes Razor source text.
    /// </summary>
    public RazorStyleFileResult CheckText(string text, string filePath, RazorStyleOptions options)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(options);

        RazorStyleDocument document = new(text, filePath);
        List<RazorDiagnostic> diagnostics = [];
        string newLine = DetectNewLine(text);

        foreach (IRazorStyleRule rule in _rules.Where(rule => options.IsRuleEnabled(rule.DiagnosticId)))
        {
            diagnostics.AddRange(rule.Evaluate(document, applyFixes: false, newLine).Diagnostics);
        }

        return new RazorStyleFileResult(filePath, diagnostics, null);
    }

    /// <summary>
    /// Analyzes and fixes Razor source text.
    /// </summary>
    public RazorStyleFileResult FixText(string text, string filePath)
    {
        return FixText(text, filePath, RazorStyleOptions.Default);
    }

    /// <summary>
    /// Analyzes and fixes Razor source text.
    /// </summary>
    public RazorStyleFileResult FixText(string text, string filePath, RazorStyleOptions options)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(options);

        List<RazorDiagnostic> diagnostics = [];
        string newLine = DetectNewLine(text);
        string currentText = text;

        foreach (IRazorStyleRule rule in _rules.Where(rule => options.IsRuleEnabled(rule.DiagnosticId)))
        {
            RazorStyleDocument document = new(currentText, filePath);
            RazorStyleRuleResult result = rule.Evaluate(document, applyFixes: true, newLine);
            diagnostics.AddRange(result.Diagnostics);

            if (result.Replacements.Count == 0)
            {
                continue;
            }

            currentText = ApplyReplacements(currentText, result.Replacements);
        }

        string? rewrittenText = string.Equals(text, currentText, StringComparison.Ordinal) ? null : currentText;
        return new RazorStyleFileResult(filePath, diagnostics, rewrittenText);
    }

    /// <summary>
    /// Finds Razor files under the provided file or directory path.
    /// </summary>
    public static IReadOnlyList<string> FindRazorFiles(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return File.Exists(path)
            ? string.Equals(Path.GetExtension(path), ".razor", StringComparison.OrdinalIgnoreCase)
                ? [Path.GetFullPath(path)]
                : []
            : Directory.Exists(path)
            ? Directory.EnumerateFiles(path, "*.razor", SearchOption.AllDirectories)
            .Where(file => !IsGeneratedPath(file))
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(Path.GetFullPath)
            .ToArray()
            : throw new DirectoryNotFoundException("The path does not exist: " + path);
    }

    /// <summary>
    /// Reads a text file while preserving a detectable encoding for later writes.
    /// </summary>
    public static string ReadAllTextPreservingEncoding(string path, out Encoding encoding)
    {
        byte[] bytes = File.ReadAllBytes(path);
        encoding = DetectEncoding(bytes);
        return encoding.GetString(RemovePreamble(bytes, encoding));
    }

    /// <summary>
    /// Writes a text file with the provided encoding.
    /// </summary>
    public static void WriteAllText(string path, string text, Encoding encoding)
    {
        File.WriteAllText(path, text, encoding);
    }

    private static string ApplyReplacements(string text, IReadOnlyList<RazorStyleReplacement> replacements)
    {
        StringBuilder builder = new(text);

        foreach (RazorStyleReplacement replacement in replacements.OrderByDescending(replacement => replacement.StartIndex))
        {
            builder.Remove(replacement.StartIndex, replacement.EndIndex - replacement.StartIndex + 1);
            builder.Insert(replacement.StartIndex, replacement.NewText);
        }

        return builder.ToString();
    }

    private static string DetectNewLine(string text)
    {
        int newlineIndex = text.IndexOf('\n', StringComparison.Ordinal);
        return newlineIndex > 0 && text[newlineIndex - 1] == '\r'
            ? "\r\n"
            : newlineIndex >= 0 ? "\n" : Environment.NewLine;
    }

    private static bool IsGeneratedPath(string path)
    {
        string normalized = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        return normalized.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static Encoding DetectEncoding(byte[] bytes)
    {
        return bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF
            ? new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)
            : bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE
            ? Encoding.Unicode
            : bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF
            ? Encoding.BigEndianUnicode
            : new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    }

    private static byte[] RemovePreamble(byte[] bytes, Encoding encoding)
    {
        byte[] preamble = encoding.GetPreamble();
        if (preamble.Length == 0 || bytes.Length < preamble.Length)
        {
            return bytes;
        }

        for (int index = 0; index < preamble.Length; index++)
        {
            if (bytes[index] != preamble[index])
            {
                return bytes;
            }
        }

        return bytes[preamble.Length..];
    }
}

