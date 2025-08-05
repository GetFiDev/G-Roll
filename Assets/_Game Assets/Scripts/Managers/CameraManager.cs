using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;
using Sirenix.OdinInspector;

public class CameraManager : MonoBehaviour
{
    [InfoBox("Main Camera with CinemachineBrain is required.", InfoMessageType.Error, "CheckForCameraBrain")]
    [ReadOnly, Required]
    public CinemachineBrain mainCameraBrain;

    [SerializeField] private CinemachineCamera defaultVirtualCamera;
    [SerializeField] private CinemachineCamera targetedVirtualCamera;

    private CinemachineCamera _activeVirtualCamera;

    private List<Transition> _transitions;
    public bool IsPlayerTargeted => _activeVirtualCamera.Follow == PlayerController.Instance.transform;

    public CameraManager Initialize()
    {
        mainCameraBrain = GetComponentInChildren<CinemachineBrain>();

        _activeVirtualCamera = defaultVirtualCamera;
        _transitions = new List<Transition>();

        return this;
    }

    public void SetPlayerTarget(Transform playerTransform)
    {
        defaultVirtualCamera.gameObject.SetActive(true);
        
        defaultVirtualCamera.Follow = playerTransform;
        defaultVirtualCamera.LookAt = playerTransform;
    }

    public void ActivateVirtualCamera(CinemachineCamera virtualCamera = null)
    {
        if (virtualCamera)
        {
            _activeVirtualCamera = virtualCamera;
            virtualCamera.gameObject.SetActive(true);
        }
        else
        {
            _activeVirtualCamera.gameObject.SetActive(false);
            _activeVirtualCamera = defaultVirtualCamera;
            _activeVirtualCamera.gameObject.SetActive(true);
        }
    }

    private CinemachineCamera _activeTargetCamera;

    public void SetCameraTarget(Transform targetTransform)
    {
        _activeTargetCamera = Instantiate(targetedVirtualCamera, transform, true);
        _activeTargetCamera.Follow = targetTransform;
        _activeTargetCamera.LookAt = targetTransform;

        ActivateVirtualCamera(_activeTargetCamera);
    }

    public void AddCameraTarget(Transform targetTransform, Action onCompleteAction = null, float actionDelay = 0)
    {
        var transition = new Transition()
        {
            TransitionTarget = targetTransform,
            TransitionCompleteAction = onCompleteAction,
            TransitionDelay = actionDelay
        };

        _transitions.Add(transition);

        ShowNextTarget();
    }

    public void RemoveCameraTarget(Transform targetTransform)
    {
        if (!_activeTargetCamera || _activeTargetCamera.Follow != targetTransform)
            return;

        ActivateVirtualCamera();
        Destroy(_activeTargetCamera.gameObject, TransitionTime + .1f);
    }

    private Transition _activeTransition;

    private void ShowNextTarget()
    {
        if (_transitions.Count <= 0)
        {
            ActivateVirtualCamera();
            return;
        }

        if (_activeTransition == null)
        {
            StartCoroutine(TransitionCoroutine());
        }
    }

    private const float TransitionTime = 1f;

    private IEnumerator TransitionCoroutine()
    {
        _activeTransition = _transitions[0];
        //Go to targeted transform
        var newTarget = Instantiate(targetedVirtualCamera, transform, true);
        newTarget.Follow = _activeTransition.TransitionTarget;
        newTarget.LookAt = _activeTransition.TransitionTarget;
        _transitions.RemoveAt(0);
        ActivateVirtualCamera(newTarget);

        //Wait for transition
        yield return new WaitForSeconds(TransitionTime);

        _activeTransition.TransitionCompleteAction?.Invoke();

        yield return new WaitForSeconds(_activeTransition.TransitionDelay);

        _activeTransition = null;
        Destroy(newTarget.gameObject, TransitionTime + .1f);
        ShowNextTarget();
    }

    private class Transition
    {
        public Transform TransitionTarget;
        public Action TransitionCompleteAction;
        public float TransitionDelay;
    }

    //Used by OdinInspector for Editor Control
    private bool CheckForCameraBrain()
    {
        mainCameraBrain = GetComponentInChildren<CinemachineBrain>();

        return mainCameraBrain is null;
    }
}