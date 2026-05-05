using System.Collections.Generic;
using System.Globalization;
using System.Text;

public static class SolutionSurfaceBuilder
{
    public static string DetectIndustryFromStudentCsv(string csvText)
    {
        if (string.IsNullOrEmpty(csvText)) return null;

        string[] lines = csvText.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
        if (lines.Length < 2) return null;

        List<string> headers = StudentCsvSchema.ParseCsvLine(lines[0]);
        var headerIdx = new Dictionary<string, int>();
        for (int i = 0; i < headers.Count; i++)
            headerIdx[headers[i].Trim()] = i;

        for (int li = 1; li < lines.Length; li++)
        {
            if (string.IsNullOrWhiteSpace(lines[li])) continue;
            List<string> fields = StudentCsvSchema.ParseCsvLine(lines[li]);

            for (int i = 0; i < StudentCsvSchema.IndustryFlags.Length; i++)
            {
                string ind = StudentCsvSchema.IndustryFlags[i];
                if (!headerIdx.TryGetValue(ind, out int idx)) continue;
                if (idx >= fields.Count) continue;
                if (fields[idx].Trim() == "1") return ind;
            }
        }
        return null;
    }

    public static string BuildSolutionCsv(CSVDataSource masterData, string industry)
    {
        var numCol = StudentCsvSchema.BuildColumnMap(masterData);
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder(8192);

        StudentCsvSchema.WriteHeader(sb);

        var rows = masterData.AllRows;
        int emitted = 0;

        for (int r = 0; r < rows.Count; r++)
        {
            SurfaceDataSource.DataRow row = rows[r];
            if (row.CountryCode != "USA") continue;
            if (row.Industry != industry) continue;

            FormulaCalculator.Inputs inputs = FormulaCalculator.ExtractInputs(row.NumericValues, numCol);
            float[] formulas = FormulaCalculator.Compute(in inputs);

            StudentCsvSchema.WriteMetadataRow(sb, row, numCol);
            for (int i = 0; i < formulas.Length; i++)
            {
                sb.Append(',');
                sb.Append(formulas[i].ToString("R", inv));
            }
            sb.Append('\n');
            emitted++;
        }

        UnityEngine.Debug.Log($"[SolutionSurfaceBuilder] Built solution CSV for industry '{industry}': {emitted} USA rows");
        return sb.ToString();
    }
}
