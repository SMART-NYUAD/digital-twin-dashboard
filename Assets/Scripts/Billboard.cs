using UnityEngine;

public class Billboard : MonoBehaviour
{
    public Camera targetCamera;

    void Start()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main; // Automatically assign the main camera if none is set
        }
    }

    void LateUpdate()
    {
        if (targetCamera != null)
        {
            Vector3 direction = targetCamera.transform.position - transform.position;
            direction.y = 0; // Keep the y-axis rotation fixed
            transform.rotation = Quaternion.LookRotation(-direction);
        }
        else
        {
            Debug.LogWarning("Target camera not assigned.");
        }
    }
}
