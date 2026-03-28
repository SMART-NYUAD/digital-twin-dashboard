using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using System.Collections;

public class LightSwitch : MonoBehaviour
{
    public Toggle toggleSwitch;
    public string apiUrl = "http://YOUR_HOME_ASSISTANT_IP:8123/api/";
    public string longLivedAccessToken = "YOUR_LONG_LIVED_ACCESS_TOKEN";
    public string entityId = "light.living_room";
    public string serviceType = "light"; // Default service type is "light"
    public float refreshInterval = 5f; // Interval in seconds to refresh the entity state

    private bool previousToggleState;

    void Start()
    {
        toggleSwitch.onValueChanged.AddListener(delegate { OnToggleValueChanged(); });
        previousToggleState = toggleSwitch.isOn;

        // Start refreshing the entity state periodically
        StartCoroutine(RefreshEntityState());
    }

    IEnumerator RefreshEntityState()
    {
        while (true)
        {
            // Get the current state of the entity from Home Assistant
            yield return GetEntityState(entityId);
            // Wait for the specified interval before refreshing again
            yield return new WaitForSeconds(refreshInterval);
        }
    }

    void OnToggleValueChanged()
    {
        // Check if the toggle state has changed
        if (toggleSwitch.isOn != previousToggleState)
        {
            // Send the state change request to Home Assistant
            StartCoroutine(SetServiceState(entityId, toggleSwitch.isOn));
        }
    }

    IEnumerator SetServiceState(string entityId, bool newState)
    {
        string url = apiUrl + "services/" + serviceType + "/turn_" + (newState ? "on" : "off");
        string jsonData = "{\"entity_id\": \"" + entityId + "\"}";

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
        request.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + longLivedAccessToken);

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.ConnectionError ||
            request.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError("Error: " + request.error);
        }
        else
        {
            Debug.Log("Service state change request sent successfully");
        }
    }

    IEnumerator GetEntityState(string entityId)
    {
        string url = apiUrl + "states/" + entityId;

        UnityWebRequest request = UnityWebRequest.Get(url);
        request.SetRequestHeader("Authorization", "Bearer " + longLivedAccessToken);

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.ConnectionError ||
            request.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError("Error: " + request.error);
        }
        else
        {
            string responseJson = request.downloadHandler.text;
            bool newState = ParseEntityState(responseJson);
            // Update the toggle switch state if it has changed
            if (newState != previousToggleState)
            {
                toggleSwitch.isOn = newState;
                previousToggleState = newState;
            }
        }
    }

    bool ParseEntityState(string json)
    {
        // Parse the JSON response to get the state of the entity
        // You may need to adjust this based on the structure of your Home Assistant API response
        // Here is a simplified example assuming the JSON has a "state" field with a boolean value
        return json.Contains("\"state\":\"on\"");
    }
}
