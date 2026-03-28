// DiagnosticView.cs  (rewritten)
// Tier 2 – statistical summary of historical sensor data.
// Panel: right-side overlay, 380×460 px
// Shows Avg ± Std / Min / Max for each parameter across three time windows.

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DiagnosticView : MonoBehaviour
{
    // ── Dummy statistics [window][param] = (avg, std, min, max) ─────────
    // Windows: 0=24h, 1=7d, 2=1mo
    // Params:  0=Temp, 1=Humidity, 2=PM2.5, 3=VOC, 4=Lighting, 5=Noise
    private static readonly (float avg, float std, float min, float max)[,] Stats =
    {
        // 24h
        {
            (23.4f,0.6f,21.8f,25.1f), (51.8f,3.2f,44.5f,59.3f),
            (10.8f,2.1f, 6.2f,17.4f), (148f, 18f, 112f, 198f),
            (472f, 55f, 310f, 650f),  (39.2f,3.8f,32.1f,51.6f)
        },
        // 7d
        {
            (23.1f,0.9f,20.5f,26.3f), (52.4f,4.5f,41.0f,63.7f),
            (11.5f,3.4f, 4.8f,22.8f), (152f, 25f, 98f,  231f),
            (461f, 72f, 280f, 710f),  (40.1f,5.2f,30.5f,58.4f)
        },
        // 1mo
        {
            (22.8f,1.2f,19.2f,27.6f), (53.1f,5.8f,38.5f,68.2f),
            (12.3f,4.1f, 3.5f,28.5f), (155f, 32f, 85f,  265f),
            (455f, 88f, 240f, 750f),  (40.8f,6.1f,28.3f,62.1f)
        },
    };

    private static readonly string[] ParamNames = { "Temperature", "Humidity", "PM2.5", "VOC", "Lighting", "Noise" };
    private static readonly string[] Units       = { "°C", "%", "µg/m³", "ppb", "lux", "dB" };
    private static readonly string[] WinLabels   = { "24h", "7d", "1mo" };

    private static readonly Color ColActive   = HexColor("#3389F2");
    private static readonly Color ColInactive = HexColor("#3A3A3A");
    private static readonly Color ColHeader   = HexColor("#888888");
    private static readonly Color ColGreen    = HexColor("#4CAF50");
    private static readonly Color ColYellow   = HexColor("#FFC107");
    private static readonly Color ColOrange   = HexColor("#FF9800");
    private static readonly Color ColRed      = HexColor("#F44336");

    // ── IEQ sub-scores ────────────────────────────────────────────────────
    private static readonly (string abbr, float pct)[] IEQScores =
    {
        ("IEQI", 80.0f),
        ("IIAQ", 58.2f),
        ("ITC",  94.9f),
        ("IAC",  90.0f),
        ("ILC",  85.5f),
    };

    // ── IEQ face icons (assign PNGs from Assets/Images in Inspector) ────────
    [Header("IEQ Icons")]
    [SerializeField] public Sprite faceIconGreen;   // score >= 60
    [SerializeField] public Sprite faceIconOrange;  // score <  60

    // ── Runtime ──────────────────────────────────────────────────────────
    private Sprite            _circleSprite;
    private Sprite            _roundedRect;   // panel background (r=6)
    private Sprite            _roundedCard;   // cards & buttons  (r=3)
    private Button[]          _winBtns  = new Button[3];
    private TextMeshProUGUI[] _avgCells = new TextMeshProUGUI[6];
    private TextMeshProUGUI[] _stdCells = new TextMeshProUGUI[6];
    private TextMeshProUGUI[] _minCells = new TextMeshProUGUI[6];
    private TextMeshProUGUI[] _maxCells = new TextMeshProUGUI[6];

    private void Start() { _circleSprite = MakeCircleSprite(64); _roundedRect = MakeRoundedRect(64, 6); _roundedCard = MakeRoundedRect(64, 3); BuildPanel(); ApplyWindow(0); }

    // ── Build ────────────────────────────────────────────────────────────
    private void BuildPanel()
    {
        var rt = gameObject.GetOrAddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1,0.5f); rt.anchorMax = new Vector2(1,0.5f);
        rt.pivot = new Vector2(1,0.5f); rt.sizeDelta = new Vector2(360,460);
        rt.anchoredPosition = new Vector2(-8,0);
        var panelImg = gameObject.GetOrAddComponent<Image>();
        panelImg.sprite = _roundedRect; panelImg.type = Image.Type.Sliced;
        panelImg.color = new Color(0.10f,0.10f,0.12f,0.92f);

        float y = -12f;
        AddLabel("DIAGNOSTIC VIEW", 15f, FontStyles.Bold, ref y, 22f, TextAlignmentOptions.Left);

        // ── IEQ Sub-Scores ────────────────────────────────────────────────
        y -= 2f; AddSep(ref y);
        AddLabel("IEQ Sub-Scores", 12f, FontStyles.Bold, ref y, 16f, TextAlignmentOptions.Left);
        y -= 1f;
        MakeFullCard("IEQI", 80.0f, "Overall IEQ Index", ref y, 42f);
        y -= 3f;
        MakeCardRow("IIAQ", 58.2f, "Air Quality", "ITC", 94.9f, "Thermal", ref y, 56f);
        y -= 3f;
        MakeCardRow("IAC",  90.0f, "Acoustic",    "ILC", 85.5f, "Visual",  ref y, 56f);

        // Window selector row
        y -= 3f; AddSep(ref y);
        var row0 = AddRow("WinRow", ref y, 28f);
        AddTextInRow(row0, "Period:", 13f, FontStyles.Normal, 60f);
        for (int i = 0; i < 3; i++)
        { int idx = i; _winBtns[i] = AddBtnInRow(row0, WinLabels[i], 52f, () => ApplyWindow(idx)); }

        // Separator
        y -= 2f; AddSep(ref y);

        // Table header
        var hdr = AddRow("HDR", ref y, 20f);
        AddTextInRow(hdr, "Parameter",   13f, FontStyles.Bold,  82f);
        AddTextInRow(hdr, "Avg",         13f, FontStyles.Bold,  82f, ColHeader);
        AddTextInRow(hdr, "± Std",       13f, FontStyles.Bold,  54f, ColHeader);
        AddTextInRow(hdr, "Min",         13f, FontStyles.Bold,  52f, ColHeader);
        AddTextInRow(hdr, "Max",         13f, FontStyles.Bold,  52f, ColHeader);

        AddSep(ref y);

        // Data rows
        for (int i = 0; i < 6; i++)
        {
            y -= 2f;
            var row = AddRow($"Row{i}", ref y, 16f);
            AddTextInRow(row, $"{ParamNames[i]}", 13f, FontStyles.Normal,  82f);
            _avgCells[i] = AddTextInRow(row, "",  13f, FontStyles.Bold,    82f);
            _stdCells[i] = AddTextInRow(row, "",  12f, FontStyles.Normal,  54f, ColHeader);
            _minCells[i] = AddTextInRow(row, "",  12f, FontStyles.Normal,  52f);
            _maxCells[i] = AddTextInRow(row, "",  12f, FontStyles.Normal,  52f);
        }

        AddSep(ref y);
        AddLabel("All values: mean of occupied hours across selected period.",
                 11f, FontStyles.Normal, ref y, 18f, TextAlignmentOptions.Left);
    }

    // ── Apply ────────────────────────────────────────────────────────────
    private void ApplyWindow(int w)
    {
        for (int i = 0; i < _winBtns.Length; i++)
        {
            var img = _winBtns[i]?.GetComponent<Image>();
            if (img) img.color = (i == w) ? ColActive : ColInactive;
        }
        for (int p = 0; p < 6; p++)
        {
            var (avg, std, min, max) = Stats[w, p];
            string u = Units[p];
            _avgCells[p].text = $"{avg:G4}{u}";
            _stdCells[p].text = $"±{std:G3}";
            _minCells[p].text = $"{min:G4}";
            _maxCells[p].text = $"{max:G4}";
        }
    }

    // ── UI helpers ───────────────────────────────────────────────────────
    private void AddLabel(string txt, float sz, FontStyles fs, ref float y, float h,
                          TextAlignmentOptions align)
    {
        var go = new GameObject("Lbl", typeof(RectTransform), typeof(TextMeshProUGUI),
                                typeof(CanvasRenderer));
        go.transform.SetParent(transform, false);
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0,1); r.anchorMax = new Vector2(1,1);
        r.pivot = new Vector2(0.5f,1); r.sizeDelta = new Vector2(-20,h);
        r.anchoredPosition = new Vector2(0,y); y -= h;
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = txt; t.fontSize = sz; t.fontStyle = fs;
        t.color = Color.white; t.alignment = align;
    }

    private void AddSep(ref float y)
    {
        var go = new GameObject("Sep", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0,1); r.anchorMax = new Vector2(1,1);
        r.pivot = new Vector2(0.5f,1); r.sizeDelta = new Vector2(-20,1);
        r.anchoredPosition = new Vector2(0,y); y -= 5f;
        go.GetComponent<Image>().color = new Color(1,1,1,0.15f);
    }

    private GameObject AddRow(string name, ref float y, float h)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
        go.transform.SetParent(transform, false);
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0,1); r.anchorMax = new Vector2(1,1);
        r.pivot = new Vector2(0.5f,1); r.sizeDelta = new Vector2(-20,h);
        r.anchoredPosition = new Vector2(0,y); y -= (h + 3f);
        var hlg = go.GetComponent<HorizontalLayoutGroup>();
        hlg.childControlHeight = true; hlg.childForceExpandWidth = false;
        hlg.spacing = 0f; hlg.childAlignment = TextAnchor.MiddleLeft;
        return go;
    }

    private TextMeshProUGUI AddTextInRow(GameObject row, string txt, float sz,
                                         FontStyles fs, float w, Color? col = null)
    {
        var go = new GameObject("C", typeof(RectTransform), typeof(TextMeshProUGUI),
                                typeof(CanvasRenderer));
        go.transform.SetParent(row.transform, false);
        var le = go.AddComponent<LayoutElement>(); le.preferredWidth = w; le.flexibleWidth = 0;
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = txt; t.fontSize = sz; t.fontStyle = fs;
        t.color = col ?? Color.white; t.alignment = TextAlignmentOptions.Left;
        return t;
    }

    private Button AddBtnInRow(GameObject row, string lbl, float w, System.Action onClick)
    {
        var go = new GameObject("Btn", typeof(RectTransform), typeof(Image),
                                typeof(Button));
        go.transform.SetParent(row.transform, false);
        var le = go.AddComponent<LayoutElement>(); le.preferredWidth = w; le.flexibleWidth = 0;
        var btnImg = go.GetComponent<Image>();
        btnImg.sprite = _roundedCard; btnImg.type = Image.Type.Sliced;
        btnImg.color = ColInactive;
        var btn = go.GetComponent<Button>();
        btn.onClick.AddListener(() => onClick());
        var tgo = new GameObject("L", typeof(RectTransform), typeof(TextMeshProUGUI),
                                 typeof(CanvasRenderer));
        tgo.transform.SetParent(go.transform, false);
        var tr = tgo.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.sizeDelta = Vector2.zero;
        var t = tgo.GetComponent<TextMeshProUGUI>();
        t.text = lbl; t.fontSize = 13f; t.color = Color.white;
        t.alignment = TextAlignmentOptions.Center;
        return btn;
    }

    // ── IEQ score cards ───────────────────────────────────────────────────

    // Full-width card (used for IEQI overall)
    private void MakeFullCard(string abbr, float pct, string desc, ref float y, float h)
    {
        Color valCol = pct >= 80f ? ColGreen : pct >= 60f ? ColYellow : pct >= 40f ? ColOrange : ColRed;

        var card = new GameObject("FC_" + abbr, typeof(RectTransform), typeof(Image));
        card.transform.SetParent(transform, false);
        var cr = card.GetComponent<RectTransform>();
        cr.anchorMin = new Vector2(0,1); cr.anchorMax = new Vector2(1,1);
        cr.pivot = new Vector2(0.5f,1); cr.sizeDelta = new Vector2(-20, h);
        cr.anchoredPosition = new Vector2(0, y); y -= (h + 3f);
        var fcImg = card.GetComponent<Image>();
        fcImg.sprite = _roundedCard; fcImg.type = Image.Type.Sliced;
        fcImg.color = new Color(0.15f, 0.17f, 0.20f, 1f);

        // Left colour stripe
        var stripe = new GameObject("S", typeof(RectTransform), typeof(Image));
        stripe.transform.SetParent(card.transform, false);
        var sr = stripe.GetComponent<RectTransform>();
        sr.anchorMin = Vector2.zero; sr.anchorMax = new Vector2(0, 1);
        sr.pivot = Vector2.zero; sr.sizeDelta = new Vector2(4, 0);
        stripe.GetComponent<Image>().color = valCol;

        // Icon: face sprite (green or orange) with coloured circle fallback
        float textX = 46f;
        {
            var ico = new GameObject("Ico", typeof(RectTransform), typeof(Image));
            ico.transform.SetParent(card.transform, false);
            var ir = ico.GetComponent<RectTransform>();
            ir.anchorMin = new Vector2(0, 0.5f); ir.anchorMax = new Vector2(0, 0.5f);
            ir.pivot = new Vector2(0, 0.5f); ir.sizeDelta = new Vector2(30, 30);
            ir.anchoredPosition = new Vector2(10, 0);
            var ii = ico.GetComponent<Image>();
            var face = FaceFor(pct);
            if (face != null) { ii.sprite = face; ii.color = Color.white; }
            else { ii.sprite = _circleSprite; ii.color = new Color(valCol.r, valCol.g, valCol.b, 0.85f); }
            ii.preserveAspect = true;
        }

        // Abbr + value (upper ~60%)
        var lgo = new GameObject("Txt", typeof(RectTransform), typeof(TextMeshProUGUI));
        lgo.transform.SetParent(card.transform, false);
        var lr = lgo.GetComponent<RectTransform>();
        lr.anchorMin = new Vector2(0, 0.38f); lr.anchorMax = Vector2.one;
        lr.offsetMin = new Vector2(textX, 0); lr.offsetMax = Vector2.zero;
        var lt = lgo.GetComponent<TextMeshProUGUI>();
        lt.text = $"{abbr}   {pct:F1}%";
        lt.fontSize = 13f; lt.fontStyle = FontStyles.Bold;
        lt.color = valCol; lt.alignment = TextAlignmentOptions.Center;

        // Description (lower ~38%)
        var dgo = new GameObject("Desc", typeof(RectTransform), typeof(TextMeshProUGUI));
        dgo.transform.SetParent(card.transform, false);
        var dr = dgo.GetComponent<RectTransform>();
        dr.anchorMin = Vector2.zero; dr.anchorMax = new Vector2(1, 0.38f);
        dr.offsetMin = new Vector2(textX, 2); dr.offsetMax = new Vector2(-4, 0);
        var dt = dgo.GetComponent<TextMeshProUGUI>();
        dt.text = desc; dt.fontSize = 10f;
        dt.color = new Color(0.65f, 0.65f, 0.65f, 1f);
        dt.alignment = TextAlignmentOptions.Center;
    }

    // Row of two half-width cards
    private void MakeCardRow(string abbr1, float pct1, string desc1,
                              string abbr2, float pct2, string desc2,
                              ref float y, float h)
    {
        var row = new GameObject("CR", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(transform, false);
        var rr = row.GetComponent<RectTransform>();
        rr.anchorMin = new Vector2(0,1); rr.anchorMax = new Vector2(1,1);
        rr.pivot = new Vector2(0.5f,1); rr.sizeDelta = new Vector2(-20, h);
        rr.anchoredPosition = new Vector2(0, y); y -= (h + 3f);
        var hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.childControlHeight = true; hlg.childForceExpandHeight = true;
        hlg.childForceExpandWidth = false; hlg.spacing = 4f;

        MakeHalfCard(row, abbr1, pct1, desc1);
        MakeHalfCard(row, abbr2, pct2, desc2);
    }

    // Individual half-width card (horizontal: icon left | abbr / desc / value right)
    private void MakeHalfCard(GameObject row, string abbr, float pct, string desc)
    {
        Color valCol = pct >= 80f ? ColGreen : pct >= 60f ? ColYellow : pct >= 40f ? ColOrange : ColRed;

        var card = new GameObject("C_" + abbr, typeof(RectTransform), typeof(Image));
        card.transform.SetParent(row.transform, false);
        card.AddComponent<LayoutElement>().flexibleWidth = 1;
        var hcImg = card.GetComponent<Image>();
        hcImg.sprite = _roundedCard; hcImg.type = Image.Type.Sliced;
        hcImg.color = new Color(0.15f, 0.17f, 0.20f, 1f);

        // Left colour stripe
        var stripe = new GameObject("S", typeof(RectTransform), typeof(Image));
        stripe.transform.SetParent(card.transform, false);
        var sr = stripe.GetComponent<RectTransform>();
        sr.anchorMin = Vector2.zero; sr.anchorMax = new Vector2(0, 1);
        sr.pivot = Vector2.zero; sr.sizeDelta = new Vector2(4, 0);
        stripe.GetComponent<Image>().color = valCol;

        // Icon: face sprite (green or orange) with coloured circle fallback
        float labelX = 50f;
        {
            var ico = new GameObject("Ico", typeof(RectTransform), typeof(Image));
            ico.transform.SetParent(card.transform, false);
            var ir = ico.GetComponent<RectTransform>();
            ir.anchorMin = new Vector2(0, 0.5f); ir.anchorMax = new Vector2(0, 0.5f);
            ir.pivot = new Vector2(0, 0.5f); ir.sizeDelta = new Vector2(36, 36);
            ir.anchoredPosition = new Vector2(8, 0);
            var ii = ico.GetComponent<Image>();
            var face = FaceFor(pct);
            if (face != null) { ii.sprite = face; ii.color = Color.white; }
            else { ii.sprite = _circleSprite; ii.color = new Color(valCol.r, valCol.g, valCol.b, 0.85f); }
            ii.preserveAspect = true;
        }

        // Abbr (top third)
        var lgo = new GameObject("L", typeof(RectTransform), typeof(TextMeshProUGUI));
        lgo.transform.SetParent(card.transform, false);
        var lr = lgo.GetComponent<RectTransform>();
        lr.anchorMin = new Vector2(0, 0.64f); lr.anchorMax = new Vector2(1, 1f);
        lr.offsetMin = new Vector2(labelX, 1); lr.offsetMax = new Vector2(-4, -2);
        var lt = lgo.GetComponent<TextMeshProUGUI>();
        lt.text = abbr; lt.fontSize = 11f;
        lt.color = new Color(0.75f, 0.75f, 0.75f, 1f);
        lt.alignment = TextAlignmentOptions.BottomLeft;

        // Description (middle third)
        var dgo = new GameObject("D", typeof(RectTransform), typeof(TextMeshProUGUI));
        dgo.transform.SetParent(card.transform, false);
        var dr = dgo.GetComponent<RectTransform>();
        dr.anchorMin = new Vector2(0, 0.33f); dr.anchorMax = new Vector2(1, 0.64f);
        dr.offsetMin = new Vector2(labelX, 0); dr.offsetMax = new Vector2(-4, 0);
        var dt = dgo.GetComponent<TextMeshProUGUI>();
        dt.text = desc; dt.fontSize = 9.5f;
        dt.color = new Color(0.55f, 0.55f, 0.55f, 1f);
        dt.alignment = TextAlignmentOptions.Left;

        // Value (bottom third)
        var vgo = new GameObject("V", typeof(RectTransform), typeof(TextMeshProUGUI));
        vgo.transform.SetParent(card.transform, false);
        var vr = vgo.GetComponent<RectTransform>();
        vr.anchorMin = new Vector2(0, 0); vr.anchorMax = new Vector2(1, 0.33f);
        vr.offsetMin = new Vector2(labelX, 1); vr.offsetMax = new Vector2(-4, 0);
        var vt = vgo.GetComponent<TextMeshProUGUI>();
        vt.text = $"{pct:F1}%"; vt.fontSize = 13f; vt.fontStyle = FontStyles.Bold;
        vt.color = valCol; vt.alignment = TextAlignmentOptions.TopLeft;
    }

    private Sprite FaceFor(float pct) =>
        pct >= 60f ? faceIconGreen : faceIconOrange;

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

    private static Sprite MakeCircleSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float cx = size * 0.5f, r = size * 0.5f - 1f;
        for (int py = 0; py < size; py++)
            for (int px = 0; px < size; px++)
            {
                float dx = px - cx, dy = py - cx;
                float alpha = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                tex.SetPixel(px, py, new Color(1f, 1f, 1f, alpha));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Color HexColor(string hex)
    { ColorUtility.TryParseHtmlString(hex, out Color c); return c; }
}
