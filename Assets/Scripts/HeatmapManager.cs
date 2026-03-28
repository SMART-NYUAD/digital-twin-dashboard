using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class HeatmapManager : MonoBehaviour
{
    public int x_size = 128;
    public int y_size = 128;
    public int num_shells = 10;
    public GameObject plane_prefab;
    public float height = 10;
    public float constantAlpha = 1.0f;
    public Toggle heatmapToggle;
    public Toggle textToggle; // Toggle for text display
    public GameObject textPrefab; // Prefab for displaying text
    public RawImage colorBar;
    public Text minTemperatureText;
    public Text maxTemperatureText;
    public Text middleTemperatureText;
    public string apiUrl;
    public string apiToken;
    public string[] temperatureEntityIds = new string[6];
    public string[] humidityEntityIds = new string[6];
    public string[] pressureEntityIds = new string[3];
    public string[] pm25EntityIds = new string[3];
    public Vector2[] temperatureCoordinates = new Vector2[6];
    public Vector2[] humidityCoordinates = new Vector2[6];
    public Vector2[] pressureCoordinates = new Vector2[3];
    public Vector2[] pm25Coordinates = new Vector2[3];
    public float refreshInterval = 60.0f; // Refresh interval in seconds
    public TMP_Dropdown propertyDropdown; // Reference to the TextMeshPro dropdown
    public Camera mainCamera; // Reference to the main camera for billboarding

    private GameObject[] shells;
    private GameObject[] textObjects;
    private float[] temperatures = new float[6];
    private float[] humidity = new float[6];
    private float[] pressure = new float[3];
    private float[] pm25 = new float[3];
    private Gradient heatmapGradient;
    private SensorDataFetcher sensordataFetcher;
    private HeatmapGenerator heatmapGenerator;
    private HeatmapFunctions heatmapFunctions;
    private string currentProperty = "Temperature";
    private Dictionary<string, float[]> cachedData = new Dictionary<string, float[]>();

    // Public properties to expose data and coordinates
    public float[] Temperatures => temperatures;
    public float[] Humidity => humidity;
    public float[] PM25 => pm25;
    public Vector2[] TemperatureCoordinates => temperatureCoordinates;
    public Vector2[] HumidityCoordinates => humidityCoordinates;
    public Vector2[] PM25Coordinates => pm25Coordinates;





    void Start()
    {
        // Swap x_size and y_size
        int temp = x_size;
        x_size = y_size;
        y_size = temp;

        // Initialize the heatmap generator with the coordinates for the current property
        heatmapFunctions = gameObject.AddComponent<HeatmapFunctions>();
        Vector2[] initialCoordinates = heatmapFunctions.GetCurrentCoordinates(currentProperty, temperatureCoordinates, humidityCoordinates, pressureCoordinates, pm25Coordinates);
        Vector2[] modifiedCoordinates = heatmapFunctions.SwapSensorCoordinates(initialCoordinates);

        sensordataFetcher = gameObject.AddComponent<SensorDataFetcher>();
        heatmapGenerator = gameObject.AddComponent<HeatmapGenerator>();
        heatmapGenerator.Initialize(x_size, y_size, constantAlpha, modifiedCoordinates, heatmapFunctions.GetInterpolationMethod(currentProperty));

        propertyDropdown.onValueChanged.AddListener(OnPropertyChanged);

        // Pre-fetch data for all properties
        StartCoroutine(FetchAllPropertiesData());

        StartCoroutine(heatmapFunctions.PeriodicUpdate(apiUrl, apiToken, currentProperty, sensordataFetcher, temperatureEntityIds, humidityEntityIds, pressureEntityIds, pm25EntityIds, temperatures, humidity, pressure, pm25, cachedData, heatmapGenerator, heatmapGradient, num_shells, shells, refreshInterval));
    }

    IEnumerator FetchAllPropertiesData()
    {
        // Dummy temperature data (°C) — 6 sensors spread across the lab
        temperatures[0] = 22.5f; temperatures[1] = 23.8f; temperatures[2] = 24.2f;
        temperatures[3] = 23.1f; temperatures[4] = 22.9f; temperatures[5] = 24.5f;
        heatmapFunctions.CacheData("Temperature", temperatures, cachedData);

        // Dummy humidity data (%) — 6 sensors
        humidity[0] = 48.2f; humidity[1] = 51.5f; humidity[2] = 53.0f;
        humidity[3] = 46.8f; humidity[4] = 50.3f; humidity[5] = 49.1f;
        heatmapFunctions.CacheData("Humidity", humidity, cachedData);

        // Dummy pressure data (hPa) — 3 sensors
        pressure[0] = 1013.2f; pressure[1] = 1013.5f; pressure[2] = 1013.8f;
        heatmapFunctions.CacheData("Pressure", pressure, cachedData);

        // Dummy PM2.5 data (µg/m³) — 3 sensors
        pm25[0] = 8.5f; pm25[1] = 11.2f; pm25[2] = 7.8f;
        heatmapFunctions.CacheData("PM2.5", pm25, cachedData);

        yield return null;

        // Initialize heatmap with the default property (Temperature)
        InitializeHeatmap();

        // Initialize and update text objects after data is ready
        InitializeTextObjects();
        UpdateTextObjects();
    }

    void InitializeHeatmap()
    {
        heatmapGradient = heatmapGenerator.GenerateGradient();
        shells = new GameObject[num_shells];

        heatmapFunctions.InitializeHeatmap(shells, plane_prefab, transform, heatmapGenerator, heatmapGradient, temperatures, num_shells, height);

        UpdateColorBar();
        UpdateVisibility();
    }

    void UpdateHeatmap(float[] data)
    {
        Vector2[] coordinates = heatmapFunctions.SwapSensorCoordinates(heatmapFunctions.GetCurrentCoordinates(currentProperty, temperatureCoordinates, humidityCoordinates, pressureCoordinates, pm25Coordinates)); // Ensure coordinates are updated and swapped
        
        // Ensure lengths of coordinates and data match
        if (coordinates.Length != data.Length)
        {
            Debug.LogError("The length of sensorCoordinates and data must match.");
            return;
        }

        heatmapGenerator.SetSensorCoordinates(coordinates); // Update coordinates in heatmap generator
        heatmapGenerator.SetInterpolationMethod(heatmapFunctions.GetInterpolationMethod(currentProperty)); // Update interpolation method in heatmap generator

        heatmapFunctions.UpdateHeatmap(data, heatmapGenerator, heatmapGradient, num_shells, shells);

        UpdateColorBar();
        UpdateTextObjects(); // Update text objects with new data
    }

    void Update()
    {
        UpdateVisibility();
    }

    void UpdateColorBar()
    {
        if (colorBar == null) return;

        heatmapFunctions.UpdateColorBar(colorBar, minTemperatureText, maxTemperatureText, middleTemperatureText, heatmapFunctions.GetCurrentPropertyData(currentProperty, temperatures, humidity, pressure, pm25), heatmapGradient, heatmapFunctions.GetUnit(currentProperty));
    }

    void UpdateVisibility()
    {
        bool showHeatmap = heatmapToggle != null && heatmapToggle.isOn;
        bool showText = textToggle != null && textToggle.isOn;

        if (shells != null)
        {
            foreach (var shell in shells)
            {
                if (shell != null)
                {
                    shell.SetActive(showHeatmap);
                }
            }
        }

        if (colorBar != null)
        {
            colorBar.gameObject.SetActive(showHeatmap);
        }

        if (minTemperatureText != null)
        {
            minTemperatureText.gameObject.SetActive(showHeatmap);
        }

        if (maxTemperatureText != null)
        {
            maxTemperatureText.gameObject.SetActive(showHeatmap);
        }

        if (middleTemperatureText != null)
        {
            middleTemperatureText.gameObject.SetActive(showHeatmap);
        }

        if (propertyDropdown != null)
        {
            propertyDropdown.gameObject.SetActive(showHeatmap || showText);
        }

        if (textObjects != null)
        {
            foreach (var textObject in textObjects)
            {
                if (textObject != null)
                {
                    textObject.SetActive(showText);
                }
            }
        }
    }

    void OnPropertyChanged(int index)
    {
        currentProperty = propertyDropdown.options[index].text;
        if (cachedData.ContainsKey(currentProperty))
        {
            // Use cached data
            float[] data = cachedData[currentProperty];
            UpdateHeatmap(data);
        }
        else
        {
            // Fetch new data
            string[] entityIds = heatmapFunctions.GetCurrentEntityIds(currentProperty, temperatureEntityIds, humidityEntityIds, pressureEntityIds, pm25EntityIds);
            float[] data = heatmapFunctions.GetCurrentPropertyData(currentProperty, temperatures, humidity, pressure, pm25);
            StartCoroutine(sensordataFetcher.FetchData(apiUrl, apiToken, entityIds, data, () => {
                heatmapFunctions.CacheData(currentProperty, data, cachedData);
                UpdateHeatmap(data);
            }));
        }

        // Re-initialize text objects
        InitializeTextObjects();
        UpdateTextObjects();
    }

    void InitializeTextObjects()
    {
        // Clear existing text objects
        if (textObjects != null)
        {
            foreach (var textObject in textObjects)
            {
                if (textObject != null)
                {
                    Destroy(textObject);
                }
            }
        }

        Vector2[] initialCoordinates = heatmapFunctions.GetCurrentCoordinates(currentProperty, temperatureCoordinates, humidityCoordinates, pressureCoordinates, pm25Coordinates);
        Vector3[] transformedCoordinates = TransformCoordinates(initialCoordinates);

        textObjects = new GameObject[transformedCoordinates.Length];
        for (int i = 0; i < transformedCoordinates.Length; i++)
        {
            GameObject textObject = Instantiate(textPrefab);
            textObject.transform.localPosition = transformedCoordinates[i];
            textObject.transform.localScale = Vector3.one; // Ensure scale is appropriate

            // Initialize the TextMeshPro component
            TextMeshPro textComponent = textObject.GetComponent<TextMeshPro>();
            if (textComponent != null)
            {
                textComponent.fontSize = 3f; // Adjust font size if needed
                textComponent.color = Color.white; // Ensure text color is visible
                textComponent.enableAutoSizing = false; // Ensure auto-sizing is disabled if not needed
                textComponent.alignment = TextAlignmentOptions.Center; // Ensure text alignment is correct
                textComponent.text = "Loading..."; // Initialize with a space to ensure it's rendered correctly
            }


            // Add the Billboard component and set the target camera
            Billboard billboard = textObject.AddComponent<Billboard>();
            billboard.targetCamera = mainCamera;


            textObjects[i] = textObject;
        }
    }

    void UpdateTextObjects()
    {
        float[] data = heatmapFunctions.GetCurrentPropertyData(currentProperty, temperatures, humidity, pressure, pm25);
        for (int i = 0; i < textObjects.Length; i++)
        {
            if (i < data.Length)
            {
                TextMeshPro textComponent = textObjects[i].GetComponent<TextMeshPro>();
                if (textComponent != null)
                {
                    textComponent.text = data[i].ToString("F1") + heatmapFunctions.GetUnit(currentProperty);
                }
                textObjects[i].SetActive(true); // Make sure the text object is active
            }
            else
            {
                textObjects[i].SetActive(false); // Hide unused text objects
            }
        }
    }

    Vector3[] TransformCoordinates(Vector2[] originalCoordinates)
    {
        Vector3[] transformedCoordinates = new Vector3[originalCoordinates.Length];
        for (int i = 0; i < originalCoordinates.Length; i++)
        {
            // Apply scaling
            Vector3 scaled = new Vector3(originalCoordinates[i].x, 22f, originalCoordinates[i].y) * 0.1f;

            // Apply rotation
            Vector3 rotated = RotatePosition(scaled);

            transformedCoordinates[i] = rotated;
            //Debug.Log($"Original: {originalCoordinates[i]}, Scaled: {scaled}, Rotated: {rotated}");
        }
        return transformedCoordinates;
    }

    Vector3 RotatePosition(Vector3 position)
    {
        Quaternion rotation = Quaternion.Euler(0, -90, 0);
        return rotation * position;
    }
}
