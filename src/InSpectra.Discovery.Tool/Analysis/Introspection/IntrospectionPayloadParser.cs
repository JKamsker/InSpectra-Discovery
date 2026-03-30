namespace InSpectra.Discovery.Tool.Analysis.Introspection;

using System.Text.Json.Nodes;
using System.Xml.Linq;

internal static class IntrospectionPayloadParser
{
    public static JsonParseResult TryParse(string expectedFormat, string? text)
        => string.Equals(expectedFormat, "json", StringComparison.OrdinalIgnoreCase)
            ? TryParseJson(text)
            : TryParseXml(text);

    private static JsonParseResult TryParseJson(string? text)
    {
        var normalized = RuntimeSupport.NormalizeConsoleText(text);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return new JsonParseResult(false, null, null, "Output was empty.");
        }

        string? lastError = null;
        foreach (var candidate in GetJsonCandidates(normalized))
        {
            foreach (var parseCandidate in JsonPayloadRepair.ExpandCandidates(candidate))
            {
                try
                {
                    return new JsonParseResult(true, JsonNode.Parse(parseCandidate), parseCandidate, null);
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                }
            }
        }

        return new JsonParseResult(false, null, null, lastError ?? "JSON parsing failed.");
    }

    private static JsonParseResult TryParseXml(string? text)
    {
        var normalized = RuntimeSupport.NormalizeConsoleText(text);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return new JsonParseResult(false, null, null, "Output was empty.");
        }

        var candidates = new List<string> { normalized };
        var firstAngle = normalized.IndexOf('<');
        if (firstAngle > 0)
        {
            var candidate = normalized[firstAngle..].Trim();
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                candidates.Add(candidate);
            }
        }

        string? lastError = null;
        foreach (var candidate in candidates)
        {
            try
            {
                _ = XDocument.Parse(candidate);
                return new JsonParseResult(true, null, candidate, null);
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
            }
        }

        return new JsonParseResult(false, null, null, lastError ?? "XML parsing failed.");
    }

    private static IEnumerable<string> GetJsonCandidates(string normalized)
    {
        var candidates = new List<string> { normalized };
        var firstBrace = normalized.IndexOf('{');
        var firstBracket = normalized.IndexOf('[');
        var startIndex = new[] { firstBrace, firstBracket }
            .Where(index => index >= 0)
            .OrderBy(index => index)
            .FirstOrDefault(-1);
        if (startIndex > 0)
        {
            var candidate = normalized[startIndex..].Trim();
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                candidates.Add(candidate);
            }
        }

        var balancedCandidate = GetBalancedJsonSegment(normalized);
        if (!string.IsNullOrWhiteSpace(balancedCandidate) && !candidates.Contains(balancedCandidate, StringComparer.Ordinal))
        {
            candidates.Add(balancedCandidate);
        }

        return candidates;
    }

    private static string? GetBalancedJsonSegment(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var start = -1;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] is '{' or '[')
            {
                start = index;
                break;
            }
        }

        if (start < 0)
        {
            return null;
        }

        var stack = new Stack<char>();
        var inString = false;
        var escapeNext = false;

        for (var index = start; index < text.Length; index++)
        {
            var ch = text[index];
            if (escapeNext)
            {
                escapeNext = false;
                continue;
            }

            if (inString)
            {
                if (ch == '\\')
                {
                    escapeNext = true;
                }
                else if (ch == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (ch == '"')
            {
                inString = true;
                continue;
            }

            if (ch == '{')
            {
                stack.Push('}');
                continue;
            }

            if (ch == '[')
            {
                stack.Push(']');
                continue;
            }

            if ((ch is '}' or ']') && stack.Count > 0 && stack.Peek() == ch)
            {
                stack.Pop();
                if (stack.Count == 0)
                {
                    return text.Substring(start, (index - start) + 1).Trim();
                }
            }
        }

        return null;
    }
}



