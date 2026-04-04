using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace DataWorld
{
    /// <summary>
    /// Loads dataworld_solutions.csv from StreamingAssets.
    /// Call LoadData() once at startup (e.g. from DataManager.Awake).
    ///
    /// StreamingAssets path on Meta Quest: jar:file:// → must use UnityWebRequest.
    /// On Editor/PC: direct File.ReadAllText works fine.
    /// </summary>
    public class CsvLoader : MonoBehaviour
    {
        // Financial variable column names (order must match CSV header)
        public static readonly string[] FinancialVarNames = new[]
        {
            "Environmental", "Social", "Governance", "ESG_score",
            "Current Assets", "Assets", "Cash", "Inventory",
            "Current Marketable Securities", "Current Liabilities", "Liabilities",
            "Property, Plant and Equipment", "Preferred/Preference Stock",
            "Allowance for Doubtful Receivables", "Total Receivables",
            "Stockholders Equity", "Cost of Goods Sold",
            "Dividends - Preferred/Preference", "Dividends",
            "Earnings Before Interest and Taxes", "Earnings Per Share (Basic)",
            "Net Income (Loss)", "Net Income Adjusted for common stocks",
            "Sales/Turnover (Net)", "Interest and Related Expense",
            "Common Shares Outstanding", "Total Debt Including Current",
            "Price Close - Annual -", "Net receivables",
            "Total assets last year", "Net receivables last year",
            "Inventory last year", "Stockholder equity last year",
            "Cost of Goods Sold last year", "Common shares outstanding last year"
        };

        private const string CsvFileName = "dataworld_solutions.csv";

        public IEnumerator Load(Action<List<CompanyRecord>> onComplete)
        {
            string path = Path.Combine(Application.streamingAssetsPath, CsvFileName);

#if UNITY_ANDROID && !UNITY_EDITOR
            // Android / Meta Quest: use UnityWebRequest
            using var req = UnityWebRequest.Get(path);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[CsvLoader] Failed to load CSV: {req.error}");
                onComplete?.Invoke(null);
                yield break;
            }
            string rawCsv = req.downloadHandler.text;
#else
            // Editor / PC: direct read
            if (!File.Exists(path))
            {
                Debug.LogError($"[CsvLoader] CSV not found at: {path}");
                onComplete?.Invoke(null);
                yield break;
            }
            string rawCsv = File.ReadAllText(path);
            yield return null;  // keep coroutine contract
#endif
            var records = Parse(rawCsv);
            Debug.Log($"[CsvLoader] Loaded {records.Count} records.");
            onComplete?.Invoke(records);
        }

        // ── CSV Parser ───────────────────────────────────────────────────────────

        private List<CompanyRecord> Parse(string csv)
        {
            var records = new List<CompanyRecord>();
            var lines   = csv.Split('\n');

            if (lines.Length < 2) return records;

            // Build header → index map
            var headers = SplitLine(lines[0]);
            var idx     = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Length; i++)
                idx[headers[i].Trim()] = i;

            for (int row = 1; row < lines.Length; row++)
            {
                string line = lines[row].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var cols = SplitLine(line);
                var r    = new CompanyRecord();

                r.Ticker      = GetStr(cols, idx, "Ticker");
                r.Year        = GetInt(cols,  idx, "YEAR");
                r.CompanyName = GetStr(cols, idx, "Company Name");
                r.CountryCode = GetStr(cols, idx, "Country Code");
                r.City        = GetStr(cols, idx, "City");
                r.SicCode     = GetInt(cols,  idx, "SIC code");

                r.IsMining        = GetInt(cols, idx, "Mining")        == 1;
                r.IsConstruction  = GetInt(cols, idx, "Construction")  == 1;
                r.IsManufacturing = GetInt(cols, idx, "Manufacturing") == 1;
                r.IsTransportation= GetInt(cols, idx, "Transportation Public Utilities") == 1;
                r.IsWholesale     = GetInt(cols, idx, "Wholesale Trade") == 1;
                r.IsRetail        = GetInt(cols, idx, "Retail Trade")  == 1;
                r.IsServices      = GetInt(cols, idx, "Services")      == 1;

                r.Environmental = GetFloat(cols, idx, "Environmental");
                r.Social        = GetFloat(cols, idx, "Social");
                r.Governance    = GetFloat(cols, idx, "Governance");
                r.EsgScore      = GetFloat(cols, idx, "ESG_score");

                // All financial variables into dictionary
                foreach (var varName in FinancialVarNames)
                    r.FinancialValues[varName] = GetFloat(cols, idx, varName);

                records.Add(r);
            }

            return records;
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        // Handles quoted fields with commas inside
        private string[] SplitLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var  current  = new System.Text.StringBuilder();

            foreach (char c in line)
            {
                if (c == '"')      { inQuotes = !inQuotes; continue; }
                if (c == ',' && !inQuotes) { result.Add(current.ToString()); current.Clear(); }
                else               current.Append(c);
            }
            result.Add(current.ToString());
            return result.ToArray();
        }

        private string GetStr(string[] cols, Dictionary<string, int> idx, string key)
        {
            if (!idx.TryGetValue(key, out int i) || i >= cols.Length) return "";
            return cols[i].Trim();
        }

        private float GetFloat(string[] cols, Dictionary<string, int> idx, string key)
        {
            if (!idx.TryGetValue(key, out int i) || i >= cols.Length) return 0f;
            return float.TryParse(cols[i].Trim(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : 0f;
        }

        private int GetInt(string[] cols, Dictionary<string, int> idx, string key)
        {
            if (!idx.TryGetValue(key, out int i) || i >= cols.Length) return 0;
            return int.TryParse(cols[i].Trim(), out int v) ? v : 0;
        }
    }
}
