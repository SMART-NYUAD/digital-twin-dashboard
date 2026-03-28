// PrescriptiveFetcher.cs  (rewritten)
// Tier 4 – automated IEQ decision support.
// Displays 3 LLM-style recommendation cards derived from synthesised sensor context.
// Panel: right-side overlay, 370×480 px. "Refresh" rotates to alternate recommendation sets.

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PrescriptiveFetcher : MonoBehaviour
{
    // ── Recommendation sets (rotate on Refresh) ──────────────────────────
    // Each set: 3 cards. Each card: (category, priority, recommendation, action)
    private struct Card { public string Category, Priority, Body, Action; }

    private static readonly Card[][] RecommendationSets =
    {
        new[] {
            new Card {
                Category = "Ventilation & IAQ",
                Priority = "HIGH",
                Body     = "PM2.5 at Node B (12.5 µg/m³) and Node D (11.3 µg/m³) have trended 18% above the lab weekly baseline. CO₂ is approaching 600 ppm during peak occupancy windows.",
                Action   = "Open north-facing windows for 15 min · Activate HEPA purifier (Zone B) · Review HVAC fresh-air damper setting"
            },
            new Card {
                Category = "Thermal Comfort",
                Priority = "MED",
                Body     = "Zone B temperature (24.1 °C) exceeds the optimal 22–23 °C band. Combined with RH of 48.7%, predicted PMV index is +0.6 (slightly warm). Afternoon solar gain expected.",
                Action   = "Lower HVAC set-point to 22.5 °C in Zone B · Deploy west-window solar shading by 14:00 · Monitor RH – target 50–55%"
            },
            new Card {
                Category = "Lighting Optimisation",
                Priority = "LOW",
                Body     = "Node C reports 395 lux – 21% below the 500 lux design target for task-oriented workstations. Three luminaires in that zone are below rated output (lamp age > 18 months).",
                Action   = "Schedule luminaire maintenance for Zone C · Increase dimmer level by 15% as interim fix · Re-assess post-maintenance"
            },
        },
        new[] {
            new Card {
                Category = "Acoustic Comfort",
                Priority = "MED",
                Body     = "Node B noise level (41.2 dB) is close to the 45 dB discomfort threshold. Peak noise events are correlated with corridor HVAC diffuser. VOC (162 ppb) may indicate off-gassing from newly installed furniture.",
                Action   = "Inspect and rebalance corridor HVAC diffuser · Increase ventilation rate for 48 h to purge VOC · Schedule meeting-room bookings away from Zone B until resolved"
            },
            new Card {
                Category = "Occupancy Scheduling",
                Priority = "MED",
                Body     = "IEQ Index drops to 68/100 during 09:00–11:00 peak occupancy (all nodes). PM2.5 spikes correlate with morning commuter activity and entry door proximity.",
                Action   = "Stagger occupancy arrival to 08:30 / 09:30 shifts · Enable pre-cooling 30 min before occupancy · Activate entry foyer air curtain"
            },
            new Card {
                Category = "Energy Efficiency",
                Priority = "LOW",
                Body     = "Lighting in Zone A (485 lux) and Zone B (520 lux) exceeds design target by 5–10% during daylight hours. Automated daylight harvesting is not active.",
                Action   = "Enable daylight-linked dimming on Zone A & B luminaires · Estimated 12% lighting energy saving · Recheck lux levels at 10:00 and 15:00"
            },
        },
        new[] {
            new Card {
                Category = "Predictive Maintenance",
                Priority = "HIGH",
                Body     = "HVAC filter pressure-drop proxy (noise + CO₂ rise) suggests filter loading above 70% capacity. PM2.5 baseline has risen 2.1 µg/m³ over the past month – consistent with reduced filtration efficiency.",
                Action   = "Schedule HVAC filter replacement within 5 working days · Log baseline PM2.5 post-replacement · Set 30-day filter inspection reminder"
            },
            new Card {
                Category = "Thermal Comfort",
                Priority = "MED",
                Body     = "Node C (22.8 °C, RH 55.1%) is within comfort band, but the 1-month standard deviation of 1.2 °C indicates frequent short excursions above 25 °C, likely due to uncontrolled solar gain in the afternoon.",
                Action   = "Install external solar film on south glazing · Add zone temperature sensor feedback to BMS · Target ±0.5 °C stability"
            },
            new Card {
                Category = "IAQ – VOC Reduction",
                Priority = "LOW",
                Body     = "VOC levels (138–162 ppb across all nodes) are within acceptable limits but 35% above outdoor baseline. Source analysis points to cleaning products used Wednesday evenings.",
                Action   = "Switch to low-VOC cleaning products · Increase night-time ventilation rate to 3 ACH on Wednesdays · Re-test one week post-change"
            },
        },
    };

    private static readonly Color ColHigh = HexColor("#F44336");
    private static readonly Color ColMed  = HexColor("#FFC107");
    private static readonly Color ColLow  = HexColor("#4CAF50");

    // Context line (shows synthesised sensor snapshot)
    private const string ContextSummary =
        "T: 23.5°C  RH: 52%  PM2.5: 10.2 µg/m³  VOC: 145 ppb  Lux: 472  dB: 39.2\n" +
        "IEQ Index: 74.2 / 100  ·  Occupancy: 12 / 20  ·  Analysed: just now";

    // ── Runtime ──────────────────────────────────────────────────────────
    private Sprite _chevronDown, _roundedRect, _roundedCard;
    private int _setIdx;
    private GameObject[] _cardRoots = new GameObject[3];
    private TextMeshProUGUI _timestamp;

    private void Start() { _chevronDown = MakeArrowSprite(24, 2); _roundedRect = MakeRoundedRect(64, 6); _roundedCard = MakeRoundedRect(64, 3); BuildPanel(); ShowSet(0); }

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
        AddLabel("PRESCRIPTIVE VIEW", 13f, FontStyles.Bold, ref y, 24f);

        // Subtitle + model dropdown on one line
        var subtitleRow = AddRow("SubtitleRow", ref y, 22f);
        MakeTextCell(subtitleRow, "LLM supported Decision", 11f, FontStyles.Normal,
                     new Color(0.75f,0.75f,0.75f,1f), 0f, true);
        MakeDropdownBtn(subtitleRow, "Llama3-4B");

        y -= 2f; AddSep(ref y);

        // 3 card slots
        for (int i = 0; i < 3; i++)
        {
            _cardRoots[i] = new GameObject($"Card{i}", typeof(RectTransform));
            _cardRoots[i].transform.SetParent(transform, false);
            var cr = _cardRoots[i].GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0,1); cr.anchorMax = new Vector2(1,1);
            cr.pivot = new Vector2(0.5f,1); cr.sizeDelta = new Vector2(-20, 94);
            cr.anchoredPosition = new Vector2(0, y);
            y -= 100f;
        }

        AddSep(ref y);

        // Context block
        AddLabel("Context snapshot:", 10f, FontStyles.Bold, ref y, 16f);
        var ctxGO = new GameObject("Ctx", typeof(RectTransform), typeof(TextMeshProUGUI),
                                   typeof(CanvasRenderer));
        ctxGO.transform.SetParent(transform, false);
        var ctxR = ctxGO.GetComponent<RectTransform>();
        ctxR.anchorMin = new Vector2(0,1); ctxR.anchorMax = new Vector2(1,1);
        ctxR.pivot = new Vector2(0.5f,1); ctxR.sizeDelta = new Vector2(-20,32);
        ctxR.anchoredPosition = new Vector2(0,y); y -= 34f;
        var ctxT = ctxGO.GetComponent<TextMeshProUGUI>();
        ctxT.text = ContextSummary; ctxT.fontSize = 10f;
        ctxT.color = new Color(0.65f,0.65f,0.65f,1f);
        ctxT.alignment = TextAlignmentOptions.Left; ctxT.enableWordWrapping = true;

        // Refresh button
        y -= 2f;
        var btnRow = AddRow("BtnRow", ref y, 30f);
        var refBtn = AddBtnInRow(btnRow, "Refresh Recommendations", 220f, OnRefresh);
        refBtn.GetComponent<Image>().color = HexColor("#2A5A8A");
    }

    // ── Populate cards ────────────────────────────────────────────────────
    private void ShowSet(int setIdx)
    {
        var set = RecommendationSets[setIdx % RecommendationSets.Length];
        for (int i = 0; i < 3; i++)
        {
            foreach (Transform child in _cardRoots[i].transform)
                Destroy(child.gameObject);
            BuildCard(_cardRoots[i], set[i]);
        }
    }

    private void BuildCard(GameObject root, Card card)
    {
        var img = root.GetOrAddComponent<Image>();
        img.sprite = _roundedCard; img.type = Image.Type.Sliced;
        img.color = new Color(0.15f, 0.17f, 0.20f, 1f);

        // Priority stripe (left edge)
        var stripe = new GameObject("Stripe", typeof(RectTransform), typeof(Image),
                                    typeof(CanvasRenderer));
        stripe.transform.SetParent(root.transform, false);
        var sr = stripe.GetComponent<RectTransform>();
        sr.anchorMin = new Vector2(0,0); sr.anchorMax = new Vector2(0,1);
        sr.pivot = new Vector2(0,0.5f); sr.sizeDelta = new Vector2(4,0);
        sr.anchoredPosition = Vector2.zero;
        stripe.GetComponent<Image>().color = PriorityColour(card.Priority);

        // Category + priority badge row
        var hdr = new GameObject("Hdr", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        hdr.transform.SetParent(root.transform, false);
        var hdrR = hdr.GetComponent<RectTransform>();
        hdrR.anchorMin = new Vector2(0,1); hdrR.anchorMax = new Vector2(1,1);
        hdrR.pivot = new Vector2(0.5f,1); hdrR.sizeDelta = new Vector2(-10,22);
        hdrR.anchoredPosition = new Vector2(6,-4);
        var hlg = hdr.GetComponent<HorizontalLayoutGroup>();
        hlg.childControlHeight = true; hlg.childForceExpandWidth = false;
        hlg.spacing = 6f; hlg.childAlignment = TextAnchor.MiddleLeft;

        MakeTextCell(hdr, card.Category, 12f, FontStyles.Bold, Color.white, 0f, true);
        MakeBadge(hdr, card.Priority, PriorityColour(card.Priority));

        // Body text
        var bodyGO = new GameObject("Body", typeof(RectTransform), typeof(TextMeshProUGUI),
                                    typeof(CanvasRenderer));
        bodyGO.transform.SetParent(root.transform, false);
        var bR = bodyGO.GetComponent<RectTransform>();
        bR.anchorMin = new Vector2(0,1); bR.anchorMax = new Vector2(1,1);
        bR.pivot = new Vector2(0.5f,1); bR.sizeDelta = new Vector2(-14,38);
        bR.anchoredPosition = new Vector2(6,-26);
        var bT = bodyGO.GetComponent<TextMeshProUGUI>();
        bT.text = card.Body; bT.fontSize = 10f; bT.color = new Color(0.8f,0.8f,0.8f,1f);
        bT.alignment = TextAlignmentOptions.TopLeft; bT.enableWordWrapping = true;

        // Action text
        var actGO = new GameObject("Act", typeof(RectTransform), typeof(TextMeshProUGUI),
                                   typeof(CanvasRenderer));
        actGO.transform.SetParent(root.transform, false);
        var aR = actGO.GetComponent<RectTransform>();
        aR.anchorMin = new Vector2(0,0); aR.anchorMax = new Vector2(1,0);
        aR.pivot = new Vector2(0.5f,0); aR.sizeDelta = new Vector2(-14,28);
        aR.anchoredPosition = new Vector2(6,4);
        var aT = actGO.GetComponent<TextMeshProUGUI>();
        aT.text = "> " + card.Action; aT.fontSize = 10f;
        aT.color = HexColor("#7EC8F8"); aT.alignment = TextAlignmentOptions.TopLeft;
        aT.enableWordWrapping = true;
    }

    private void OnRefresh()
    {
        _setIdx = (_setIdx + 1) % RecommendationSets.Length;
        ShowSet(_setIdx);
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private Color PriorityColour(string p) =>
        p == "HIGH" ? ColHigh : p == "MED" ? ColMed : ColLow;

    private void MakeTextCell(GameObject row, string txt, float sz, FontStyles fs,
                              Color col, float w, bool flex)
    {
        var go = new GameObject("T", typeof(RectTransform), typeof(TextMeshProUGUI),
                                typeof(CanvasRenderer));
        go.transform.SetParent(row.transform, false);
        var le = go.AddComponent<LayoutElement>();
        if (flex) le.flexibleWidth = 1; else { le.preferredWidth = w; le.flexibleWidth = 0; }
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = txt; t.fontSize = sz; t.fontStyle = fs; t.color = col;
        t.alignment = TextAlignmentOptions.Left;
    }

    private void MakeBadge(GameObject row, string txt, Color col)
    {
        var go = new GameObject("Badge", typeof(RectTransform), typeof(Image),
                                typeof(CanvasRenderer));
        go.transform.SetParent(row.transform, false);
        var le = go.AddComponent<LayoutElement>(); le.preferredWidth = 36f; le.flexibleWidth = 0;
        go.GetComponent<Image>().color = new Color(col.r, col.g, col.b, 0.35f);
        var tgo = new GameObject("L", typeof(RectTransform), typeof(TextMeshProUGUI),
                                 typeof(CanvasRenderer));
        tgo.transform.SetParent(go.transform, false);
        var tr = tgo.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.sizeDelta = Vector2.zero;
        var t = tgo.GetComponent<TextMeshProUGUI>();
        t.text = txt; t.fontSize = 10f; t.fontStyle = FontStyles.Bold;
        t.color = col; t.alignment = TextAlignmentOptions.Center;
    }

    private void AddLabel(string txt, float sz, FontStyles fs, ref float y, float h)
    {
        var go = new GameObject("Lbl", typeof(RectTransform), typeof(TextMeshProUGUI),
                                typeof(CanvasRenderer));
        go.transform.SetParent(transform, false);
        var r = go.GetComponent<RectTransform>();
        r.anchorMin=new Vector2(0,1); r.anchorMax=new Vector2(1,1);
        r.pivot=new Vector2(0.5f,1); r.sizeDelta=new Vector2(-20,h);
        r.anchoredPosition=new Vector2(0,y); y-=h;
        var t=go.GetComponent<TextMeshProUGUI>();
        t.text=txt; t.fontSize=sz; t.fontStyle=fs; t.color=Color.white;
        t.alignment=TextAlignmentOptions.Left;
    }

    private void AddSep(ref float y)
    {
        var go=new GameObject("Sep",typeof(RectTransform),typeof(Image));
        go.transform.SetParent(transform,false);
        var r=go.GetComponent<RectTransform>();
        r.anchorMin=new Vector2(0,1); r.anchorMax=new Vector2(1,1);
        r.pivot=new Vector2(0.5f,1); r.sizeDelta=new Vector2(-20,1);
        r.anchoredPosition=new Vector2(0,y); y-=5f;
        go.GetComponent<Image>().color=new Color(1,1,1,0.15f);
    }

    private GameObject AddRow(string name, ref float y, float h)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(HorizontalLayoutGroup));
        go.transform.SetParent(transform,false);
        var r=go.GetComponent<RectTransform>();
        r.anchorMin=new Vector2(0,1); r.anchorMax=new Vector2(1,1);
        r.pivot=new Vector2(0.5f,1); r.sizeDelta=new Vector2(-20,h);
        r.anchoredPosition=new Vector2(0,y); y-=(h+4f);
        var hlg=go.GetComponent<HorizontalLayoutGroup>();
        hlg.childControlHeight=true; hlg.childForceExpandWidth=false;
        hlg.spacing=6f; hlg.childAlignment=TextAnchor.MiddleLeft;
        return go;
    }

    private Button AddBtnInRow(GameObject row, string lbl, float w, System.Action onClick)
    {
        var go=new GameObject("B",typeof(RectTransform),typeof(Image),typeof(Button));
        go.transform.SetParent(row.transform,false);
        var le=go.AddComponent<LayoutElement>(); le.preferredWidth=w; le.flexibleWidth=0;
        var refImg=go.GetComponent<Image>();
        refImg.sprite=_roundedCard; refImg.type=Image.Type.Sliced;
        refImg.color=HexColor("#3389F2");
        var btn=go.GetComponent<Button>(); btn.onClick.AddListener(()=>onClick());
        var tgo=new GameObject("L",typeof(RectTransform),typeof(TextMeshProUGUI));
        tgo.transform.SetParent(go.transform,false);
        var tr=tgo.GetComponent<RectTransform>();
        tr.anchorMin=Vector2.zero; tr.anchorMax=Vector2.one; tr.sizeDelta=Vector2.zero;
        var t=tgo.GetComponent<TextMeshProUGUI>();
        t.text=lbl; t.fontSize=11f; t.color=Color.white; t.alignment=TextAlignmentOptions.Center;
        return btn;
    }

    private void MakeDropdownBtn(GameObject row, string label)
    {
        // Outer button (looks like a dropdown)
        var go = new GameObject("DD", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(row.transform, false);
        var le = go.AddComponent<LayoutElement>(); le.preferredWidth = 130f; le.flexibleWidth = 0;
        var img = go.GetComponent<Image>();
        img.sprite = _roundedCard; img.type = Image.Type.Sliced;
        img.color = new Color(0.20f, 0.22f, 0.26f, 1f);

        // Outline via child image border (simple approach: slightly lighter bg rect inset)
        var outline = new GameObject("OL", typeof(RectTransform), typeof(Image));
        outline.transform.SetParent(go.transform, false);
        var olr = outline.GetComponent<RectTransform>();
        olr.anchorMin = Vector2.zero; olr.anchorMax = Vector2.one;
        olr.offsetMin = new Vector2(0,0); olr.offsetMax = Vector2.zero;
        outline.GetComponent<Image>().color = new Color(0.40f, 0.42f, 0.48f, 0.5f);

        // Label text (left-aligned, inside)
        var tgo = new GameObject("Lbl", typeof(RectTransform), typeof(TextMeshProUGUI));
        tgo.transform.SetParent(go.transform, false);
        var tr = tgo.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero; tr.anchorMax = new Vector2(0.78f, 1f);
        tr.offsetMin = new Vector2(8, 0); tr.offsetMax = Vector2.zero;
        var t = tgo.GetComponent<TextMeshProUGUI>();
        t.text = label; t.fontSize = 11f; t.color = Color.white;
        t.alignment = TextAlignmentOptions.Left;

        // Chevron icon (right side)
        var cgo = new GameObject("Chv", typeof(RectTransform), typeof(Image));
        cgo.transform.SetParent(go.transform, false);
        var cr = cgo.GetComponent<RectTransform>();
        cr.anchorMin = new Vector2(0.78f, 0.2f); cr.anchorMax = new Vector2(1f, 0.8f);
        cr.offsetMin = Vector2.zero; cr.offsetMax = new Vector2(-5, 0);
        var ci = cgo.GetComponent<Image>();
        ci.sprite = _chevronDown; ci.color = new Color(0.7f, 0.7f, 0.7f, 1f);
        ci.preserveAspect = true;
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

    // dir: 0 = right (>), 1 = left (<), 2 = down (v)
    private static Sprite MakeArrowSprite(int size, int dir)
    {
        float s = size;
        Vector2 a, b, c;
        if      (dir == 0) { a=new Vector2(0.78f*s,0.50f*s); b=new Vector2(0.22f*s,0.15f*s); c=new Vector2(0.22f*s,0.85f*s); }
        else if (dir == 1) { a=new Vector2(0.22f*s,0.50f*s); b=new Vector2(0.78f*s,0.15f*s); c=new Vector2(0.78f*s,0.85f*s); }
        else               { a=new Vector2(0.50f*s,0.82f*s); b=new Vector2(0.15f*s,0.22f*s); c=new Vector2(0.85f*s,0.22f*s); }
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int py = 0; py < size; py++)
            for (int px = 0; px < size; px++)
            {
                float d1=(px-b.x)*(a.y-b.y)-(a.x-b.x)*(py-b.y);
                float d2=(px-c.x)*(b.y-c.y)-(b.x-c.x)*(py-c.y);
                float d3=(px-a.x)*(c.y-a.y)-(c.x-a.x)*(py-a.y);
                bool inside=(d1>=0&&d2>=0&&d3>=0)||(d1<=0&&d2<=0&&d3<=0);
                tex.SetPixel(px, py, inside ? Color.white : Color.clear);
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0,0,size,size), new Vector2(0.5f,0.5f));
    }

    private static Color HexColor(string hex)
    { ColorUtility.TryParseHtmlString(hex, out Color c); return c; }
}
