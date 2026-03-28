// ViewManager.cs
// Central controller for the four-tier analytics view system.
//
// SETUP INSTRUCTIONS (after importing all five new scripts):
//  1. Create an empty GameObject named "ViewManager" in the scene.
//     Attach this script to it.
//  2. Create three empty panel GameObjects under your main Canvas:
//       "DiagnosticPanel"    → attach DiagnosticView.cs
//       "PredictivePanel"    → attach PredictiveFetcher.cs
//       "PrescriptivePanel"  → attach PrescriptiveFetcher.cs
//  3. In the ViewManager Inspector, assign:
//       tier2DiagnosticPanel   → DiagnosticPanel
//       tier3PredictivePanel   → PredictivePanel
//       tier4PrescriptivePanel → PrescriptivePanel
//  4. Assign any old toggle / button root GameObjects to legacyUIObjects.
//  5. Assign existing 3-D visual system roots to tier1Objects:
//       e.g. heatmap shells parent, power gauges parent, avatar container.
//  6. Enter Play mode – a 56 px tab bar appears at the top of the Canvas,
//     all old toggles are hidden, and Tier 1 (Descriptive) is active.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ViewManager : MonoBehaviour
{
    // ── Inspector wiring ─────────────────────────────────────────────────
    [Header("Legacy UI (will be hidden on Start)")]
    [SerializeField] private GameObject[] legacyUIObjects;

    [Header("Tier 1 – Descriptive (existing 3-D visuals)")]
    [SerializeField] private GameObject[] tier1Objects;

    [Header("Tier 1 – Sensor Data Panel")]
    [SerializeField] private GameObject sensorDataPanel;

    [Header("Canvas (assign your main UI Canvas here)")]
    [SerializeField] private Canvas targetCanvas;

    [Header("Tier 2-4 Panels")]
    [SerializeField] private GameObject tier2DiagnosticPanel;
    [SerializeField] private GameObject tier3PredictivePanel;
    [SerializeField] private GameObject tier4PrescriptivePanel;

    // ── Colours ──────────────────────────────────────────────────────────
    private static readonly Color ColActive   = HexColor("#3389F2");
    private static readonly Color ColInactive = HexColor("#2E2E2E");
    private static readonly Color ColTabText  = Color.white;

    // ── Runtime state ────────────────────────────────────────────────────
    private readonly List<Button> _tabButtons     = new();
    private readonly List<Image>  _tabBackgrounds = new();
    private int _currentTier = -1;

    // ── Unity lifecycle ──────────────────────────────────────────────────
    private void Start()
    {
        HideLegacyUI();
        BuildTabBar();
        SetView(0);
    }

    // ── Public API ───────────────────────────────────────────────────────
    public void SetView(int tier)
    {
        _currentTier = tier;

        // Tier 1 objects
        bool showTier1 = (tier == 0);
        foreach (var go in tier1Objects)
            if (go != null) go.SetActive(showTier1);
        SetActiveIfNotNull(sensorDataPanel, showTier1);

        // Tier 2-4 panels
        SetActiveIfNotNull(tier2DiagnosticPanel,   tier == 1);
        SetActiveIfNotNull(tier3PredictivePanel,   tier == 2);
        SetActiveIfNotNull(tier4PrescriptivePanel, tier == 3);

        // Tab highlight
        for (int i = 0; i < _tabBackgrounds.Count; i++)
            _tabBackgrounds[i].color = (i == tier) ? ColActive : ColInactive;
    }

    // ── Private helpers ──────────────────────────────────────────────────
    private void HideLegacyUI()
    {
        foreach (var go in legacyUIObjects)
            if (go != null) go.SetActive(false);
    }

    private void BuildTabBar()
    {
        // Use the explicitly assigned canvas, or fall back to auto-find
        Canvas canvas = targetCanvas != null ? targetCanvas : FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[ViewManager] No Canvas found in scene. Assign it to the 'Target Canvas' field.");
            return;
        }

        // Remove any previously built bar (prevents duplicates on domain reload)
        var existing = canvas.transform.Find("TierTabBar");
        if (existing != null) Destroy(existing.gameObject);

        // ── Tab bar root ──────────────────────────────────────────────
        var barGO = new GameObject("TierTabBar", typeof(RectTransform), typeof(Image),
                                   typeof(HorizontalLayoutGroup));
        barGO.transform.SetParent(canvas.transform, false);

        var barRect = barGO.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 1f);
        barRect.anchorMax = new Vector2(1f, 1f);
        barRect.pivot     = new Vector2(0.5f, 1f);
        barRect.sizeDelta = new Vector2(0f, 56f);
        barRect.anchoredPosition = Vector2.zero;

        barGO.GetComponent<Image>().color = HexColor("#1A1A1A");

        var hlg = barGO.GetComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth  = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth  = true;
        hlg.childForceExpandHeight = true;
        hlg.spacing = 2f;
        hlg.padding = new RectOffset(4, 4, 4, 4);

        // ── Four tab buttons ──────────────────────────────────────────
        string[] labels = { "Descriptive", "Diagnostic", "Predictive", "Prescriptive" };
        for (int i = 0; i < labels.Length; i++)
        {
            int capturedIndex = i;

            var btnGO = new GameObject($"Tab_{labels[i]}", typeof(RectTransform),
                                       typeof(Image), typeof(Button));
            btnGO.transform.SetParent(barGO.transform, false);

            var bg = btnGO.GetComponent<Image>();
            bg.color = ColInactive;
            _tabBackgrounds.Add(bg);

            var btn = btnGO.GetComponent<Button>();
            var cb  = new Button.ButtonClickedEvent();
            cb.AddListener(() => SetView(capturedIndex));
            btn.onClick = cb;
            // Remove default colour transitions so our manual colours persist
            var colours = btn.colors;
            colours.normalColor      = Color.white;
            colours.highlightedColor = Color.white;
            colours.pressedColor     = HexColor("#1A6DC7");
            colours.selectedColor    = Color.white;
            btn.colors = colours;
            btn.targetGraphic = bg;
            _tabButtons.Add(btn);

            // Label
            var txtGO = new GameObject("Label", typeof(RectTransform),
                                       typeof(TextMeshProUGUI));
            txtGO.transform.SetParent(btnGO.transform, false);
            var txtRect = txtGO.GetComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.sizeDelta = Vector2.zero;
            txtRect.anchoredPosition = Vector2.zero;

            var tmp = txtGO.GetComponent<TextMeshProUGUI>();
            tmp.text      = labels[i];
            tmp.color     = ColTabText;
            tmp.fontSize  = 14f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
        }

    }

    private static void SetActiveIfNotNull(GameObject go, bool active)
    {
        if (go != null) go.SetActive(active);
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
