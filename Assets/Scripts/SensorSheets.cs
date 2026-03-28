using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class CSVFetcher : MonoBehaviour
{
    [SerializeField] private string csvUrl; // URL to the CSV file
    [SerializeField] private int columnNumber; // Column number to extract value from (1-based index)
    [SerializeField] private Material cubeMaterial; // Material with emission applied to the cube
    [SerializeField] private Color minBrightnessColor = Color.black; // Minimum brightness color
    [SerializeField] private Color maxBrightnessColor = Color.white; // Maximum brightness color
    [SerializeField] private float updateInterval = 5f; // Update interval in seconds

    private void Start()
    {
        StartCoroutine(FetchAndParseCSVPeriodically());
    }

    private IEnumerator FetchAndParseCSVPeriodically()
    {
        while (true)
        {
            yield return StartCoroutine(FetchAndParseCSV());
            yield return new WaitForSeconds(updateInterval);
        }
    }

    private IEnumerator FetchAndParseCSV()
    {
        using (UnityWebRequest www = UnityWebRequest.Get(csvUrl))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Failed to fetch CSV file: " + www.error);
                yield break;
            }

            string csvText = www.downloadHandler.text;
            string latestValue = ParseCSVForLatestColumnValue(csvText, columnNumber);

            if (latestValue != null)
            {
                Debug.Log("Obtained value from CSV: " + latestValue);

                // Remove surrounding quotes from the obtained value
                latestValue = latestValue.Trim('"');

                // Convert the obtained value to a float
                if (float.TryParse(latestValue, out float parsedValue))
                {
                    // Map the obtained value to the color range
                    Color color = Color.Lerp(minBrightnessColor, maxBrightnessColor, Mathf.Clamp01(parsedValue / 200f));
                    // Set the emission color of the cube material
                    cubeMaterial.SetColor("_EmissionColor", color);
                    // Enable emission
                    cubeMaterial.EnableKeyword("_EMISSION");
                }
                else
                {
                    Debug.LogError("Failed to parse obtained value to float.");
                }
            }
        }
    }

    private string ParseCSVForLatestColumnValue(string csvText, int columnNumber)
    {
        string[] lines = csvText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        // Start from index 1 to skip header row
        List<string> columnValues = new List<string>();
        for (int i = 1; i < lines.Length; i++)
        {
            string[] fields = lines[i].Split(',');
            if (fields.Length >= columnNumber)
            {
                columnValues.Add(fields[columnNumber - 1]); // Adjusting for 1-based index
            }
        }

        // Return the bottom-most value in the column
        if (columnValues.Count > 0)
        {
            return columnValues.Last();
        }
        else
        {
            Debug.LogError("No data found for column number " + columnNumber + ".");
            return null;
        }
    }
}
