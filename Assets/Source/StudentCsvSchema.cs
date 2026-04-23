using System.Collections.Generic;
using System.Globalization;
using System.Text;

internal static class StudentCsvSchema
{
    public static readonly string[] Metadata = {
        "Ticker", "YEAR", "Environmental", "Social", "Governance", "ESG_score", "GVKEY",
        "Address Line 1", "Postal Code", "City",
        "Mining", "Construction", "Manufactuing", "Transportation Public Utilities",
        "Wholesale Trade", "Retail Trade", "Services",
        "SIC code", "Company Name", "CIK Number", "Country Code"
    };

    public static readonly string[] IndustryFlags = {
        "Mining", "Construction", "Manufactuing",
        "Transportation Public Utilities", "Wholesale Trade",
        "Retail Trade", "Services"
    };

    public static void WriteHeader(StringBuilder sb)
    {
        for (int i = 0; i < Metadata.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(Metadata[i]);
        }
        for (int i = 0; i < FormulaCalculator.FormulaColumns.Length; i++)
        {
            sb.Append(',');
            sb.Append(FormulaCalculator.FormulaColumns[i]);
        }
        sb.Append('\n');
    }

    public static void WriteMetadataRow(StringBuilder sb, SurfaceDataSource.DataRow row, Dictionary<string, int> numCol)
    {
        var inv = CultureInfo.InvariantCulture;
        AppendEscaped(sb, row.Ticker); sb.Append(',');
        AppendEscaped(sb, row.Year); sb.Append(',');
        sb.Append(FormatNumeric(row, numCol, "Environmental")); sb.Append(',');
        sb.Append(FormatNumeric(row, numCol, "Social")); sb.Append(',');
        sb.Append(FormatNumeric(row, numCol, "Governance")); sb.Append(',');
        sb.Append(FormatNumeric(row, numCol, "ESG_score")); sb.Append(',');
        sb.Append(','); // GVKEY
        sb.Append(','); // Address Line 1
        sb.Append(','); // Postal Code
        AppendEscaped(sb, row.City); sb.Append(',');
        for (int i = 0; i < IndustryFlags.Length; i++)
        {
            sb.Append(IndustryFlags[i] == row.Industry ? "1" : "0");
            sb.Append(',');
        }
        sb.Append(row.SICCode.ToString(inv)); sb.Append(',');
        AppendEscaped(sb, row.CompanyName); sb.Append(',');
        sb.Append(','); // CIK Number
        AppendEscaped(sb, row.CountryCode);
    }

    public static void AppendEscaped(StringBuilder sb, string s)
    {
        if (string.IsNullOrEmpty(s)) return;
        if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
        {
            sb.Append('"');
            sb.Append(s.Replace("\"", "\"\""));
            sb.Append('"');
        }
        else
        {
            sb.Append(s);
        }
    }

    public static string FormatNumeric(SurfaceDataSource.DataRow row, Dictionary<string, int> map, string name)
    {
        if (!map.TryGetValue(name, out int i)) return "";
        if (row.NumericValues == null || i < 0 || i >= row.NumericValues.Length) return "";
        return row.NumericValues[i].ToString("R", CultureInfo.InvariantCulture);
    }

    public static Dictionary<string, int> BuildColumnMap(CSVDataSource src)
    {
        var map = new Dictionary<string, int>();
        var names = src.NumericColumnNames;
        for (int i = 0; i < names.Count; i++) map[names[i]] = i;
        return map;
    }

    public static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        bool inQuotes = false;
        var sb = new StringBuilder(64);

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = false;
                }
                else sb.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',') { fields.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
        }
        fields.Add(sb.ToString());
        return fields;
    }
}
