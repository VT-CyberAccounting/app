using System.Collections.Generic;

namespace DataWorld
{
    /// <summary>
    /// One row from dataworld_solutions.csv — a single company in a single year.
    /// Financial values are stored as raw floats; normalization is handled by NormManager.
    /// </summary>
    public class CompanyRecord
    {
        // ── Identifiers ─────────────────────────────────────────────
        public string Ticker;
        public int    Year;
        public string CompanyName;
        public string CountryCode;
        public string City;
        public int    SicCode;

        // ── Industry flags ──────────────────────────────────────────
        public bool IsMining;
        public bool IsConstruction;
        public bool IsManufacturing;
        public bool IsTransportation;
        public bool IsWholesale;
        public bool IsRetail;
        public bool IsServices;

        // ── ESG ─────────────────────────────────────────────────────
        public float Environmental;
        public float Social;
        public float Governance;
        public float EsgScore;

        // ── Financial variables (raw, in millions USD) ──────────────
        // Key = exact column name from CSV (used as block label in scene)
        public Dictionary<string, float> FinancialValues = new Dictionary<string, float>();

        // Convenience: unique key for this record
        public string UniqueKey => $"{Ticker}_{Year}";
    }
}
