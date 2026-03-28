using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using System.IO;
using System;
using System.Globalization;
using TMPro;
using System.Threading.Tasks;
using Newtonsoft.Json;

public class CSVReader : MonoBehaviour
{
    [SerializeField] private string csvUrl; // URL to the CSV file
    [SerializeField] private GameObject avatarPrefab; // Drag and drop your avatar prefab here
    [SerializeField] private float trackingCameraX; // X coordinate of the tracking camera
    [SerializeField] private float trackingCameraZ; // Z coordinate of the tracking camera
    [SerializeField] private float trackingCameraYRot; // Y Rotation of the tracking camera
#pragma warning disable CS0414
    [SerializeField] private float updateInterval = 0.5f; // Update interval in seconds (used by commented-out InvokeRepeating)
#pragma warning restore CS0414
    [SerializeField] private Toggle avatarToggle; // Drag and drop the UI Toggle object here
    [SerializeField] private HeatmapManager heatmapManager; // Reference to HeatmapManager

    private Transform avatarContainer;
    private List<GameObject> avatarPool;
    private bool showAvatars = true; // Declare the showAvatars variable
    public Camera mainCamera; // Reference to the main camera for billboarding

    private MQTTManager mqttManager;
    private string latestMessage = "";

    public class ThermalComfortCalculator
{
    public static double CalculatePMV(double temperature, double humidity, double CLO)
    {
        // Constants
        double VEL = 0.1;  // Air velocity in m/s
        double WME = 0.0;  // External work, normally around 0
        double MET = 1.1;  // Metabolic rate in met units (e.g., 1.2 for office work)

        // Calculate saturated vapor pressure (PA)
        double PA = humidity / 100 * 610.6 * Math.Exp((17.27 * temperature) / (237.7 + temperature));

        double ICL = 0.155 * CLO;
        double M = MET * 58.15;
        double W = WME * 58.15;
        double MW = M - W;

        double FCL = ICL < 0.078 ? 1 + 1.29 * ICL : 1.05 + 0.645 * ICL;

        double HCF = 12.1 * Math.Sqrt(VEL);
        double TAA = temperature + 273;
        double TRA = TAA;

        // Initial guess for clothing surface temperature
        double TCLA = TAA + (35.5 - temperature) / (3.5 * (6.45 * ICL + 0.1));

        double P1 = ICL * FCL;
        double P2 = P1 * 3.96;
        double P3 = P1 * 100;
        double P4 = P1 * TAA;
        double P5 = 308.7 - 0.028 * MW + P2 * Math.Pow(TRA / 100, 4);

        double XN = TCLA / 100;
        double XF;
        double EPS = 0.00015;
        int N = 0;

        // Iterative calculation of TCL (clothing surface temperature)
        do
        {
            XF = XN;
            double HCN = 2.38 * Math.Pow(Math.Abs(100 * XF - TAA), 0.25);
            double HC = HCF > HCN ? HCF : HCN;

            XN = (P5 + P4 * HC - P2 * Math.Pow(XF, 4)) / (100 + P3 * HC);

            N++;
        }
        while (N <= 150 && Math.Abs(XN - XF) > EPS);

        double TCL = 100 * XN - 273;

        // Heat loss components
        double HL1 = 3.05 * 0.001 * (5733 - 6.99 * MW - PA);
        double HL2 = MW > 58.15 ? 0.42 * (MW - 58.15) : 0.0;
        double HL3 = 1.7 * 0.00001 * M * (5867 - PA);
        double HL4 = 0.0014 * M * (34 - temperature);
        double HL5 = 3.96 * FCL * (Math.Pow(XN, 4) - Math.Pow(TRA / 100, 4));
        double HL6 = FCL * HCF * (TCL - temperature);

        // Calculate PMV
        double TS = 0.303 * Math.Exp(-0.036 * M) + 0.028;
        double PMV = TS * (MW - HL1 - HL2 - HL3 - HL4 - HL5 - HL6);

        return PMV;
    }
}

    private void Start()
    {
        // Create a container for avatars
        avatarContainer = new GameObject("AvatarContainer").transform;

        // Create the avatar pool
        avatarPool = new List<GameObject>();

        // Ensure HeatmapManager is set
        if (heatmapManager == null)
        {
            heatmapManager = FindObjectOfType<HeatmapManager>();
        }

        if (heatmapManager == null)
        {
            Debug.LogError("HeatmapManager not found!");
            enabled = false; // Disable this script if HeatmapManager is not found
            return;
        }

        // Update coordinates and heatmap data at regular intervals
        //InvokeRepeating("UpdateData", 0f, updateInterval);
        UpdateData();

        // Attach event listener to the avatarToggle
        if (avatarToggle != null)
        {
            avatarToggle.onValueChanged.AddListener(ToggleShowAvatars);
        }

        mqttManager = new MQTTManager();
        mqttManager.OnMessageReceived += HandleMessageReceived;
        StartCoroutine(InitializeMQTT());
    }

