using Sirenix.OdinInspector;

public class PlayerController : MonoSingleton<PlayerController>
{
    [ReadOnly, Required] public PlayerCollision playerCollision;
    [ReadOnly, Required] public PlayerMovement playerMovement;
    [ReadOnly, Required] public PlayerAnimator playerAnimator;
    
    private void Start()
    {
        playerCollision = GetComponent<PlayerCollision>().Initialize(this);
        playerMovement = GetComponent<PlayerMovement>().Initialize(this);
        playerAnimator = GetComponentInChildren<PlayerAnimator>().Initialize(this);
        
        GameManager.Instance.cameraManager.SetPlayerTarget(transform);
    }

    private void OnValidate()
    {
        playerCollision = GetComponent<PlayerCollision>();
        playerMovement = GetComponent<PlayerMovement>();
        playerAnimator = GetComponentInChildren<PlayerAnimator>();
    }
}
