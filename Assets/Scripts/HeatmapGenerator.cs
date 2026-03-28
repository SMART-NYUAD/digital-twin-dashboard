using UnityEngine;

public class HeatmapGenerator : MonoBehaviour
{
    private int x_size;
    private int y_size;
    private float constantAlpha;
    private Vector2[] sensorCoordinates;
    private string interpolationMethod;

    public void Initialize(int xSize, int ySize, float alpha, Vector2[] coordinates, string method = "linear")
    {
        x_size = xSize;
        y_size = ySize;
        constantAlpha = alpha;
        sensorCoordinates = coordinates;
        interpolationMethod = method;
    }

    public void SetSensorCoordinates(Vector2[] coordinates)
    {
        sensorCoordinates = coordinates;
    }

    public void SetInterpolationMethod(string method)
    {
        interpolationMethod = method;
    }

    public Gradient GenerateGradient()
    {
        var heatmapGradient = new Gradient();
        GradientColorKey[] colorKeys = new GradientColorKey[5];
        colorKeys[0].color = Color.blue;
        colorKeys[0].time = 0.0f;
        colorKeys[1].color = Color.cyan;
        colorKeys[1].time = 0.25f;
        colorKeys[2].color = Color.green;
        colorKeys[2].time = 0.5f;
        colorKeys[3].color = Color.yellow;
        colorKeys[3].time = 0.75f;
        colorKeys[4].color = Color.red;
        colorKeys[4].time = 1.0f;

        GradientAlphaKey[] alphaKeys = new GradientAlphaKey[5];
        for (int i = 0; i < 5; i++)
        {
            alphaKeys[i].alpha = constantAlpha;
            alphaKeys[i].time = colorKeys[i].time;
        }

        heatmapGradient.SetKeys(colorKeys, alphaKeys);
        return heatmapGradient;
    }

    public Texture2D GenerateHeatmapTexture(float[] data, Gradient gradient, int shellIndex)
    {
        if (sensorCoordinates.Length != data.Length)
        {
            Debug.LogError("The length of sensorCoordinates and data must match.");
            return null;
        }

        var texture = new Texture2D(x_size, y_size);

        float maxData = Mathf.Max(data);
        float minData = Mathf.Min(data);

        if (maxData == minData)
        {
            Debug.LogWarning("Max and min data values are the same. Adjusting maxData to avoid division by zero.");
            maxData = minData + 0.01f;
        }

        //Debug.Log($"Generating heatmap texture. Min data: {minData}, Max data: {maxData}");

        Color[] pixels = new Color[x_size * y_size];
        for (int x = 0; x < x_size; x++)
        {
            for (int y = 0; y < y_size; y++)
            {
                float value = InterpolateData(x, y, data);

                if (float.IsNaN(value))
                {
                    Debug.LogError($"Interpolated value is NaN at pixel ({x}, {y}), setting to 0");
                    value = 0f;
                }

                value = Mathf.Clamp(value, minData, maxData);
                float normalizedValue = Mathf.InverseLerp(minData, maxData, value);

                if (float.IsNaN(normalizedValue) || float.IsInfinity(normalizedValue))
                {
                    Debug.LogError($"Normalized value is invalid: {normalizedValue} at pixel ({x}, {y}), data value: {value}, minData: {minData}, maxData: {maxData}");
                    normalizedValue = 0f; // Set a default value to avoid passing invalid parameter to Gradient.Evaluate
                }

                normalizedValue = Mathf.Clamp01(normalizedValue); // Ensure the time parameter is within [0, 1]
                Color color = gradient.Evaluate(normalizedValue);
                pixels[x + y * x_size] = color;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    float InterpolateData(int x, int y, float[] data)
    {
        float totalWeight = 0;
        float interpolatedValue = 0;

        for (int i = 0; i < data.Length; i++)
        {
            float distance = Vector2.Distance(new Vector2(x, y), sensorCoordinates[i]);

            if (distance == 0)
            {
                // Directly return the data value if the distance is zero
                return data[i];
            }

            float weight = (interpolationMethod == "inverseSquared") ? 1f / Mathf.Pow(distance, 2) : 1f / distance;

            if (float.IsNaN(weight) || float.IsInfinity(weight))
            {
                Debug.LogError($"Weight is invalid: {weight} for sensor at index {i}, distance: {distance}, using 0");
                weight = 0;
            }

            totalWeight += weight;
            interpolatedValue += weight * data[i];
        }

        if (totalWeight == 0)
        {
            Debug.LogError("Total weight is zero, returning 0 to avoid division by zero");
            return 0;
        }

        interpolatedValue /= totalWeight;

        if (float.IsNaN(interpolatedValue) || float.IsInfinity(interpolatedValue))
        {
            Debug.LogError($"Interpolated value is invalid after division: {interpolatedValue}, setting to 0");
            interpolatedValue = 0;
        }

        return interpolatedValue;
    }
}
