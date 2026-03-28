using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Reflection;

public class DisplaySensorData : MonoBehaviour
{
    [Serializable]
    public class SensorData
    {
        public string sensorName;
        public string sensorURL;
        public List<PropertyMapping> propertyMappings;
        public TextMeshPro textComponent;
        public ParticleSystem airQualityParticles;
    }

    [Serializable]
    public class PropertyMapping
    {
        public string propertyName;
        public string jsonField;
        public string units;
        public bool isAirQualityProperty;
        public bool isTemperatureProperty;
    }

    public List<SensorData> sensors;
    public Toggle showValuesToggle;
    public Toggle showParticlesToggle;

    private List<Coroutine> fetchDataCoroutines;

    private void Start()
    {
        fetchDataCoroutines = new List<Coroutine>();

        foreach (SensorData sensor in sensors)
        {
            Coroutine fetchDataCoroutine = StartCoroutine(FetchDataRoutine(sensor));
            fetchDataCoroutines.Add(fetchDataCoroutine);

            sensor.textComponent.gameObject.SetActive(showValuesToggle.isOn);
            sensor.textComponent.text = "Fetching data for " + sensor.sensorName + "...";
        }

        showValuesToggle.onValueChanged.AddListener(OnToggleValueChanged);
        showParticlesToggle.onValueChanged.AddListener(OnParticlesToggleValueChanged);
    }

    private void OnDestroy()
    {
        foreach (Coroutine coroutine in fetchDataCoroutines)
        {
            StopCoroutine(coroutine);
        }
    }

    private void OnToggleValueChanged(bool value)
    {
        foreach (SensorData sensor in sensors)
        {
            sensor.textComponent.gameObject.SetActive(value);
        }
    }

    private void OnParticlesToggleValueChanged(bool value)
    {
        foreach (SensorData sensor in sensors)
        {
            if (sensor.airQualityParticles != null)
            {
                var emission = sensor.airQualityParticles.emission;
                emission.enabled = value;
            }
        }
    }

    private IEnumerator FetchDataRoutine(SensorData sensor)
    {
        // Dummy base values per property index: temperature, humidity, PM2.5, CO2, pressure, light
        float[] dummyBaseValues = { 23.5f, 52.3f, 10.2f, 420.0f, 1013.4f, 8.7f };

        while (true)
        {
            sensor.textComponent.gameObject.SetActive(showValuesToggle.isOn);

            try
            {
                StringBuilder stringBuilder = new StringBuilder();
                float airQualityParticleCount = 0;
                int valueIndex = 0;

                foreach (PropertyMapping propertyMapping in sensor.propertyMappings)
                {
                    float baseValue = valueIndex < dummyBaseValues.Length ? dummyBaseValues[valueIndex] : 0f;
                    float value = baseValue + UnityEngine.Random.Range(-0.5f, 0.5f);
                    valueIndex++;

                    float roundedValue = Mathf.Round(value * 10f) / 10f;
                    stringBuilder.AppendLine(propertyMapping.propertyName + ": " +
                        roundedValue.ToString("F1", CultureInfo.InvariantCulture) + " " + propertyMapping.units);

                    if (propertyMapping.isAirQualityProperty)
                        airQualityParticleCount = value;
                }

                if (showValuesToggle.isOn)
                    sensor.textComponent.text = stringBuilder.ToString();

                ParticleSystem particles = sensor.airQualityParticles;
                if (particles != null)
                {
                    var emission = particles.emission;
                    emission.enabled = showParticlesToggle.isOn && airQualityParticleCount > 0;
                    emission.rateOverTime = airQualityParticleCount * 10f;
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Error setting dummy sensor data: " + e.Message);
            }

            yield return new WaitForSeconds(60f);
        }
    }

    private string GetFieldValue(List<FeedData> feeds, string fieldName)
    {
        if (feeds != null && feeds.Count > 0)
        {
            FeedData latestFeed = feeds[feeds.Count - 1];
            var field = typeof(FeedData).GetField(fieldName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                return (string)field.GetValue(latestFeed);
            }
        }
        return null;
    }

    [Serializable]
    private class ResponseData
    {
        public ChannelData channel;
        public List<FeedData> feeds;
    }

    [Serializable]
    private class FeedData
    {
        public string field1;
        public string field2;
        public string field3;
        public string field4;
        public string field5;
        public string field6;
    }

    [Serializable]
    private class ChannelData
    {
        public string id;
        public string name;
        public string description;
        public string latitude;
        public string longitude;
        public string created_at;
        public string updated_at;
        public string last_entry_id;
    }
}
