using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using TMPro;

namespace DataWorld
{
    /// <summary>
    /// Attached to each spawned block. Handles:
    ///   - XR hover: shows a floating tooltip with company / year / variable / raw value
    ///   - XR select (trigger): logs the selection (extend for detail panels later)
    ///
    /// Requires XR Interaction Toolkit — this component auto-adds an XRSimpleInteractable.
    /// The tooltip canvas is created at runtime so no prefab dependency is needed.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DataBlock : MonoBehaviour
    {
        // ── Data ──────────────────────────────────────────────────────
        public CompanyRecord Record   { get; private set; }
        public string        VarName  { get; private set; }
        public float         RawValue { get; private set; }

        // ── Tooltip ───────────────────────────────────────────────────
        private GameObject _tooltip;
        private TMP_Text   _tooltipText;

        // ── Hover tint ────────────────────────────────────────────────
        private Material   _originalMaterial;
        private static readonly Color HoverTint = new Color(1f, 1f, 0.5f, 1f);

        // ── Init ──────────────────────────────────────────────────────

        public void Init(CompanyRecord record, string varName, float rawValue)
        {
            Record   = record;
            VarName  = varName;
            RawValue = rawValue;

            _originalMaterial = GetComponent<MeshRenderer>().material;

            // Create tooltip
            _tooltip = BuildTooltip();
            _tooltip.SetActive(false);

            // Hook up XR interactable
            var interactable = gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            interactable.hoverEntered.AddListener(OnHoverEnter);
            interactable.hoverExited.AddListener(OnHoverExit);
            interactable.selectEntered.AddListener(OnSelect);
        }

        // ── XR events ─────────────────────────────────────────────────

        private void OnHoverEnter(HoverEnterEventArgs args)
        {
            _tooltip.SetActive(true);
            // Tint the block for visual feedback
            var mr  = GetComponent<MeshRenderer>();
            var mat = new Material(_originalMaterial);
            mat.color = HoverTint;
            mr.material = mat;
        }

        private void OnHoverExit(HoverExitEventArgs args)
        {
            _tooltip.SetActive(false);
            GetComponent<MeshRenderer>().material = _originalMaterial;
        }

        private void OnSelect(SelectEnterEventArgs args)
        {
            // Future: open detail panel, highlight related blocks, etc.
            Debug.Log($"[DataBlock] Selected — {Record.CompanyName} {Record.Year} | {VarName}: {RawValue:F2}M");
        }

        // ── Tooltip builder ────────────────────────────────────────────

        private GameObject BuildTooltip()
        {
            var go = new GameObject("Tooltip");
            go.transform.SetParent(transform, false);

            // Position tooltip above the block (block height is transform.localScale.y)
            go.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            go.transform.localScale    = Vector3.one * 0.008f; // small world-space text

            // Background quad
            var bg       = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.transform.SetParent(go.transform, false);
            bg.transform.localPosition = Vector3.zero;
            bg.transform.localScale    = new Vector3(160f, 60f, 1f);
            var bgMat    = new Material(Shader.Find("Sprites/Default"));
            bgMat.color  = new Color(0.08f, 0.08f, 0.15f, 0.88f);
            bg.GetComponent<MeshRenderer>().material = bgMat;
            Destroy(bg.GetComponent<Collider>());

            // Text
            var textGo   = new GameObject("TooltipText");
            textGo.transform.SetParent(go.transform, false);
            textGo.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            textGo.transform.localScale    = Vector3.one;

            _tooltipText = textGo.AddComponent<TextMeshPro>();
            _tooltipText.text      = BuildTooltipString();
            _tooltipText.fontSize  = 14f;
            _tooltipText.color     = Color.white;
            _tooltipText.alignment = TextAlignmentOptions.Center;
            _tooltipText.rectTransform.sizeDelta = new Vector2(155f, 55f);

            // Billboard: always face user
            go.AddComponent<Billboard>();

            return go;
        }

        private string BuildTooltipString()
        {
            string unit = IsPerShareVar(VarName) ? "" : "M USD";
            return $"<b>{Record.CompanyName}</b>\n" +
                   $"{Record.Year}   |   {VarName}\n" +
                   $"<color=#88FFCC>{RawValue:F2} {unit}</color>";
        }

        private static bool IsPerShareVar(string v) =>
            v.Contains("Per Share") || v.Contains("Price Close");

        // ── Tooltip position update (keep above block top) ─────────────

        private void LateUpdate()
        {
            var cam = Camera.main ?? FindFirstObjectByType<Camera>();
            if (cam == null) return;
            transform.LookAt(cam.transform);
            transform.rotation = Quaternion.LookRotation(
                transform.position - cam.transform.position);
        }
    }

    /// <summary>
    /// Simple billboard component — always faces the main camera.
    /// </summary>
    public class Billboard : MonoBehaviour
    {
        private void LateUpdate()
        {
            if (Camera.main == null) return;
            transform.LookAt(Camera.main.transform);
            transform.rotation = Quaternion.LookRotation(
                transform.position - Camera.main.transform.position);
        }
    }
}
