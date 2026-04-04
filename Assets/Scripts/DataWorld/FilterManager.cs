using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace DataWorld
{
    /// <summary>
    /// Tracks which Countries / Years / Companies are currently selected.
    /// FilterPanelUI calls Toggle*() when a user clicks a list item.
    /// BlockSpawner listens to OnFilterChanged to rebuild the scene.
    ///
    /// Rule: if a set is EMPTY it means "show all" (no filter active).
    /// </summary>
    public class FilterManager : MonoBehaviour
    {
        public static FilterManager Instance { get; private set; }

        public UnityEvent OnFilterChanged = new UnityEvent();

        private readonly HashSet<string> _countries = new HashSet<string>();
        private readonly HashSet<int>    _years     = new HashSet<int>();
        private readonly HashSet<string> _companies = new HashSet<string>();

        // ── Read-only views for BlockSpawner ─────────────────────────
        public IEnumerable<string> ActiveCountries => _countries;
        public IEnumerable<int>    ActiveYears     => _years;
        public IEnumerable<string> ActiveCompanies => _companies;

        // ── Unity lifecycle ───────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        // ── Toggle methods ────────────────────────────────────────────

        public void ToggleCountry(string code)
        {
            if (!_countries.Remove(code)) _countries.Add(code);
            OnFilterChanged.Invoke();
        }

        public void ToggleYear(int year)
        {
            if (!_years.Remove(year)) _years.Add(year);
            OnFilterChanged.Invoke();
        }

        public void ToggleCompany(string name)
        {
            if (!_companies.Remove(name)) _companies.Add(name);
            OnFilterChanged.Invoke();
        }

        // ── Select-all helpers ────────────────────────────────────────

        public void SelectAllCountries()  { _countries.Clear(); OnFilterChanged.Invoke(); }
        public void SelectAllYears()      { _years.Clear();     OnFilterChanged.Invoke(); }
        public void SelectAllCompanies()  { _companies.Clear(); OnFilterChanged.Invoke(); }
        public void ClearAll()
        {
            _countries.Clear(); _years.Clear(); _companies.Clear();
            OnFilterChanged.Invoke();
        }

        // ── State queries ─────────────────────────────────────────────
        public bool IsCountrySelected(string code) => _countries.Count == 0 || _countries.Contains(code);
        public bool IsYearSelected(int year)       => _years.Count == 0     || _years.Contains(year);
        public bool IsCompanySelected(string name) => _companies.Count == 0 || _companies.Contains(name);
    }
}
