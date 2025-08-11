using Sirenix.OdinInspector;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private float minSpeed, maxSpeed;
    [SerializeField] private float timeToReachMaxSpeed;

    [SerializeField, ReadOnly] private float cameraSpeed;

    private void Start()
    {
        cameraSpeed = minSpeed;
        GameManager.Instance.cameraManager.SetPlayerTarget(transform);
    }

    private void Update()
    {
        if (!PlayerController.Instance.playerMovement.IsMoving)
            return;

        MoveCamera();
    }

    private void MoveCamera()
    {
        cameraSpeed = Mathf.MoveTowards(cameraSpeed, maxSpeed, Time.deltaTime * (1f / timeToReachMaxSpeed));
        transform.Translate(Vector3.forward * (cameraSpeed * Time.deltaTime), Space.World);
    }

    public void ChangeSpeed(float speedChangeAmount)
    {
        // cameraSpeed = Mathf.Clamp(cameraSpeed + speedChangeAmount, minSpeed, maxSpeed);
        cameraSpeed = cameraSpeed + speedChangeAmount;
    }
}