using UnityEngine;
using UnityEngine.UI;

public class ObjectVisibilityToggle : MonoBehaviour
{
    public GameObject bimObject;
    public GameObject pointCloudObject;
    public Toggle visibilityToggle;

    void Start()
    {
        // Set the initial visibility based on the toggle state
        SetVisibility();

        // Register the Toggle's onValueChanged event to the ToggleVisibility method
        visibilityToggle.onValueChanged.AddListener(ToggleVisibility);
    }

    void SetVisibility()
    {
        // Set the visibility based on the toggle state
        bimObject.SetActive(!visibilityToggle.isOn);
        pointCloudObject.SetActive(visibilityToggle.isOn);
    }

    void ToggleVisibility(bool isVisible)
    {
        // Toggle the visibility based on the toggle state
        bimObject.SetActive(!isVisible);
        pointCloudObject.SetActive(isVisible);
    }
}
