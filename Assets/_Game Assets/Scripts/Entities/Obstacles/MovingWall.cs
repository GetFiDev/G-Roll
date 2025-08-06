using System.Collections.Generic;
using UnityEngine;

public class MovingWall : Wall
{
    [SerializeField] private List<Transform> targetPositions;
    [SerializeField] private float speed;

    private Transform _targetPosition;
    private int _targetIndex;

    private void Start()
    {
        _targetPosition = targetPositions[0];
    }

    private void Update()
    {
        if (!PlayerController.Instance.playerMovement.IsMoving)
            return;
            
        var remainingDistance = (_targetPosition.position - transform.position).magnitude;

        if (remainingDistance > 0.1f)
        {
            transform.position =
                Vector3.MoveTowards(transform.position, _targetPosition.position, Time.deltaTime * speed);
        }
        else
        {
            _targetIndex = (_targetIndex + 1) % targetPositions.Count;
            _targetPosition = targetPositions[_targetIndex];
        }
    }
}