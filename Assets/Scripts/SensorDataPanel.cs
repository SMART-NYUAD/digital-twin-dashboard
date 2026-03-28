// SensorDataPanel.cs
// Attach to a panel GameObject under Canvas. Shows one sensor's live readings at a time.
// Switch between 4 dummy sensor nodes with the ◀ / ▶ buttons.
//
// SETUP: Create "SensorDataPanel" under Canvas → attach this script
//        → assign it to ViewManager's sensorDataPanel field.

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SensorDataPanel : MonoBehaviour
{
    // ── Dummy sensor data [sensor][param] ────────────────────────────────
    private static readonly string[] SensorNames = { "Sensor 1", "Sensor 2", "Sensor 3", "Sensor 4" };

    // Params: Temp(°C), Humidity(%), PM2.5(µg/m³), VOC(ppb), Lighting(lux), Noise(dB)
    private static readonly float[,] Data =
    {
        { 23.5f, 52.3f, 10.2f, 145f, 485f, 38.5f },
        { 24.1f, 48.7f, 12.5f, 162f, 520f, 41.2f },
        { 22.8f, 55.1f,  8.7f, 138f, 395f, 35.8f },
        { 23.9f, 50.2f, 11.3f, 155f, 462f, 40.1f },
    };

    private static readonly string[] ParamNames  = { "Temperature", "Humidity", "PM2.5", "VOC", "Lighting", "Noise" };
    private static readonly string[] Units        = { "°C", "%", "µg/m³", "ppb", "lux", "dB" };
    // Comfortable thresholds (min, ok, warn) for colour coding
    private static readonly float[,] Thresholds  =
    {
        { 19f, 25f, 28f },  // Temp
        { 30f, 60f, 75f },  // Humidity
        {  0f, 15f, 35f },  // PM2.5 (lower=better, invert logic)
        {  0f,250f,500f },  // VOC
        {300f,600f,900f },  // Lighting
        { 25f, 45f, 60f },  // Noise
    };
    private static readonly bool[] LowerBetter = { false, false, true, true, false, true };

    private static readonly Color ColGood   = HexColor("#4CAF50");
    private static readonly Color ColWarn   = HexColor("#FFC107");
    private static readonly Color ColBad    = HexColor("#F44336");
    private static readonly Color ColBtnBg  = HexColor("#3A3A3A");

    // ── Runtime ──────────────────────────────────────────────────────────
    private Sprite _arrowL, _arrowR, _roundedRect, _roundedCard;
    private int _current;
    private TextMeshProUGUI _nodeLabel;
    private TextMeshProUGUI[] _valueTexts = new TextMeshProUGUI[6];
    private Image[]           _valueCards  = new Image[6];
    private TextMeshProUGUI   _statusTimeText;

    // ── Lifecycle ────────────────────────────────────────────────────────
    private void Start()
    {
        _arrowL = MakeArrowSprite(32, 1);
        _arrowR = MakeArrowSprite(32, 0);
        _roundedRect = MakeRoundedRect(64, 6);
        _roundedCard = MakeRoundedRect(64, 3);
        BuildPanel();
        Refresh();
    }

    private void Update()
    {
        if (_statusTimeText != null)
            _statusTimeText.text = $"Updated: {System.DateTime.Now:HH:mm:ss}";
    }

    // ── Build ────────────────────────────────────────────────────────────
    private void BuildPanel()
    {
        var rt = gameObject.GetOrAddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot     = new Vector2(1f, 0.5f);
        rt.sizeDelta = new Vector2(360f, 460f);
        rt.anchoredPosition = new Vector2(-8f, 0f);

        var panelImg = gameObject.GetOrAddComponent<Image>();
        panelImg.sprite = _roundedRect; panelImg.type = Image.Type.Sliced;
        panelImg.color = new Color(0.10f, 0.10f, 0.12f, 0.92f);

        float y = -10f;

        // Title
        MakeText("SENSOR DATA", 15f, FontStyles.Bold, ref y, 26f, TextAlignmentOptions.Center);

        // Node label + prev/next row
        var navRow = MakeRow("NavRow", ref y, 30f);
        var prevBtn = MakeNavBtn(navRow, _arrowL, () => Navigate(-1));
        _nodeLabel = MakeTextInRow(navRow, "", 13f, FontStyles.Bold, 0f, true);
        MakeNavBtn(navRow, _arrowR, () => Navigate(+1));

        // 6 parameter cards in 2 columns
        y -= 4f;
        for (int i = 0; i < 6; i += 2)
        {
            var rowGO = MakeRow($"ParamRow{i}", ref y, 82f);
            MakeParamCard(rowGO, i);
            MakeParamCard(rowGO, i + 1);
        }

        // ── Status block ──────────────────────────────────────────────
        y -= 6f;
        // thin separator
        var sepGO = new GameObject("StatusSep", typeof(RectTransform), typeof(Image));
        sepGO.transform.SetParent(transform, false);
        var sepR = sepGO.GetComponent<RectTransform>();
        sepR.anchorMin = new Vector2(0,1); sepR.anchorMax = new Vector2(1,1);
        sepR.pivot = new Vector2(0.5f,1); sepR.sizeDelta = new Vector2(-20,1);
        sepR.anchoredPosition = new Vector2(0,y); y -= 8f;
        sepGO.GetComponent<Image>().color = new Color(1,1,1,0.15f);

        string[] statusKeys   = { "Updated:", "Sources:", "Alerts:" };
        string[] statusValues = { "--:--:--", "OK", "0" };
        for (int i = 0; i < 3; i++)
        {
            var rowGO = MakeRow($"StatusRow{i}", ref y, 22f);
            var kGO = new GameObject("K", typeof(RectTransform), typeof(TextMeshProUGUI));
            kGO.transform.SetParent(rowGO.transform, false);
            var kLE = kGO.AddComponent<LayoutElement>(); kLE.preferredWidth = 80f;
            var kT = kGO.GetComponent<TextMeshProUGUI>();
            kT.text = statusKeys[i]; kT.fontSize = 12f; kT.fontStyle = FontStyles.Bold;
            kT.color = new Color(0.7f,0.7f,0.7f,1f); kT.alignment = TextAlignmentOptions.Left;

            var vGO = new GameObject("V", typeof(RectTransform), typeof(TextMeshProUGUI));
            vGO.transform.SetParent(rowGO.transform, false);
            var vLE = vGO.AddComponent<LayoutElement>(); vLE.flexibleWidth = 1;
            var vT = vGO.GetComponent<TextMeshProUGUI>();
            vT.text = statusValues[i]; vT.fontSize = 12f;
            vT.color = Color.white; vT.alignment = TextAlignmentOptions.Left;
            if (i == 0) _statusTimeText = vT;
        }
    }

    // ── Navigation ───────────────────────────────────────────────────────
    private void Navigate(int dir)
    {
        _current = (_current + dir + SensorNames.Length) % SensorNames.Length;
        Refresh();
    }

    private void Refresh()
    {
        _nodeLabel.text = SensorNames[_current];
        for (int i = 0; i < 6; i++)
        {
            float val = Data[_current, i];
            _valueTexts[i].text = $"{val:G4} {Units[i]}";
            _valueCards[i].color = CardColour(i, val);
        }
    }

    // ── Colour helper ────────────────────────────────────────────────────
    private static Color CardColour(int paramIdx, float val)
    {
        float lo = Thresholds[paramIdx, 0];
        float ok = Thresholds[paramIdx, 1];
        float hi = Thresholds[paramIdx, 2];
        bool inv = LowerBetter[paramIdx];
        if (!inv)
        {
            if (val < lo || val > hi) return new Color(ColBad.r,  ColBad.g,  ColBad.b,  0.55f);
            if (val > ok)             return new Color(ColWarn.r, ColWarn.g, ColWarn.b, 0.45f);
            return new Color(ColGood.r, ColGood.g, ColGood.b, 0.35f);
        }
        else
        {
            if (val > hi) return new Color(ColBad.r,  ColBad.g,  ColBad.b,  0.55f);
            if (val > ok) return new Color(ColWarn.r, ColWarn.g, ColWarn.b, 0.45f);
            return new Color(ColGood.r, ColGood.g, ColGood.b, 0.35f);
        }
    }

    // ── UI helpers ───────────────────────────────────────────────────────
    private void MakeParamCard(GameObject row, int paramIdx)
    {
        var cardGO = new GameObject($"Card_{paramIdx}", typeof(RectTransform),
                                    typeof(Image));
        cardGO.transform.SetParent(row.transform, false);
        var le = cardGO.AddComponent<LayoutElement>();
        le.flexibleWidth = 1;

        var cImg = cardGO.GetComponent<Image>();
        cImg.sprite = _roundedCard; cImg.type = Image.Type.Sliced;
        _valueCards[paramIdx] = cImg;

        // Name label
        var nameGO = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI),
                                    typeof(CanvasRenderer));
        nameGO.transform.SetParent(cardGO.transform, false);
        var nr = nameGO.GetComponent<RectTransform>();
        nr.anchorMin = new Vector2(0,1); nr.anchorMax = new Vector2(1,1);
        nr.pivot = new Vector2(0.5f,1); nr.sizeDelta = new Vector2(0,26); nr.anchoredPosition = new Vector2(0,-3);
        var nt = nameGO.GetComponent<TextMeshProUGUI>();
        nt.text = ParamNames[paramIdx]; nt.fontSize = 12f; nt.color = new Color(1,1,1,0.7f);
        nt.alignment = TextAlignmentOptions.Center;

        // Value label
        var valGO = new GameObject("Val", typeof(RectTransform), typeof(TextMeshProUGUI),
                                   typeof(CanvasRenderer));
        valGO.transform.SetParent(cardGO.transform, false);
        var vr = valGO.GetComponent<RectTransform>();
        vr.anchorMin = new Vector2(0,0); vr.anchorMax = new Vector2(1,1);
        vr.sizeDelta = new Vector2(0,-22); vr.anchoredPosition = new Vector2(0,-10);
        var vt = valGO.GetComponent<TextMeshProUGUI>();
        vt.fontSize = 14f; vt.color = Color.white; vt.fontStyle = FontStyles.Bold;
        vt.alignment = TextAlignmentOptions.Center;
        _valueTexts[paramIdx] = vt;
    }

    private Button MakeNavBtn(GameObject row, Sprite arrow, System.Action onClick)
    {
        var go = new GameObject("NavBtn", typeof(RectTransform), typeof(Image),
                                typeof(Button));
        go.transform.SetParent(row.transform, false);
        var le = go.AddComponent<LayoutElement>(); le.preferredWidth = 28f; le.flexibleWidth = 0;
        var navImg = go.GetComponent<Image>();
        navImg.sprite = _roundedCard; navImg.type = Image.Type.Sliced;
        navImg.color = ColBtnBg;
        var btn = go.GetComponent<Button>();
        btn.onClick.AddListener(() => onClick());

        // Arrow icon centred inside the button
        var ico = new GameObject("Ico", typeof(RectTransform), typeof(Image));
        ico.transform.SetParent(go.transform, false);
        var ir = ico.GetComponent<RectTransform>();
        ir.anchorMin = new Vector2(0.2f, 0.2f); ir.anchorMax = new Vector2(0.8f, 0.8f);
        ir.sizeDelta = Vector2.zero;
        var ii = ico.GetComponent<Image>();
        ii.sprite = arrow; ii.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        ii.preserveAspect = true;
        return btn;
    }

    private TextMeshProUGUI MakeTextInRow(GameObject row, string text, float size,
                                          FontStyles style, float fixedW, bool flexible)
    {
        var go = new GameObject("Txt", typeof(RectTransform), typeof(TextMeshProUGUI),
                                typeof(CanvasRenderer));
        go.transform.SetParent(row.transform, false);
        var le = go.AddComponent<LayoutElement>();
        if (flexible) le.flexibleWidth = 1; else { le.preferredWidth = fixedW; le.flexibleWidth = 0; }
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.fontStyle = style;
        t.color = Color.white; t.alignment = TextAlignmentOptions.Center;
        return t;
    }

    private void MakeText(string text, float size, FontStyles style, ref float y,
                          float height, TextAlignmentOptions align)
    {
        var go = new GameObject("Lbl", typeof(RectTransform), typeof(TextMeshProUGUI),
                                typeof(CanvasRenderer));
        go.transform.SetParent(transform, false);
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0,1); r.anchorMax = new Vector2(1,1);
        r.pivot = new Vector2(0.5f,1); r.sizeDelta = new Vector2(-20,height);
        r.anchoredPosition = new Vector2(0, y); y -= height;
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.fontStyle = style;
        t.color = Color.white; t.alignment = align;
    }

    private GameObject MakeRow(string name, ref float y, float height)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
        go.transform.SetParent(transform, false);
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0,1); r.anchorMax = new Vector2(1,1);
        r.pivot = new Vector2(0.5f,1); r.sizeDelta = new Vector2(-20,height);
        r.anchoredPosition = new Vector2(0, y); y -= (height + 4f);
        var hlg = go.GetComponent<HorizontalLayoutGroup>();
        hlg.childControlHeight = true; hlg.childForceExpandHeight = true;
        hlg.childForceExpandWidth = false; hlg.spacing = 4f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        return go;
    }

    private TextMeshProUGUI MakeChildText(GameObject parent, string text, float size)
    {
        var go = new GameObject("Lbl", typeof(RectTransform), typeof(TextMeshProUGUI),
                                typeof(CanvasRenderer));
        go.transform.SetParent(parent.transform, false);
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.sizeDelta = Vector2.zero;
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = Color.white;
        t.alignment = TextAlignmentOptions.Center;
        return t;
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
