using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private float cameraSpeed;

    private void Start()
    {
        GameManager.Instance.cameraManager.SetPlayerTarget(transform);
    }

    private void Update()
    {
        if (GameManager.Instance.GameState == GameState.Gameplay && PlayerController.Instance.playerMovement.IsMoving)
            MoveCamera();
    }

    private void MoveCamera()
    {
        transform.Translate(Vector3.forward * (cameraSpeed * Time.deltaTime), Space.World);
    }
}
