namespace InSpectra.Discovery.Tool.Help.Parsing;

internal static class StructuredOptionTableSupport
{
    public static IReadOnlyList<string> TryExtractStructuredOptionLines(IReadOnlyList<string> lines)
    {
        var markdownTableLines = TryExtractMarkdownTableLines(lines);
        if (markdownTableLines.Count > 0)
        {
            return markdownTableLines;
        }

        var boxTableLines = TryExtractBoxTableLines(lines);
        return boxTableLines.Count > 0 ? boxTableLines : [];
    }

    private static IReadOnlyList<string> TryExtractMarkdownTableLines(IReadOnlyList<string> lines)
    {
        var headerIndex = -1;
        StructuredOptionTableParsingSupport.StructuredOptionTableSchema? schema = null;
        for (var index = 0; index < lines.Count; index++)
        {
            if (!StructuredOptionTableParsingSupport.TryGetMarkdownOptionTableSchema(lines[index], out var candidateSchema))
            {
                continue;
            }

            headerIndex = index;
            schema = candidateSchema;
            break;
        }

        if (headerIndex < 0)
        {
            return [];
        }

        var results = new List<string>();
        var hasRows = false;
        foreach (var rawLine in lines.Skip(headerIndex + 1))
        {
            if (StructuredOptionTableParsingSupport.IsMarkdownTableSeparator(rawLine))
            {
                continue;
            }

            if (!StructuredOptionTableParsingSupport.TrySplitMarkdownTableRow(rawLine, out var cells) || schema is null)
            {
                if (hasRows)
                {
                    break;
                }

                continue;
            }

            var rowKind = StructuredOptionTableParsingSupport.TryBuildStructuredRow(cells, schema.Value, out var syntheticLine);
            if (rowKind == StructuredOptionTableParsingSupport.StructuredRowKind.Entry)
            {
                results.Add(syntheticLine);
                hasRows = true;
                continue;
            }

            if (rowKind == StructuredOptionTableParsingSupport.StructuredRowKind.Continuation && hasRows)
            {
                results.Add(syntheticLine);
                continue;
            }

            if (hasRows)
            {
                break;
            }
        }

        return hasRows ? results : [];
    }

    private static IReadOnlyList<string> TryExtractBoxTableLines(IReadOnlyList<string> lines)
    {
        var headerIndex = -1;
        StructuredOptionTableParsingSupport.StructuredOptionTableSchema? schema = null;
        for (var index = 0; index < lines.Count; index++)
        {
            if (!StructuredOptionTableParsingSupport.TryGetBoxOptionTableSchema(lines[index], out var candidateSchema))
            {
                continue;
            }

            headerIndex = index;
            schema = candidateSchema;
            break;
        }

        if (headerIndex < 0)
        {
            return [];
        }

        var results = new List<string>();
        var hasRows = false;
        var currentRowCaptured = false;
        foreach (var rawLine in lines.Skip(headerIndex + 1))
        {
            if (StructuredOptionTableParsingSupport.IsBoxTableBorder(rawLine))
            {
                continue;
            }

            if (!StructuredOptionTableParsingSupport.TrySplitBoxTableRow(rawLine, out var cells) || schema is null)
            {
                if (hasRows)
                {
                    break;
                }

                continue;
            }

            var rowKind = StructuredOptionTableParsingSupport.TryBuildStructuredRow(cells, schema.Value, out var syntheticLine);
            if (rowKind == StructuredOptionTableParsingSupport.StructuredRowKind.Entry)
            {
                results.Add(syntheticLine);
                hasRows = true;
                currentRowCaptured = true;
                continue;
            }

            if (rowKind == StructuredOptionTableParsingSupport.StructuredRowKind.Continuation && currentRowCaptured)
            {
                results.Add(syntheticLine);
                continue;
            }

            if (hasRows)
            {
                break;
            }

            currentRowCaptured = false;
        }

        return hasRows ? results : [];
    }
}

