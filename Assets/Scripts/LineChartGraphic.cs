// LineChartGraphic.cs
// Custom Unity UI Graphic that draws a filled line chart from a float[] dataset.
// Add this component to any UI GameObject. Set Data and call SetDirty() to refresh.

using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class LineChartGraphic : MaskableGraphic
{
    public float[] Data;
    public float RefLine       = -1f;   // draw a horizontal reference line if >= 0
    public Color  LineColor    = new Color(0.35f, 0.75f, 1.00f, 1f);
    public Color  FillColor    = new Color(0.35f, 0.75f, 1.00f, 0.18f);
    public Color  RefLineColor = new Color(1f, 0.85f, 0.2f, 0.85f);
    public float  LineWidth    = 2.5f;

    // Padding inside rect (left, right, bottom, top)
    public Vector4 Padding = new Vector4(30f, 10f, 20f, 10f);

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (Data == null || Data.Length < 2) return;

        var   r  = rectTransform.rect;
        float W  = r.width;
        float H  = r.height;
        float ox = r.xMin;   // respects pivot, works for any pivot setting
        float oy = r.yMin;

        float plotX0 = ox + Padding.x;
        float plotX1 = ox + W - Padding.y;
        float plotY0 = oy + Padding.z;
        float plotY1 = oy + H - Padding.w;
        float plotW  = plotX1 - plotX0;
        float plotH  = plotY1 - plotY0;

        float minV = float.MaxValue, maxV = float.MinValue;
        foreach (var v in Data) { if (v < minV) minV = v; if (v > maxV) maxV = v; }
        float range = maxV - minV; if (range < 0.001f) range = 1f;
        // Add 10% headroom
        float displayMin = minV - range * 0.05f;
        float displayMax = maxV + range * 0.15f;
        float displayRange = displayMax - displayMin;

        System.Func<int, Vector2> Pt = i =>
        {
            float px = plotX0 + (float)i / (Data.Length - 1) * plotW;
            float py = plotY0 + (Data[i] - displayMin) / displayRange * plotH;
            return new Vector2(px, py);
        };

        // ── Fill area ────────────────────────────────────────────────────
        for (int i = 0; i < Data.Length - 1; i++)
        {
            var p0 = Pt(i); var p1 = Pt(i + 1);
            int b = vh.currentVertCount;
            AddVert(vh, new Vector2(p0.x, plotY0), FillColor);
            AddVert(vh, p0,                         FillColor);
            AddVert(vh, p1,                         FillColor);
            AddVert(vh, new Vector2(p1.x, plotY0), FillColor);
            vh.AddTriangle(b, b+1, b+2); vh.AddTriangle(b, b+2, b+3);
        }

        // ── Reference line ────────────────────────────────────────────────
        if (RefLine >= 0f)
        {
            float ry = plotY0 + (RefLine - displayMin) / displayRange * plotH;
            DrawHLine(vh, plotX0, plotX1, ry, RefLineColor, 1.5f);
        }

        // ── Axis lines ───────────────────────────────────────────────────
        Color axCol = new Color(1,1,1,0.2f);
        DrawHLine(vh, plotX0, plotX1, plotY0,         axCol, 1f);
        DrawVLine(vh, plotX0, plotY0, plotY1,         axCol, 1f);

        // ── Line ─────────────────────────────────────────────────────────
        for (int i = 0; i < Data.Length - 1; i++)
            DrawSegment(vh, Pt(i), Pt(i + 1), LineColor, LineWidth);
    }

    private static void DrawSegment(VertexHelper vh, Vector2 a, Vector2 b, Color col, float w)
    {
        Vector2 dir  = (b - a).normalized;
        Vector2 perp = new Vector2(-dir.y, dir.x) * w * 0.5f;
        int idx = vh.currentVertCount;
        AddVert(vh, a - perp, col); AddVert(vh, a + perp, col);
        AddVert(vh, b + perp, col); AddVert(vh, b - perp, col);
        vh.AddTriangle(idx, idx+1, idx+2); vh.AddTriangle(idx, idx+2, idx+3);
    }

    private static void DrawHLine(VertexHelper vh, float x0, float x1, float y, Color col, float w)
        => DrawSegment(vh, new Vector2(x0, y), new Vector2(x1, y), col, w);

    private static void DrawVLine(VertexHelper vh, float x, float y0, float y1, Color col, float w)
        => DrawSegment(vh, new Vector2(x, y0), new Vector2(x, y1), col, w);

    private static void AddVert(VertexHelper vh, Vector2 pos, Color col)
    {
        vh.AddVert(new UIVertex { position = pos, color = col, uv0 = Vector2.zero });
    }
}
