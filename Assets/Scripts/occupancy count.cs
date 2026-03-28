using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using System.Globalization;

public class OccupancyCount : MonoBehaviour
{
    [SerializeField] private float updateInterval = 10f; // Update interval in seconds
    [SerializeField] private Text occupancyText; // UI Text to display occupancy

    // Dummy occupancy sequence to simulate people entering/leaving
    private static readonly int[] dummyOccupancy = { 3, 3, 2, 4, 3, 2, 3, 4, 3, 3 };
    private int occupancyIndex = 0;

    private void Start()
    {
        InvokeRepeating("UpdateOccupancy", 0f, updateInterval);
    }

    private void UpdateOccupancy()
    {
        StartCoroutine(FetchOccupancyData());
    }

    private IEnumerator FetchOccupancyData()
    {
        // Dummy occupancy count cycles through a pre-defined sequence
        float occupancy = dummyOccupancy[occupancyIndex % dummyOccupancy.Length];
        occupancyIndex++;
        UpdateOccupancyText(occupancy);
        yield return null;
    }

    private void UpdateOccupancyText(float occupancy)
    {
        if (occupancyText != null)
        {
            occupancyText.text = "Occupancy: " + occupancy.ToString(CultureInfo.InvariantCulture);
        }
    }
}