    void HandleMessageReceived(string message)
    {
        latestMessage = message;
    }

    IEnumerator InitializeMQTT()
    {
        Task initTask = mqttManager.Initialize();
        while (!initTask.IsCompleted)
        {
            yield return null;
        }
    }

    private void UpdateData()
    {
        // Fetch and parse CSV data
        StartCoroutine(FetchAndParseMQTT());

        // Log heatmap data
        LogHeatmapData();
        Invoke(nameof(UpdateData), 0f); // Immediately call UpdateData again
    }

    IEnumerator FetchAndParseMQTT()
    {
        if (!string.IsNullOrEmpty(latestMessage))
        {
            List<Vector2> coordinates = ParseCoordinate(latestMessage);
            //Debug.Log($"Received Coordinates: {coordinates.Count}");
            UpdateAvatars(coordinates);
            latestMessage = ""; // Clear after processing
        }
        yield return null;
    }

    public List<Vector2> ParseCoordinate(string mqttMessage)
    {
        try
        {
            // Deserialize the MQTT message into a list of lists of objects
            var parts = JsonConvert.DeserializeObject<List<List<object>>>(mqttMessage);

            // List to store coordinates
            List<Vector2> coordinates = new List<Vector2>();

            // Loop through the parsed data
            for (int i = 0; i < parts.Count; i++)
            {
                // Ensure that the inner list has exactly 3 items
                if (parts[i].Count == 3)
                {
                    // Attempt to parse each of the values (x, y, z) from the list
                    float x = 0f, y = 0f;

                    // Parse X, Y, and Z values (if possible)
                    if (float.TryParse(parts[i][0].ToString(), out x) &&
                        float.TryParse(parts[i][1].ToString(), out y))
                    {
                        // Add the 2D coordinate (x, y) to the list
                        coordinates.Add(new Vector2(x, y));
                    }
                }
            }

            return coordinates; // Return the list of coordinates
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error parsing message: {e.Message}");
        }

        return null; // Return null if parsing fails
    }

    private IEnumerator FetchAndParseCSV()
    {
        // Fetch the CSV file data
        using (UnityWebRequest www = UnityWebRequest.Get(csvUrl))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Failed to fetch CSV file: " + www.error);
                yield break;
            }

