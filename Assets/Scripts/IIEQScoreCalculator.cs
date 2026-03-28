// IIEQScoreCalculator.cs
// Static helper – no Unity dependencies.
// Converts raw sensor readings into 0-100 sub-scores via piecewise linear
// normalisation (below-min and above-max clamp to 0; optimum = 100).

public static class IIEQScoreCalculator
{
    // ── Piecewise linear helper ──────────────────────────────────────────
    // Maps:  value ≤ min  → 0
    //        value == opt → 100
    //        value ≥ max  → 0
    // with linear ramps on each side.
    private static float Normalise(float value, float min, float opt, float max)
    {
        if (value <= min || value >= max) return 0f;
        if (value <= opt)
            return (value - min) / (opt - min) * 100f;
        else
            return (max - value) / (max - opt) * 100f;
    }

    // ── Public score methods ─────────────────────────────────────────────

    /// <summary>
    /// Thermal comfort score (0-100).
    /// Optimal: 22-24 °C, 40-60 % RH.
    /// </summary>
    public static float ThermalComfortScore(float tempC, float humidity)
    {
        float tScore = Normalise(tempC,    16f, 23f, 30f);
        float hScore = Normalise(humidity, 20f, 50f, 80f);
        return (tScore + hScore) * 0.5f;
    }

    /// <summary>
    /// Indoor Air Quality score (0-100).
    /// PM2.5 optimal: 0 µg/m³ (lower is better, cap at 75).
    /// CO2 optimal: 400 ppm, cap at 2000 ppm.
    /// </summary>
    public static float IAQScore(float pm25, float co2)
    {
        // For PM2.5 lower is strictly better – treat min=0, opt=0, max=75
        // Use a simple inverse ramp: score = max(0, 100 - pm25/75*100)
        float pmScore  = System.Math.Max(0f, 100f - (pm25 / 75f) * 100f);
        float co2Score = Normalise(co2, 350f, 400f, 2000f);
        return (pmScore + co2Score) * 0.5f;
    }

    /// <summary>
    /// Acoustic comfort score (0-100).
    /// Optimal: 35 dB, comfortable up to 55 dB, intolerable ≥ 85 dB.
    /// </summary>
    public static float AcousticScore(float noiseDB)
    {
        return Normalise(noiseDB, 20f, 35f, 85f);
    }

    /// <summary>
    /// Illumination score (0-100).
    /// Optimal: 500 lux for office work; acceptable 200-1000 lux.
    /// </summary>
    public static float IlluminationScore(float lux)
    {
        return Normalise(lux, 50f, 500f, 2000f);
    }

    // ── Threshold colour helper ──────────────────────────────────────────

    /// <summary>
    /// Returns the colour category for a 0-100 score.
    /// ≥70 → "green", 40-69 → "yellow", &lt;40 → "red".
    /// </summary>
    public static string ScoreCategory(float score)
    {
        if (score >= 70f) return "green";
        if (score >= 40f) return "yellow";
        return "red";
    }
}
