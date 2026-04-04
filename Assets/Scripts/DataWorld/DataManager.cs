using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace DataWorld
{
    /// <summary>
    /// Singleton. Owns all loaded CompanyRecords and the per-variable
    /// normalization bounds (min / max across the ENTIRE dataset).
    ///
    /// Normalization strategy: "each variable normalized within itself (max → 1)"
    /// Negative values (e.g. Net Income in a loss year) are clamped to 0 for
    /// height encoding, but the raw value is always accessible for the label.
    /// </summary>
    public class DataManager : MonoBehaviour
    {
        public static DataManager Instance { get; private set; }

        // ── Events ───────────────────────────────────────────────────
        // Fired once data is loaded and ready
        public UnityEvent OnDataReady = new UnityEvent();

        // ── Runtime data ─────────────────────────────────────────────
        public List<CompanyRecord>             AllRecords    { get; private set; }
        public List<string>                    AllCountries  { get; private set; }
        public List<int>                       AllYears      { get; private set; }
        public List<string>                    AllCompanies  { get; private set; }

        // Per-variable: absolute max over full dataset (used for height normalization)
        private Dictionary<string, float> _varMax = new Dictionary<string, float>();
        private Dictionary<string, float> _varMin = new Dictionary<string, float>();

        // ── Unity lifecycle ──────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            var loader = gameObject.AddComponent<CsvLoader>();
            StartCoroutine(loader.Load(OnCsvLoaded));
        }
        

        private void OnCsvLoaded(List<CompanyRecord> records)
        {
            if (records == null || records.Count == 0)
            {
                Debug.LogError("[DataManager] No records loaded — check StreamingAssets.");
                return;
            }

            AllRecords = records;

            // Build filter lists (sorted, unique)
            AllCountries = records.Select(r => r.CountryCode)
                                  .Distinct().OrderBy(x => x).ToList();
            AllYears     = records.Select(r => r.Year)
                                  .Distinct().OrderBy(x => x).ToList();
            AllCompanies = records.Select(r => r.CompanyName)
                                  .Distinct().OrderBy(x => x).ToList();

            // Compute per-variable global min / max for normalization
            foreach (var varName in CsvLoader.FinancialVarNames)
            {
                var values = records
                    .Where(r  => r.FinancialValues.ContainsKey(varName))
                    .Select(r => r.FinancialValues[varName]);

                _varMax[varName] = values.Any() ? values.Max() : 1f;
                _varMin[varName] = values.Any() ? values.Min() : 0f;
            }

            Debug.Log($"[DataManager] Ready — {AllRecords.Count} records, " +
                      $"{AllCountries.Count} countries, {AllCompanies.Count} companies.");
            OnDataReady.Invoke();
        }

        // ── Public API ────────────────────────────────────────────────

        /// <summary>
        /// Returns records matching ALL active filters.
        /// </summary>
        public List<CompanyRecord> GetFiltered(
            IEnumerable<string> countries,
            IEnumerable<int>    years,
            IEnumerable<string> companies)
        {
            var countrySet  = new HashSet<string>(countries);
            var yearSet     = new HashSet<int>(years);
            var companySet  = new HashSet<string>(companies);

            return AllRecords
                .Where(r => (countrySet.Count  == 0 || countrySet.Contains(r.CountryCode))
                         && (yearSet.Count     == 0 || yearSet.Contains(r.Year))
                         && (companySet.Count  == 0 || companySet.Contains(r.CompanyName)))
                .ToList();
        }

        /// <summary>
        /// Normalizes a raw value into [0,1] using the global min/max for that variable.
        /// Negative raw values map to 0 for bar height; positive values map linearly.
        /// </summary>
        public float Normalize(string varName, float rawValue)
        {
            if (!_varMax.TryGetValue(varName, out float max)) return 0f;
            float min = _varMin.TryGetValue(varName, out float m) ? m : 0f;

            // Clamp negatives to 0 for height encoding
            float clamped = Mathf.Max(0f, rawValue);
            float range   = max - Mathf.Min(0f, min);

            return range > 0f ? Mathf.Clamp01(clamped / range) : 0f;
        }

        /// <summary>
        /// Global max for a variable (useful for tooltip labels).
        /// </summary>
        public float GetMax(string varName) =>
            _varMax.TryGetValue(varName, out float v) ? v : 1f;
    }
}