            // Parse the CSV data
            string csvText = www.downloadHandler.text;
            List<Vector2> coordinates = ParseCSV(csvText);
            UpdateAvatars(coordinates);
        }
    }

    private List<Vector2> ParseCSV(string csvText)
    {
        List<Vector2> coords = new List<Vector2>();

        using (StringReader reader = new StringReader(csvText))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                string[] values = ParseCSVLine(line);

                if (values.Length >= 2 && float.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) && float.TryParse(values[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
                {
                    coords.Add(new Vector2(x, y));
                }
            }
        }

        return coords;
    }

    private string[] ParseCSVLine(string line)
    {
        List<string> values = new List<string>();
        bool insideQuotes = false;
        int start = 0;

        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                insideQuotes = !insideQuotes;
            }
            else if (line[i] == ',' && !insideQuotes)
            {
                values.Add(line.Substring(start, i - start).Trim('"'));
                start = i + 1;
            }
        }

        values.Add(line.Substring(start).Trim('"'));
        return values.ToArray();
    }

    private void UpdateAvatars(List<Vector2> coordinates)
    {
        ClearAvatars();

        if (avatarToggle != null && !avatarToggle.isOn)
        {
            return;
        }

        if (coordinates != null && coordinates.Count > 0)
        {
            // Get the tracking camera position and rotation
            Vector3 cameraPosition = new Vector3(trackingCameraX, 0f, trackingCameraZ);
            Quaternion cameraRotation = Quaternion.Euler(0f, trackingCameraYRot, 0f);

            foreach (Vector2 coord in coordinates)
            {
                float x = coord.x;
                float y = coord.y;

                float scaledX = coord.x * 10f;
                float scaledY = coord.y * 10f;
                Vector2 scaledCoord = new Vector2(scaledX, scaledY);

                float interpolatedTemperature = InterpolateValue(scaledCoord, "Temperature");
                float interpolatedHumidity = InterpolateValue(scaledCoord, "Humidity");

                double pmv05 = ThermalComfortCalculator.CalculatePMV(interpolatedTemperature, interpolatedHumidity, 0.57f);
                double pmv1 = ThermalComfortCalculator.CalculatePMV(interpolatedTemperature, interpolatedHumidity, 0.96f);

                // Print the coordinates and interpolated values to the console
                //Debug.Log($"Coordinate: {coord}, Interpolated PMV light: {pmv05.ToString("F1", CultureInfo.InvariantCulture)}");
                //Debug.Log($"Coordinate: {coord}, Interpolated PMV warm: {pmv1.ToString("F1", CultureInfo.InvariantCulture)}");

                // Adjust the avatar position based on the tracking camera
                Vector3 avatarOffset = new Vector3(x, 0f, y);
                Vector3 avatarPosition = cameraPosition + cameraRotation * avatarOffset;

                // Get an available avatar from the pool or create a new one
                GameObject avatar = GetAvailableAvatar();
                if (avatar == null)
                {
                    avatar = Instantiate(avatarPrefab, avatarPosition, Quaternion.identity, avatarContainer);
                    avatarPool.Add(avatar);
                }
                else
                {
                    avatar.transform.position = avatarPosition;
                    avatar.SetActive(true);
                }

                // Get the TextMeshPro component from the avatar prefab and set the PMV values
                TextMeshPro textMeshPro = avatar.GetComponentInChildren<TextMeshPro>();
                if (textMeshPro != null)
                {
                    textMeshPro.text = $"light clothing pmv: {pmv05.ToString("F1", CultureInfo.InvariantCulture)}\nwarm clothing pmv: {pmv1.ToString("F1", CultureInfo.InvariantCulture)}";
                    // Add the Billboard component and set the target camera
                    Billboard billboard = textMeshPro.gameObject.AddComponent<Billboard>();
                    billboard.targetCamera = mainCamera;
                }
            }
        }
        else
        {
            Debug.Log("No coordinates found in the CSV file.");
        }
    }


    private void ClearAvatars()
    {
        // Return all avatars to the pool
        foreach (GameObject avatar in avatarPool)
        {
            avatar.SetActive(false);
        }
    }

    private GameObject GetAvailableAvatar()
    {
        foreach (GameObject avatar in avatarPool)
        {
            if (!avatar.activeSelf)
            {
                return avatar;
            }
        }
        return null;
    }

    private void LogHeatmapData()
    {
        // Log temperature data
        float[] temperatures = heatmapManager.Temperatures;
        Vector2[] temperatureCoordinates = heatmapManager.TemperatureCoordinates;
        //LogData("Temperature", temperatures, temperatureCoordinates);

        // Log humidity data
        float[] humidity = heatmapManager.Humidity;
        Vector2[] humidityCoordinates = heatmapManager.HumidityCoordinates;
        //LogData("Humidity", humidity, humidityCoordinates);
    }

    private void LogData(string propertyName, float[] data, Vector2[] coordinates)
    {
        Debug.Log($"{propertyName} Data:");
        for (int i = 0; i < data.Length; i++)
        {
            Debug.Log($"Coordinate: {coordinates[i]}, Value: {data[i]}");
        }
    }

    private void ToggleShowAvatars(bool value)
    {
        if (avatarToggle != null)
        {
            // Update the showAvatars variable immediately
            showAvatars = value;

            // Activate or deactivate the avatars immediately
            foreach (GameObject avatar in avatarPool)
            {
                avatar.SetActive(showAvatars);
            }

            // Clear avatars if they are being deactivated
            if (!showAvatars)
            {
                ClearAvatars();
            }
        }
    }

    private float InterpolateValue(Vector2 targetCoordinate, string property)
    {
        // Retrieve property data based on input
        float[] propertyValues;
        Vector2[] propertyCoordinates;

        if (property == "Temperature")
        {
            propertyValues = heatmapManager.Temperatures;
            propertyCoordinates = heatmapManager.TemperatureCoordinates;
        }
        else if (property == "Humidity")
        {
            propertyValues = heatmapManager.Humidity;
            propertyCoordinates = heatmapManager.HumidityCoordinates;
        }
        else if (property == "PM2.5")
        {
            propertyValues = heatmapManager.PM25; 
            propertyCoordinates = heatmapManager.PM25Coordinates;
        }
        else
        {
            Debug.LogError("Unknown property: " + property);
            return float.NaN;
        }

        // Check if there are no coordinates
        if (propertyCoordinates.Length == 0)
        {
            Debug.LogWarning("No coordinates available for interpolation.");
            return float.NaN;
        }

        // Calculate the interpolated value
        float interpolatedValue = InverseSquareInterpolation(targetCoordinate, propertyCoordinates, propertyValues);
        return interpolatedValue;
    }

    private float InverseSquareInterpolation(Vector2 targetCoordinate, Vector2[] coordinates, float[] values)
    {
        float weightedSum = 0f;
        float totalWeight = 0f;

        for (int i = 0; i < coordinates.Length; i++)
        {
            float distance = Vector2.Distance(targetCoordinate, coordinates[i]);
            if (distance == 0)
            {
                return values[i]; // Exact match found
            }

            // Compute the inverse square weight
            float weight = 1 / (distance * distance);
            weightedSum += weight * values[i];
            totalWeight += weight;
        }

        if (totalWeight == 0)
        {
            Debug.LogWarning("Total weight is zero, cannot interpolate.");
            return float.NaN;
        }

        return weightedSum / totalWeight;
    }
}
