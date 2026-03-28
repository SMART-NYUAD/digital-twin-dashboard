using UnityEngine;
using System.Collections;

public class SensorDataFetcher : MonoBehaviour
{
    // Dummy base values per sensor index (used for all property types via HeatmapManager dummy init)
    private static readonly float[] dummyBaseValues = { 22.5f, 23.8f, 24.2f, 23.1f, 22.9f, 24.5f };

    public IEnumerator FetchData(string apiUrl, string apiToken, string[] entityIds, float[] data, System.Action onFetchComplete)
    {
        for (int i = 0; i < entityIds.Length; i++)
        {
            yield return StartCoroutine(GetEntityState(apiUrl, apiToken, entityIds[i], i, data));
        }

        onFetchComplete?.Invoke();
    }

    IEnumerator GetEntityState(string apiUrl, string apiToken, string entityId, int index, float[] data)
    {
        // Dummy data: no network connection needed
        float baseValue = index < dummyBaseValues.Length ? dummyBaseValues[index] : 23.0f;
        data[index] = baseValue + Random.Range(-0.2f, 0.2f);
        yield return null;
    }
}
