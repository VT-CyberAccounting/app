using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DataWorld
{
    /// <summary>
    /// Spawns one block (a scaled cube) per variable per company-year record.
    ///
    /// Layout (top-down view):
    ///
    ///   Z (depth) ──► company rows
    ///   X (right)  ──► variable columns
    ///   Y (up)     ──► normalized value (block height)
    ///
    /// Within each variable column, company rows are spaced along Z.
    /// Within each company row, 2019 bar is spawned at X offset -barPairOffset,
    /// 2020 bar at X offset +barPairOffset.
    ///
    /// Variable groups are spaced every varSpacing units along X.
    /// Company rows are spaced every companySpacing units along Z.
    ///
    /// Usage:
    ///   - Attach to an empty GameObject at your scene origin.
    ///   - Assign blockPrefab (simple cube with a MeshRenderer).
    ///   - Assign label2019Material and label2020Material.
    ///   - Assign blockParent (empty transform to keep hierarchy clean).
    /// </summary>
    public class BlockSpawner : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────
        [Header("Prefab & Materials")]
        [SerializeField] private GameObject blockPrefab;          // Simple 1×1×1 cube
        [SerializeField] private Material   material2019;         // Purple
        [SerializeField] private Material   material2020;         // Teal

        [Header("Layout")]
        [SerializeField] private Transform  blockParent;          // Parent for spawned blocks

        [Tooltip("World-space X distance between variable groups")]
        [SerializeField] private float varSpacing      = 0.9f;

        [Tooltip("World-space Z distance between company rows")]
        [SerializeField] private float companySpacing  = 0.7f;

        [Tooltip("Max block height in world units (maps to normalized value = 1.0)")]
        [SerializeField] private float maxBlockHeight  = 2.0f;

        [Tooltip("Block width (X and Z scale)")]
        [SerializeField] private float blockWidth      = 0.18f;

        [Tooltip("Horizontal offset between the 2019 and 2020 bar of the same company")]
        [SerializeField] private float barPairOffset   = 0.12f;

        [Tooltip("Minimum block height so blocks are always visible (even for zero values)")]
        [SerializeField] private float minBlockHeight  = 0.02f;

        // Variables to visualize — subset of all 35; adjust as needed
        // Keeping all but the "last year" reference columns for clarity
        public static readonly string[] DisplayVars = new[]
        {
            "ESG_score",
            "Current Assets", "Assets", "Cash", "Inventory",
            "Current Liabilities", "Liabilities",
            "Stockholders Equity",
            "Sales/Turnover (Net)", "Cost of Goods Sold",
            "Earnings Before Interest and Taxes",
            "Net Income (Loss)",
            "Total Debt Including Current",
            "Price Close - Annual -",
            "Earnings Per Share (Basic)"
        };

        // ── Runtime ───────────────────────────────────────────────────
        private readonly List<GameObject> _spawnedBlocks = new List<GameObject>();

        private void Start()
        {
            DataManager.Instance.OnDataReady.AddListener(() =>
            {
                FilterManager.Instance.OnFilterChanged.AddListener(Rebuild);
                // Initial build with no filters = empty scene (wait for user to select)
            });
        }

        // ── Main rebuild ──────────────────────────────────────────────

        public void Rebuild()
        {
            ClearBlocks();

            var dm      = DataManager.Instance;
            var fm      = FilterManager.Instance;
            var records = dm.GetFiltered(fm.ActiveCountries, fm.ActiveYears, fm.ActiveCompanies);

            if (records.Count == 0) return;

            // Group by company name then by year
            var byCompany = records
                .GroupBy(r => r.CompanyName)
                .OrderBy(g => g.Key)
                .ToList();

            for (int compIdx = 0; compIdx < byCompany.Count; compIdx++)
            {
                var group     = byCompany[compIdx];
                float zOffset = compIdx * companySpacing;

                // Within each company group, split into years
                var byYear = group.OrderBy(r => r.Year).ToList();

                // Spawn an axis label for the company (on ground at Z position)
                SpawnCompanyLabel(group.Key, zOffset);

                for (int varIdx = 0; varIdx < DisplayVars.Length; varIdx++)
                {
                    string varName = DisplayVars[varIdx];
                    float  xCenter = varIdx * varSpacing;

                    // Spawn a variable label at the far edge (first company row)
                    if (compIdx == 0)
                        SpawnVarLabel(varName, xCenter, zOffset);

                    foreach (var record in byYear)
                    {
                        float xOffset = record.Year == 2019 ? -barPairOffset : barPairOffset;
                        float rawVal  = record.FinancialValues.TryGetValue(varName, out float v) ? v : 0f;
                        float norm    = dm.Normalize(varName, rawVal);
                        float height  = Mathf.Max(minBlockHeight, norm * maxBlockHeight);

                        var block = SpawnBlock(
                            position : new Vector3(xCenter + xOffset, height * 0.5f, zOffset),
                            scale    : new Vector3(blockWidth, height, blockWidth),
                            material : record.Year == 2019 ? material2019 : material2020,
                            record   : record,
                            varName  : varName,
                            rawValue : rawVal
                        );

                        _spawnedBlocks.Add(block);
                    }
                }
            }
        }

        // ── Block spawn ───────────────────────────────────────────────

        private GameObject SpawnBlock(
            Vector3 position, Vector3 scale,
            Material material,
            CompanyRecord record, string varName, float rawValue)
        {
            var go = Instantiate(blockPrefab, blockParent);
            go.transform.localPosition = position;
            go.transform.localScale    = scale;

            go.GetComponent<MeshRenderer>().material = material;

            // Attach interactive component
            var dataBlock = go.AddComponent<DataBlock>();
            dataBlock.Init(record, varName, rawValue);

            return go;
        }

        // ── Label helpers (TextMeshPro world-space text) ──────────────

        private void SpawnVarLabel(string varName, float x, float z)
        {
            // Short display name
            string shortName = VarShortName(varName);

            var go   = new GameObject($"Label_Var_{shortName}");
            go.transform.SetParent(blockParent, false);
            go.transform.localPosition = new Vector3(x, -0.1f, z - companySpacing * 0.5f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale    = Vector3.one * 0.12f;

            var tmp  = go.AddComponent<TMPro.TextMeshPro>();
            tmp.text             = shortName;
            tmp.alignment        = TMPro.TextAlignmentOptions.Center;
            tmp.fontSize         = 8f;
            tmp.color            = Color.white;
            tmp.GetComponent<MeshRenderer>().sortingOrder = 1;
        }

        private void SpawnCompanyLabel(string companyName, float z)
        {
            var go   = new GameObject($"Label_Company_{companyName}");
            go.transform.SetParent(blockParent, false);
            go.transform.localPosition = new Vector3(-0.8f, 0f, z);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            go.transform.localScale    = Vector3.one * 0.1f;

            var tmp  = go.AddComponent<TMPro.TextMeshPro>();
            // Truncate long company names
            tmp.text      = companyName.Length > 20 ? companyName.Substring(0, 20) + "…" : companyName;
            tmp.alignment = TMPro.TextAlignmentOptions.Right;
            tmp.fontSize  = 6f;
            tmp.color     = Color.white;
        }

        // ── Cleanup ───────────────────────────────────────────────────

        private void ClearBlocks()
        {
            foreach (var go in _spawnedBlocks)
                if (go != null) Destroy(go);
            _spawnedBlocks.Clear();

            // Also remove dynamically spawned labels
            if (blockParent != null)
            {
                foreach (Transform child in blockParent)
                {
                    if (child.name.StartsWith("Label_"))
                        Destroy(child.gameObject);
                }
            }
        }

        // ── Short name map ────────────────────────────────────────────

        private static readonly Dictionary<string, string> ShortNames = new()
        {
            { "ESG_score",                          "ESG"           },
            { "Current Assets",                     "Curr.Assets"   },
            { "Assets",                             "Assets"        },
            { "Cash",                               "Cash"          },
            { "Inventory",                          "Inventory"     },
            { "Current Liabilities",                "Curr.Liab."    },
            { "Liabilities",                        "Liabilities"   },
            { "Stockholders Equity",                "Equity"        },
            { "Sales/Turnover (Net)",               "Sales"         },
            { "Cost of Goods Sold",                 "COGS"          },
            { "Earnings Before Interest and Taxes", "EBIT"          },
            { "Net Income (Loss)",                  "Net Inc."      },
            { "Total Debt Including Current",       "Total Debt"    },
            { "Price Close - Annual -",             "Price"         },
            { "Earnings Per Share (Basic)",         "EPS"           },
        };

        private string VarShortName(string varName) =>
            ShortNames.TryGetValue(varName, out string s) ? s : varName;
    }
}
