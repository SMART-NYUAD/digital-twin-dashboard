// PredictiveFetcher.cs
// Tier 3 – 72-hour PM2.5 forecast with line chart + model evaluation metrics.
// Panel: right-side overlay, 360×440 px.

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PredictiveFetcher : MonoBehaviour
{
    // ── Dummy forecast (73 points, 0 h … 72 h) ───────────────────────────
    private static readonly float[] ForecastData = GenerateForecast();

    private static float[] GenerateForecast()
    {
        var d = new float[73];
        for (int i = 0; i < 73; i++)
        {
            float t = i / 72f * Mathf.PI * 4f;
            d[i] = Mathf.Max(2f,
                12f + Mathf.Sin(t) * 5f
                    + Mathf.Sin(t * 2.7f) * 1.5f
                    + Mathf.Sin(t * 0.5f) * 2.5f);
        }
        return d;
    }

    // ── Dummy model metrics ───────────────────────────────────────────────
    private const float RMSE = 1.82f;
    private const float MAE  = 1.45f;
    private const float R2   = 0.91f;
    private const float MAPE = 12.3f;

    private const float EPA_GUIDELINE = 9f;   // µg/m³ annual EPA standard

    // ── Runtime ──────────────────────────────────────────────────────────
    private Sprite           _roundedRect, _roundedCard;
    private LineChartGraphic _chart;
    private TextMeshProUGUI  _statsText;

    // ── Lifecycle ────────────────────────────────────────────────────────
    private void Start() { _roundedRect = MakeRoundedRect(64, 6); _roundedCard = MakeRoundedRect(64, 3); BuildPanel(); LoadForecast(); }

    // ── Build ─────────────────────────────────────────────────────────────
    private void BuildPanel()
    {
        var rt = gameObject.GetOrAddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 0.5f); rt.anchorMax = new Vector2(1, 0.5f);
        rt.pivot = new Vector2(1, 0.5f); rt.sizeDelta = new Vector2(360, 460);
        rt.anchoredPosition = new Vector2(-8, 0);
        var panelImg = gameObject.GetOrAddComponent<Image>();
        panelImg.sprite = _roundedRect; panelImg.type = Image.Type.Sliced;
        panelImg.color = new Color(0.10f, 0.10f, 0.12f, 0.92f);

        float y = -12f;

        AddLabel("PREDICTIVE VIEW",              15f, FontStyles.Bold,   ref y, 28f);
        AddLabel("PM2.5 Forecast – Next 72 h",   13f, FontStyles.Normal, ref y, 20f);

        y -= 4f; AddSep(ref y);

        // ── Chart ─────────────────────────────────────────────────────────
        var chartGO = new GameObject("Chart", typeof(RectTransform),
                                     typeof(LineChartGraphic));
        chartGO.transform.SetParent(transform, false);
        var cr = chartGO.GetComponent<RectTransform>();
        cr.anchorMin = new Vector2(0, 1); cr.anchorMax = new Vector2(1, 1);
        cr.pivot     = new Vector2(0.5f, 1);
        cr.sizeDelta = new Vector2(-20, 170);
        cr.anchoredPosition = new Vector2(0, y); y -= 190f;

        _chart          = chartGO.GetComponent<LineChartGraphic>();
        _chart.RefLine  = EPA_GUIDELINE;
        _chart.color    = Color.white;

        // Axis labels
        AddChartYLabel(chartGO, "30", 0.85f);
        AddChartYLabel(chartGO, " 9", 0.27f);
        AddChartYLabel(chartGO, " 0", 0.02f);
        AddChartXLabel(chartGO, "0h",  0.00f);
        AddChartXLabel(chartGO, "24h", 0.33f);
        AddChartXLabel(chartGO, "48h", 0.66f);
        AddChartXLabel(chartGO, "72h", 0.99f);

        // WHO legend
        AddLegendLine(ref y);

        AddSep(ref y);

        // ── Model metrics row ─────────────────────────────────────────────
        AddLabel("Model Performance", 13f, FontStyles.Bold, ref y, 20f);
        y -= 2f;
        var mRow = AddRow("MetricsRow", ref y, 28f);
        AddMetricCell(mRow, "RMSE",  $"{RMSE:F2}");
        AddMetricCell(mRow, "MAE",   $"{MAE:F2}");
        AddMetricCell(mRow, "R²",    $"{R2:F2}");
        AddMetricCell(mRow, "MAPE",  $"{MAPE:F1}%");

        AddSep(ref y);

        // ── Forecast summary stats ────────────────────────────────────────
        var sGO = new GameObject("Stats", typeof(RectTransform), typeof(TextMeshProUGUI),
                                 typeof(CanvasRenderer));
        sGO.transform.SetParent(transform, false);
        var sr = sGO.GetComponent<RectTransform>();
        sr.anchorMin = new Vector2(0, 1); sr.anchorMax = new Vector2(1, 1);
        sr.pivot = new Vector2(0.5f, 1); sr.sizeDelta = new Vector2(-20, 40);
        sr.anchoredPosition = new Vector2(0, y);
        _statsText = sGO.GetComponent<TextMeshProUGUI>();
        _statsText.fontSize = 13f; _statsText.color = Color.white;
        _statsText.alignment = TextAlignmentOptions.Left;
        _statsText.enableWordWrapping = true;
    }

    // ── Load ──────────────────────────────────────────────────────────────
    private void LoadForecast()
    {
        _chart.Data = ForecastData;
        _chart.SetAllDirty();

        float peak = float.MinValue; int peakIdx = 0;
        float sum = 0;
        for (int i = 0; i < ForecastData.Length; i++)
        {
            sum += ForecastData[i];
            if (ForecastData[i] > peak) { peak = ForecastData[i]; peakIdx = i; }
        }
        float avg = sum / ForecastData.Length;
        int exceedH = 0;
        foreach (var v in ForecastData) if (v > EPA_GUIDELINE) exceedH++;

        _statsText.text =
            $"Current: {ForecastData[0]:F1} µg/m³  ·  Peak: {peak:F1} µg/m³ @ +{peakIdx}h\n" +
            $"72h Avg: {avg:F1} µg/m³  ·  Exceeds EPA limit: {exceedH}h of 72h";
    }

    // ── Chart decoration ──────────────────────────────────────────────────
    private void AddChartYLabel(GameObject chart, string txt, float normY)
    {
        var go = new GameObject("YL", typeof(RectTransform), typeof(TextMeshProUGUI),
                                typeof(CanvasRenderer));
        go.transform.SetParent(chart.transform, false);
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0, normY); r.anchorMax = new Vector2(0, normY);
        r.pivot = new Vector2(0, 0.5f); r.sizeDelta = new Vector2(28, 14);
        r.anchoredPosition = new Vector2(2, 0);  // sits inside the 30 px left padding
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = txt; t.fontSize = 10f; t.color = new Color(1, 1, 1, 0.5f);
        t.alignment = TextAlignmentOptions.Right;
    }

    private void AddChartXLabel(GameObject chart, string txt, float normX)
    {
        var go = new GameObject("XL", typeof(RectTransform), typeof(TextMeshProUGUI),
                                typeof(CanvasRenderer));
        go.transform.SetParent(chart.transform, false);
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(normX, 0); r.anchorMax = new Vector2(normX, 0);
        r.pivot = new Vector2(0.5f, 1); r.sizeDelta = new Vector2(28, 14);
        r.anchoredPosition = new Vector2(normX > 0.5f ? -4f : 4f, 0);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = txt; t.fontSize = 10f; t.color = new Color(1, 1, 1, 0.5f);
        t.alignment = TextAlignmentOptions.Center;
    }

    private void AddLegendLine(ref float y)
    {
        var go = new GameObject("Legend", typeof(RectTransform), typeof(TextMeshProUGUI),
                                typeof(CanvasRenderer));
        go.transform.SetParent(transform, false);
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1);
        r.pivot = new Vector2(0.5f, 1); r.sizeDelta = new Vector2(-20, 16);
        r.anchoredPosition = new Vector2(0, y); y -= 18f;
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = "<color=#FFD84D>── </color>EPA guideline: 9 µg/m³";
        t.fontSize = 10f; t.color = Color.white; t.richText = true;
        t.alignment = TextAlignmentOptions.Left;
    }

    // ── Metric cell (label above, value below) ────────────────────────────
    private void AddMetricCell(GameObject row, string label, string value)
    {
        var cell = new GameObject("M_" + label, typeof(RectTransform),
                                  typeof(Image));
        cell.transform.SetParent(row.transform, false);
        var le = cell.AddComponent<LayoutElement>(); le.flexibleWidth = 1;
        var cellImg = cell.GetComponent<Image>();
        cellImg.sprite = _roundedCard; cellImg.type = Image.Type.Sliced;
        cellImg.color = new Color(0.18f, 0.20f, 0.24f, 1f);

        // Label
        var lGO = new GameObject("L", typeof(RectTransform), typeof(TextMeshProUGUI),
                                 typeof(CanvasRenderer));
        lGO.transform.SetParent(cell.transform, false);
        var lr = lGO.GetComponent<RectTransform>();
        lr.anchorMin = new Vector2(0, 0.5f); lr.anchorMax = new Vector2(1, 1);
        lr.sizeDelta = Vector2.zero;
        var lt = lGO.GetComponent<TextMeshProUGUI>();
        lt.text = label; lt.fontSize = 12f;
        lt.color = new Color(0.65f, 0.65f, 0.65f, 1f);
        lt.alignment = TextAlignmentOptions.Center;

        // Value
        var vGO = new GameObject("V", typeof(RectTransform), typeof(TextMeshProUGUI),
                                 typeof(CanvasRenderer));
        vGO.transform.SetParent(cell.transform, false);
        var vr = vGO.GetComponent<RectTransform>();
        vr.anchorMin = new Vector2(0, 0); vr.anchorMax = new Vector2(1, 0.5f);
        vr.sizeDelta = Vector2.zero;
        var vt = vGO.GetComponent<TextMeshProUGUI>();
        vt.text = value; vt.fontSize = 14f; vt.fontStyle = FontStyles.Bold;
        vt.color = HexColor("#7EC8F8"); vt.alignment = TextAlignmentOptions.Center;
    }

    // ── Generic helpers ───────────────────────────────────────────────────
    private void AddLabel(string txt, float sz, FontStyles fs, ref float y, float h)
    {
        var go = new GameObject("Lbl", typeof(RectTransform), typeof(TextMeshProUGUI),
                                typeof(CanvasRenderer));
        go.transform.SetParent(transform, false);
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1);
        r.pivot = new Vector2(0.5f, 1); r.sizeDelta = new Vector2(-20, h);
        r.anchoredPosition = new Vector2(0, y); y -= h;
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = txt; t.fontSize = sz; t.fontStyle = fs;
        t.color = Color.white; t.alignment = TextAlignmentOptions.Left;
    }

    private void AddSep(ref float y)
    {
        var go = new GameObject("Sep", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1);
        r.pivot = new Vector2(0.5f, 1); r.sizeDelta = new Vector2(-20, 1);
        r.anchoredPosition = new Vector2(0, y); y -= 5f;
        go.GetComponent<Image>().color = new Color(1, 1, 1, 0.15f);
    }

    private GameObject AddRow(string name, ref float y, float h)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
        go.transform.SetParent(transform, false);
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1);
        r.pivot = new Vector2(0.5f, 1); r.sizeDelta = new Vector2(-20, h);
        r.anchoredPosition = new Vector2(0, y); y -= (h + 4f);
        var hlg = go.GetComponent<HorizontalLayoutGroup>();
        hlg.childControlHeight = true; hlg.childForceExpandHeight = true;
        hlg.childForceExpandWidth = false; hlg.spacing = 4f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        return go;
    }

    private static Sprite MakeRoundedRect(int size, int r)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int py = 0; py < size; py++)
            for (int px = 0; px < size; px++)
            {
                float cx = Mathf.Clamp(px, r, size - r);
                float cy = Mathf.Clamp(py, r, size - r);
                float d = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
                tex.SetPixel(px, py, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d + 0.5f)));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                             100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
    }

    private static Color HexColor(string hex)
    { ColorUtility.TryParseHtmlString(hex, out Color c); return c; }
}
