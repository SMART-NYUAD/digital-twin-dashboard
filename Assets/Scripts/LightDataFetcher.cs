using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LightNamespace
{
    public class LightDataFetcher : MonoBehaviour
    {
        public string apiUrl;
        public string apiToken;
        public LightEntityPair[] lightEntityPairs;
        public GameObject[] lightBars;
        public float refreshInterval = 60.0f;

        void Start()
        {
            if (lightEntityPairs.Length != lightBars.Length)
            {
                Debug.LogError("Length of lightEntityPairs and lightBars must be equal.");
                return;
            }

            StartCoroutine(PeriodicUpdate());
        }

        IEnumerator PeriodicUpdate()
        {
            while (true)
            {
                yield return FetchLightData();
                yield return new WaitForSeconds(refreshInterval);
            }
        }

        IEnumerator FetchLightData()
        {
            List<Task> fetchTasks = new List<Task>();

            for (int i = 0; i < lightEntityPairs.Length; i++)
            {
                fetchTasks.Add(FetchAndApplyLightData(i));
            }

            yield return new WaitUntil(() => Task.WhenAll(fetchTasks).IsCompleted);
        }

        async Task FetchAndApplyLightData(int index)
        {
            // Dummy light data: warm white light at ~70% brightness
            float avgBrightness = 0.70f + UnityEngine.Random.Range(-0.05f, 0.05f);
            Color avgColor = new Color(1.0f, 0.86f, 0.70f); // warm white

            // Update the light bar color and emission
            if (lightBars[index] != null)
            {
                Renderer renderer = lightBars[index].GetComponent<Renderer>();
                if (renderer != null)
                {
                    Material material = renderer.material;
                    material.color = avgColor * avgBrightness;

                    // Set the emission color
                    Color emissionColor = avgColor * Mathf.LinearToGammaSpace(avgBrightness);
                    material.SetColor("_EmissionColor", emissionColor);
                    material.EnableKeyword("_EMISSION");
                }

                // Add or update the Light component for halo effect
                Light haloLight = lightBars[index].GetComponent<Light>();
                if (haloLight == null)
                {
                    haloLight = lightBars[index].AddComponent<Light>();
                    haloLight.type = LightType.Area;
                    haloLight.range = 1.0f; // Adjust as needed
                    haloLight.intensity = 0.0f; // Start with 0 intensity
                }

                // Update the halo light intensity and color based on brightness
                haloLight.color = avgColor;
                haloLight.intensity = avgBrightness * 0.2f; // Adjust the multiplier as needed
            }

            await Task.CompletedTask;
        }

        [System.Serializable]
        public class LightEntityPair
        {
            public string[] entityIds = new string[2];
        }

        [System.Serializable]
        public class LightEntityStateData
        {
            public string state;
            public LightAttributes attributes;
        }

        [System.Serializable]
        public class LightAttributes
        {
            public int brightness;
            public int[] rgb_color;
        }
    }
}
