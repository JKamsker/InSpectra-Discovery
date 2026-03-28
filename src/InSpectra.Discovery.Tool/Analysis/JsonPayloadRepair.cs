using System.Text;

internal static class JsonPayloadRepair
{
    public static IEnumerable<string> ExpandCandidates(string candidate)
    {
        yield return candidate;

        var repaired = EscapeControlCharactersInStrings(candidate);
        if (!string.IsNullOrWhiteSpace(repaired) &&
            !string.Equals(repaired, candidate, StringComparison.Ordinal))
        {
            yield return repaired;
        }
    }

    private static string? EscapeControlCharactersInStrings(string text)
    {
        var builder = new StringBuilder(text.Length);
        var changed = false;
        var inString = false;
        var escapeNext = false;

        foreach (var ch in text)
        {
            if (escapeNext)
            {
                builder.Append(ch);
                escapeNext = false;
                continue;
            }

            if (inString)
            {
                if (ch == '\\')
                {
                    builder.Append(ch);
                    escapeNext = true;
                    continue;
                }

                if (ch == '"')
                {
                    builder.Append(ch);
                    inString = false;
                    continue;
                }

                if (TryAppendEscapedControl(builder, ch))
                {
                    changed = true;
                    continue;
                }

                builder.Append(ch);
                continue;
            }

            builder.Append(ch);
            if (ch == '"')
            {
                inString = true;
            }
        }

        return changed ? builder.ToString() : null;
    }

    private static bool TryAppendEscapedControl(StringBuilder builder, char ch)
    {
        switch (ch)
        {
            case '\r':
                builder.Append("\\r");
                return true;
            case '\n':
                builder.Append("\\n");
                return true;
            case '\t':
                builder.Append("\\t");
                return true;
            default:
                if (!char.IsControl(ch))
                {
                    return false;
                }

                builder.Append("\\u");
                builder.Append(((int)ch).ToString("x4"));
                return true;
        }
    }
}
