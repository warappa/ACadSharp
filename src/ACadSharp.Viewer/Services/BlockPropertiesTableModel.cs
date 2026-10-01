using ACadSharp;
using ACadSharp.Objects.Evaluations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ACadSharp.Viewer.Services;

/// <summary>
/// One record of a <see cref="BlockPropertiesTable"/> for the "Block Properties Tables"
/// view: the human-readable (label, value) pair with the raw string-pool indices.
/// For the 1kV schema a record holds a single 7-bit string-pool index (shown in the
/// value-index column, with its resolved string in the value column and "—" for the
/// label); for the L3-02 schema a record holds a direct label index + an
/// offset-based value index (= 10 + count).
/// </summary>
public sealed class BptRecordItem
{
    /// <summary>
    /// 1-based display row number.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// The resolved label string (L3-02 schema only; "—" otherwise).
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// The label index (L3-02 schema only; "—" otherwise), as a string.
    /// </summary>
    public string? LabelIndex { get; }

    /// <summary>
    /// The resolved value string (both schemas: the record's 7-bit index for the
    /// 1kV schema, the offset-based index for the L3-02 schema).
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// The value index (both schemas), as a string.
    /// </summary>
    public string? ValueIndex { get; }

    public BptRecordItem(int index, string label, int? labelIndex, string value, int? valueIndex)
    {
        Index = index;
        Label = label;
        LabelIndex = labelIndex?.ToString();
        Value = value;
        ValueIndex = valueIndex?.ToString();
    }
}

/// <summary>
/// One entry of a <see cref="BlockPropertiesTable"/>'s decoded string pool.
/// </summary>
public sealed class BptStringItem
{
    /// <summary>
    /// The pool index (0-based).
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// The interned string; "(empty)" for the empty-string entry so the cell does
    /// not look like a missing value.
    /// </summary>
    public string Value { get; }

    public BptStringItem(int index, string value)
    {
        Index = index;
        Value = string.IsNullOrEmpty(value) ? "(empty)" : value;
    }
}

/// <summary>
/// One <see cref="BlockPropertiesTable"/> object for the "Block Properties Tables"
/// view: the header fields (be_major / be_minor / eed1071), the decoded string pool,
/// and the schema-dependent records, all resolved and formatted for display.
/// </summary>
public sealed class BptTableItem
{
    public string Title { get; }
    public string Subtitle { get; }

    // Header fields (display strings; the raw ints would need a converter for the
    // "— / n/a" cases the decoder's -1 / null results produce).
    public string BeMajorText { get; }
    public string BeMinorText { get; }
    public string Eed1071Text { get; }
    public string RawBitsText { get; }
    public string PoolStartText { get; }
    public int StringCount { get; }
    public int RecordCount { get; }

    /// <summary>
    /// The matched record schema, as a human-readable name.
    /// </summary>
    public string SchemaText { get; }

    /// <summary>
    /// The note shown above the records grid (what the schema is, and what the
    /// columns mean for it).
    /// </summary>
    public string RecordsNote { get; }

    public List<BptRecordItem> Records { get; }
    public List<BptStringItem> Strings { get; }

    private BptTableItem(
        string title, string subtitle,
        string beMajor, string beMinor, string eed1071,
        string rawBits, string poolStart,
        int stringCount, int recordCount,
        string schemaText, string recordsNote,
        List<BptRecordItem> records, List<BptStringItem> strings)
    {
        Title = title;
        Subtitle = subtitle;
        BeMajorText = beMajor;
        BeMinorText = beMinor;
        Eed1071Text = eed1071;
        RawBitsText = rawBits;
        PoolStartText = poolStart;
        StringCount = stringCount;
        RecordCount = recordCount;
        SchemaText = schemaText;
        RecordsNote = recordsNote;
        Records = records;
        Strings = strings;
    }

