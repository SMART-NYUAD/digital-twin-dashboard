using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

public class HeatmapFunctions : MonoBehaviour
{
    public Vector2[] SwapSensorCoordinates(Vector2[] originalCoordinates)
    {
        Vector2[] swappedCoordinates = new Vector2[originalCoordinates.Length];
        for (int i = 0; i < originalCoordinates.Length; i++)
        {
            swappedCoordinates[i] = new Vector2(originalCoordinates[i].y, 100 - originalCoordinates[i].x);
        }
        return swappedCoordinates;
    }

    public string[] GetCurrentEntityIds(string currentProperty, string[] temperatureEntityIds, string[] humidityEntityIds, string[] pressureEntityIds, string[] pm25EntityIds)
    {
        switch (currentProperty)
        {
            case "Humidity":
                return humidityEntityIds;
            case "Pressure":
                return pressureEntityIds;
            case "PM2.5":
                return pm25EntityIds;
            case "Temperature":
            default:
                return temperatureEntityIds;
        }
    }

    public float[] GetCurrentPropertyData(string currentProperty, float[] temperatures, float[] humidity, float[] pressure, float[] pm25)
    {
        switch (currentProperty)
        {
            case "Humidity":
                return humidity;
            case "Pressure":
                return pressure;
            case "PM2.5":
                return pm25;
            case "Temperature":
            default:
                return temperatures;
        }
    }

    public Vector2[] GetCurrentCoordinates(string currentProperty, Vector2[] temperatureCoordinates, Vector2[] humidityCoordinates, Vector2[] pressureCoordinates, Vector2[] pm25Coordinates)
    {
        switch (currentProperty)
        {
            case "Humidity":
                return humidityCoordinates;
            case "Pressure":
                return pressureCoordinates;
            case "PM2.5":
                return pm25Coordinates;
            case "Temperature":
            default:
                return temperatureCoordinates;
        }
    }

    public string GetUnit(string currentProperty)
    {
        switch (currentProperty)
        {
            case "Humidity":
                return "%";
            case "Pressure":
                return " hPa";
            case "PM2.5":
                return " µg/m³";
            case "Temperature":
            default:
                return "°C";
        }
    }

    public string GetInterpolationMethod(string currentProperty)
    {
        switch (currentProperty)
        {
            case "Humidity":
                return "linear";
            case "Pressure":
            case "PM2.5":
            case "Temperature":
                return "inverseSquared";
            default:
                return "linear";
        }
    }

    public void InitializeHeatmap(GameObject[] shells, GameObject plane_prefab, Transform parentTransform, HeatmapGenerator heatmapGenerator, Gradient heatmapGradient, float[] temperatures, int num_shells, float height)
    {
        for (int i = 0; i < num_shells; i++)
        {
            var shell = Instantiate(plane_prefab);
            shell.transform.parent = parentTransform;
            shell.transform.position += new Vector3(0, (float)i / num_shells * height, 0);

            var texture = heatmapGenerator.GenerateHeatmapTexture(temperatures, heatmapGradient, i);
            shell.GetComponent<MeshRenderer>().material.SetTexture("_MainTex", texture);

            shells[i] = shell;
        }
    }

    public IEnumerator PeriodicUpdate(string apiUrl, string apiToken, string currentProperty, SensorDataFetcher sensordataFetcher, string[] temperatureEntityIds, string[] humidityEntityIds, string[] pressureEntityIds, string[] pm25EntityIds, float[] temperatures, float[] humidity, float[] pressure, float[] pm25, Dictionary<string, float[]> cachedData, HeatmapGenerator heatmapGenerator, Gradient heatmapGradient, int num_shells, GameObject[] shells, float refreshInterval)
    {
        while (true)
        {
            yield return new WaitForSeconds(refreshInterval);

            // Add small random variations to simulate live sensor readings
            for (int i = 0; i < temperatures.Length; i++)
                temperatures[i] += UnityEngine.Random.Range(-0.2f, 0.2f);
            for (int i = 0; i < humidity.Length; i++)
                humidity[i] += UnityEngine.Random.Range(-0.5f, 0.5f);
            for (int i = 0; i < pressure.Length; i++)
                pressure[i] += UnityEngine.Random.Range(-0.1f, 0.1f);
            for (int i = 0; i < pm25.Length; i++)
                pm25[i] += UnityEngine.Random.Range(-0.3f, 0.3f);

            float[] data = GetCurrentPropertyData(currentProperty, temperatures, humidity, pressure, pm25);
            CacheData(currentProperty, data, cachedData);
            UpdateHeatmap(data, heatmapGenerator, heatmapGradient, num_shells, shells);
        }
    }

    public void CacheData(string property, float[] data, Dictionary<string, float[]> cachedData)
    {
        if (!cachedData.ContainsKey(property))
        {
            cachedData[property] = new float[data.Length];
        }
        data.CopyTo(cachedData[property], 0);
    }

    public void UpdateHeatmap(float[] data, HeatmapGenerator heatmapGenerator, Gradient heatmapGradient, int num_shells, GameObject[] shells)
    {
        for (int i = 0; i < num_shells; i++)
        {
            var texture = heatmapGenerator.GenerateHeatmapTexture(data, heatmapGradient, i);
            shells[i].GetComponent<MeshRenderer>().material.SetTexture("_MainTex", texture);
        }
    }

    public void UpdateColorBar(RawImage colorBar, Text minTemperatureText, Text maxTemperatureText, Text middleTemperatureText, float[] data, Gradient heatmapGradient, string unit)
    {
        if (colorBar == null) return;

        Texture2D texture = new Texture2D(1, 256);
        for (int y = 0; y < 256; y++)
        {
            float t = y / 255.0f;
            Color color = heatmapGradient.Evaluate(t);
            color.a = 1.0f; // Ensure alpha is always 1
            texture.SetPixel(0, y, color);
        }
        texture.Apply();

        colorBar.texture = texture;

        // Update the temperature text
        float minTemp = Mathf.Min(data);
        float maxTemp = Mathf.Max(data);
        float middleTemp = (minTemp + maxTemp) / 2.0f;

        if (minTemperatureText != null)
        {
            minTemperatureText.text = minTemp.ToString("F1") + unit;
        }

        if (maxTemperatureText != null)
        {
            maxTemperatureText.text = maxTemp.ToString("F1") + unit;
        }

        if (middleTemperatureText != null)
        {
            middleTemperatureText.text = middleTemp.ToString("F1") + unit;
        }
    }
}
