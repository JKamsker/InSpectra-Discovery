namespace InSpectra.Discovery.Tool.Help;

internal static class InlineOptionExampleSupport
{
    private static readonly HashSet<string> InlineReferenceWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "is",
        "was",
        "are",
        "used",
        "specified",
        "set",
        "to",
        "for",
        "if",
        "when",
    };

    public static bool ContainsInlineOptionExample(OptionSignature signature, string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return false;
        }

        foreach (var optionToken in OptionSignatureSupport.EnumerateTokens(signature))
        {
            var searchIndex = 0;
            while (searchIndex < description.Length)
            {
                var matchIndex = description.IndexOf(optionToken, searchIndex, StringComparison.OrdinalIgnoreCase);
                if (matchIndex < 0)
                {
                    break;
                }

                if (!HasInlineOptionExampleBoundary(description, matchIndex, optionToken.Length))
                {
                    searchIndex = matchIndex + optionToken.Length;
                    continue;
                }

                var valueStart = matchIndex + optionToken.Length;
                while (valueStart < description.Length && char.IsWhiteSpace(description, valueStart))
                {
                    valueStart++;
                }

                if (valueStart < description.Length)
                {
                    var next = description[valueStart];
                    if (!char.IsWhiteSpace(next)
                        && next is not '-' and not '/' and not '.' and not ',' and not ';' and not ')')
                    {
                        if (!LooksLikeInlineReferenceWord(ReadInlineReferenceWord(description, valueStart)))
                        {
                            return true;
                        }
                    }
                }

                searchIndex = matchIndex + optionToken.Length;
            }
        }

        return false;
    }

    private static bool HasInlineOptionExampleBoundary(string description, int matchIndex, int tokenLength)
    {
        if (matchIndex > 0 && char.IsLetterOrDigit(description[matchIndex - 1]))
        {
            return false;
        }

        var endIndex = matchIndex + tokenLength;
        return endIndex >= description.Length || !char.IsLetterOrDigit(description[endIndex]);
    }

    private static string ReadInlineReferenceWord(string description, int startIndex)
    {
        var endIndex = startIndex;
        while (endIndex < description.Length)
        {
            var character = description[endIndex];
            if (char.IsWhiteSpace(character) || character is ',' or ';' or ')' or '(' or '[' or ']')
            {
                break;
            }

            endIndex++;
        }

        return description[startIndex..endIndex];
    }

    private static bool LooksLikeInlineReferenceWord(string word)
        => InlineReferenceWords.Contains(word);
}