    public static BptTableItem Create(BlockPropertiesTable table, int ordinal)
    {
        string[] pool = table.Strings;
        int[] recordIndices = table.RecordIndices;
        int[] labelIndices = table.LabelIndices;
        int[] valueIndices = table.ValueIndices;

        List<BptRecordItem> records = new();
        string schemaText = table.RecordSchema switch
        {
            1 => "1kV 126-bit record",
            2 => "L3-02 96-bit entry",
            _ => "no schema matched",
        };

        if (table.RecordSchema == 1)
        {
            // A 1kV record holds a single 7-bit string-pool index.
            for (int i = 0; i < recordIndices.Length; i++)
            {
                records.Add(new BptRecordItem(i + 1, "—", null, Resolve(pool, recordIndices[i]), recordIndices[i]));
            }

            string note = recordIndices.Length > 0
                ? $"{schemaText}: {recordIndices.Length} record(s); each record holds one 7-bit string-pool index (the value-index column shows it, the value column shows the resolved string)."
                : $"{schemaText}: matched, but no records decoded.";
            return new BptTableItem(
                $"Table {ordinal + 1}",
                SubtitleOf(table),
                $"{table.BeMajor}", $"{table.BeMinor}", $"{table.Eed1071}",
                BitsText(table), PoolStartOf(table),
                pool.Length, records.Count, schemaText, note, records,
                PoolItems(pool));
        }

        if (table.RecordSchema == 2)
        {
            // An L3-02 96-bit entry holds a direct label index + an offset-based
            // value index (= 10 + count).
            int count = Math.Max(labelIndices.Length, valueIndices.Length);
            for (int i = 0; i < count; i++)
            {
                int? label = i < labelIndices.Length ? labelIndices[i] : null;
                int? value = i < valueIndices.Length ? valueIndices[i] : null;
                records.Add(new BptRecordItem(
                    i + 1,
                    label is null ? "—" : Resolve(pool, label.Value),
                    label,
                    value is null ? "—" : Resolve(pool, value.Value),
                    value));
            }

            string note = records.Count > 0
                ? $"{schemaText}: {records.Count} label/value pair(s); the value index is 10 + count (the group base 10 is implicit in the block's value type)."
                : $"{schemaText}: matched, but no records decoded.";
            return new BptTableItem(
                $"Table {ordinal + 1}",
                SubtitleOf(table),
                $"{table.BeMajor}", $"{table.BeMinor}", $"{table.Eed1071}",
                BitsText(table), PoolStartOf(table),
                pool.Length, records.Count, schemaText, note, records,
                PoolItems(pool));
        }

        // No schema matched: the records grid stays empty and the note explains
        // whether the (schema-independent) string pool still decoded.
        string noSchemaNote = pool.Length > 0
            ? $"No record schema matched this table — the string pool below still decoded ({pool.Length} interned string(s))."
            : "No record schema matched, and the body did not decode (no string pool).";
        return new BptTableItem(
            $"Table {ordinal + 1}",
            SubtitleOf(table),
            $"{table.BeMajor}", $"{table.BeMinor}", $"{table.Eed1071}",
            BitsText(table), PoolStartOf(table),
            pool.Length, 0, schemaText, noSchemaNote, records,
            PoolItems(pool));
    }

    private static string SubtitleOf(BlockPropertiesTable table)
    {
        bool hasTail = table.RawTail is { Length: > 0 };
        return hasTail
            ? $"{table.BeMajor}.{table.BeMinor} · {table.RawTailBitCount} bits · {table.Strings.Length} string(s)"
            : $"{table.BeMajor}.{table.BeMinor} · no raw tail";
    }

    private static string BitsText(BlockPropertiesTable table)
    {
        return table.RawTail is { Length: > 0 } ? $"{table.RawTailBitCount}" : "—";
    }

    private static string PoolStartOf(BlockPropertiesTable table)
    {
        int start = table.StringPoolStart;
        return start >= 0 ? $"{start}" : "—";
    }

    /// <summary>
    /// Resolves a string-pool index to its interned string, flagging an
    /// out-of-range index (it still shows, marked, so a decode anomaly is visible
    /// rather than silently dropping a record).
    /// </summary>
    private static string Resolve(string[] pool, int index)
    {
        return index >= 0 && index < pool.Length ? pool[index] : $"<{index}>";
    }

    private static List<BptStringItem> PoolItems(string[] pool)
    {
        return pool.Select((s, i) => new BptStringItem(i, s)).ToList();
    }
}

/// <summary>
/// Builds the display models for a loaded document's block properties tables.
/// </summary>
public static class BlockPropertiesTableModel
{
    public static List<BptTableItem> Build(CadDocument document)
    {
        return Collect(document)
            .Select((t, i) => BptTableItem.Create(t, i))
            .ToList();
    }

    /// <summary>
    /// All <see cref="BlockPropertiesTable"/> objects in the document. The main
    /// library has no public API to enumerate a document's standalone objects (a
    /// BPT is a data-only object no table or dictionary references), so the
    /// private <c>_cadObjects</c> field is read — the same approach
    /// <c>BlockPropertiesTableTests.Collect</c> uses.
    /// </summary>
    public static List<BlockPropertiesTable> Collect(CadDocument document)
    {
        FieldInfo? field = typeof(CadDocument).GetField("_cadObjects", BindingFlags.NonPublic | BindingFlags.Instance);
        if (field?.GetValue(document) is not IDictionary dict)
        {
            return new List<BlockPropertiesTable>();
        }

        return dict.Values
            .Cast<object>()
            .OfType<BlockPropertiesTable>()
            .OrderBy(t => t.Handle)
            .ToList();
    }
}
