using UnityEngine;
using UnityEngine.AI;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float movementSpeed = 5f;
    [SerializeField] private bool useCameraDirectionAsForward = false;

    private PlayerController _playerController;

    private NavMeshAgent _navMeshAgent;
    private Joystick _joystick;
    private Transform _cameraTransform;

    public PlayerMovement Initialize(PlayerController playerController)
    {
        _playerController = playerController;
        _cameraTransform = GameManager.Instance.cameraManager.mainCameraBrain.transform;

        if (TryGetComponent(out _navMeshAgent))
        {
            _navMeshAgent.speed = movementSpeed;
            _navMeshAgent.enabled = true;
        }

        return this;
    }

    private void Start()
    {
        _joystick = UIManager.Instance.gamePlay.joystick;
    }

    private void Update()
    {
        if (GameManager.Instance.GameState == GameState.Gameplay)
        {
            Move();
        }
    }

    private const float MovementThreshold = 0.1f;

    private void Move()
    {
        if (_joystick.Direction.magnitude <= MovementThreshold)
            return;

        var worldDirection = new Vector3(_joystick.Direction.x, 0, _joystick.Direction.y);

        if (useCameraDirectionAsForward)
        {
            var forward = _cameraTransform.forward;
            var right = _cameraTransform.right;
            forward.y = 0;
            right.y = 0;

            forward = forward.normalized;
            right = right.normalized;
            worldDirection = (right * worldDirection.x) + (forward * worldDirection.z);
        }

        //worldDirection = worldDirection.normalized; //disable slow movement

        if (_navMeshAgent)
        {
            //Move Player With NavMeshAgent
            var motionVector = worldDirection * (_navMeshAgent.speed * Time.deltaTime);
            
            _navMeshAgent.Move(motionVector);
            _navMeshAgent.SetDestination(transform.position + motionVector);
        }
        else
        {
            //Move Player With Transform
            transform.position += worldDirection * (Time.deltaTime * movementSpeed);
        }

        var lookDirection = new Vector3(worldDirection.x, 0, worldDirection.z);
        var lookRotation = Quaternion.LookRotation(lookDirection, Vector3.up);

        transform.rotation = Quaternion.Lerp(transform.rotation, lookRotation, 10 * Time.deltaTime);
    }
}