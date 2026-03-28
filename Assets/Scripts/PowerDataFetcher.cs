using UnityEngine;
using System.Collections;
using TMPro;
using UnityEngine.UI;

namespace PowerNamespace
{
    public class PowerDataFetcher : MonoBehaviour
    {
        public string apiUrl;
        public string apiToken;
        public string[] entityIds = new string[4];
        public string[] maxPowerEntityIds = new string[4];
        public GameObject powerGaugePrefab;
        public Vector3[] gaugePositions = new Vector3[4];
        public Camera targetCamera;
        public float refreshInterval = 60.0f;
        public Vector3 gaugeScale = new Vector3(0.001f, 0.001f, 0.001f);
        public Vector3 origin = Vector3.zero;
        public float yRotation = 0.0f;
        public Toggle visibilityToggle; // Reference to the UI toggle

        private float[] powerValues;
        private float[] maxPowers;
        private GameObject[] powerGauges;

        void Start()
        {
            if (entityIds.Length != maxPowerEntityIds.Length || entityIds.Length != gaugePositions.Length)
            {
                Debug.LogError("Length of entityIds, maxPowerEntityIds, and gaugePositions must be equal.");
                return;
            }

            powerValues = new float[entityIds.Length];
            maxPowers = new float[entityIds.Length];
            powerGauges = new GameObject[entityIds.Length];

            for (int i = 0; i < entityIds.Length; i++)
            {
                if (powerGaugePrefab != null)
                {
                    Vector3 finalPosition = RotatePosition(gaugePositions[i]) + origin;
                    Quaternion finalRotation = Quaternion.Euler(0, yRotation, 0);

                    powerGauges[i] = Instantiate(powerGaugePrefab, finalPosition, finalRotation);
                    powerGauges[i].transform.localScale = gaugeScale;

                    Billboard billboard = powerGauges[i].AddComponent<Billboard>();
                    billboard.targetCamera = targetCamera;

                    Canvas canvas = powerGauges[i].GetComponent<Canvas>();
                    if (canvas != null)
                    {
                        canvas.renderMode = RenderMode.WorldSpace;
                        canvas.worldCamera = targetCamera;
                    }
                }
            }

            // Register the toggle change event
            if (visibilityToggle != null)
            {
                visibilityToggle.onValueChanged.AddListener(OnToggleValueChanged);
            }

            StartCoroutine(FetchMaxPowers());
        }

        Vector3 RotatePosition(Vector3 position)
        {
            Quaternion rotation = Quaternion.Euler(0, yRotation, 0);
            return rotation * position;
        }

        IEnumerator FetchMaxPowers()
        {
            // Dummy max power values (kWh) for 4 sensors
            float[] dummyMaxPowers = { 5.0f, 4.0f, 6.0f, 3.0f };
            for (int i = 0; i < maxPowers.Length; i++)
                maxPowers[i] = i < dummyMaxPowers.Length ? dummyMaxPowers[i] : 5.0f;

            yield return null;
            StartCoroutine(FetchPowerData());
            StartCoroutine(PeriodicUpdate());
        }

        IEnumerator FetchPowerData()
        {
            // Dummy power consumption values (kWh) for 4 sensors
            float[] dummyPowerValues = { 1.2f, 0.8f, 2.1f, 0.5f };
            for (int i = 0; i < powerValues.Length; i++)
                powerValues[i] = (i < dummyPowerValues.Length ? dummyPowerValues[i] : 1.0f)
                                  + Random.Range(-0.1f, 0.1f);

            yield return null;
            UpdatePowerGauges();
        }

        void UpdatePowerGauges()
        {
            for (int i = 0; i < powerGauges.Length; i++)
            {
                if (powerGauges[i] != null)
                {
                    TextMeshPro textComponent = powerGauges[i].GetComponentInChildren<TextMeshPro>();
                    if (textComponent != null)
                    {
                        textComponent.text = $"{powerValues[i]:F2} kWh";
                    }

                    Slider slider = powerGauges[i].GetComponentInChildren<Slider>();
                    if (slider != null)
                    {
                        // Clamp the power value to the maximum power for slider visualization
                        float clampedPowerValue = Mathf.Min(powerValues[i], maxPowers[i]);
                        // Calculate the percentage of max power used
                        float percentage = clampedPowerValue / maxPowers[i];
                        // Set the slider value based on this percentage
                        slider.value = percentage;

                        // Change the slider fill color based on the percentage
                        SetSliderFillColor(slider, percentage);
                    }
                }
            }
        }

        void SetSliderFillColor(Slider slider, float percentage)
        {
            Image fillImage = slider.fillRect.GetComponent<Image>();
            if (fillImage != null)
            {
                if (percentage <= 0.25f)
                {
                    fillImage.color = Color.green;
                }
                else if (percentage <= 0.50f)
                {
                    fillImage.color = Color.yellow;
                }
                else if (percentage <= 0.75f)
                {
                    fillImage.color = new Color(1.0f, 0.65f, 0.0f); // Orange
                }
                else
                {
                    fillImage.color = Color.red;
                }
            }
        }

        IEnumerator PeriodicUpdate()
        {
            while (true)
            {
                yield return new WaitForSeconds(refreshInterval);
                yield return StartCoroutine(FetchPowerData());
            }
        }

        // Method called when the toggle value changes
        void OnToggleValueChanged(bool isOn)
        {
            for (int i = 0; i < powerGauges.Length; i++)
            {
                if (powerGauges[i] != null)
                {
                    powerGauges[i].SetActive(isOn);
                }
            }
        }
    }

}
