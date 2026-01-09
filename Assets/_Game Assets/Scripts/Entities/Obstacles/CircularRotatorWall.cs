using UnityEngine;
using DG.Tweening;

public class CircularRotatorWall : Wall
{
    [Header("Rotator Settings")]
    [Tooltip("The actual object that will rotate.")]
    public Transform movingPart;
    [Tooltip("Time in seconds for a full 360-degree rotation.")]
    public float rotationDuration = 2f;
    [Tooltip("If true, rotates clockwise. If false, rotates counter-clockwise. Can be changed at runtime.")]
    public bool clockwise = true;

    private void Start()
    {
        if (movingPart == null)
        {
            Debug.LogError("CircularRotatorWall: Moving Part is missing!", this);
            return;
        }

        // Setup Collision Proxy on ALL child colliders recursively (from Root)
        var allColliders = GetComponentsInChildren<Collider>(true);
        foreach (var col in allColliders)
        {
            var proxy = col.GetComponent<CircularRotatorCollisionProxy>();
            if (proxy == null) proxy = col.gameObject.AddComponent<CircularRotatorCollisionProxy>();
            proxy.owner = this;
        }
    }

    private void Update()
    {
        if (movingPart == null || rotationDuration <= 0f) return;

        // Calculate speed: 360 degrees / duration
        float speed = 360f / rotationDuration;
        
        // Determine direction multiplier
        float direction = clockwise ? 1f : -1f;

        // Rotate movingPart on Y axis
        // We use Space.Self to rotate around its own local Y axis, or Space.World if desired.
        // Usually local Y is what's expected for a spinner.
        movingPart.Rotate(0, speed * direction * Time.deltaTime, 0, Space.Self);
    }

    // Public methods for the proxy to call
    public void OnProxyTriggerEnter(Collider other)
    {
        if (expectTrigger) NotifyPlayer(other);
    }

    public void OnProxyCollisionEnter(Collision collision)
    {
        if (!expectTrigger) NotifyPlayer(collision.collider);
    }


}

// Helper component added automatically to the moving part
public class CircularRotatorCollisionProxy : MonoBehaviour
{
    public CircularRotatorWall owner;

    private void OnTriggerEnter(Collider other)
    {
        if (owner != null) owner.OnProxyTriggerEnter(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (owner != null) owner.OnProxyCollisionEnter(collision);
    }
}
