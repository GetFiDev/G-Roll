using UnityEngine;

public class UILoadingIconSpinner : MonoBehaviour
{
    [SerializeField] private int frameCount = 12;
    [SerializeField] private float spinDuration = 0.8f;
    
    private float _dt;

    private void LateUpdate()
    {
        _dt += Time.unscaledDeltaTime;

        var localRotation = transform.localRotation.eulerAngles;
        var r = localRotation.z;

        var fTime = spinDuration/frameCount;
        var hasChanged = false;

        while (_dt > fTime)
        {
            r -= 360f/frameCount;
            _dt -= fTime;
            hasChanged = true;
        }

        if (hasChanged)
        {
            transform.localRotation = Quaternion.Euler(localRotation.x, localRotation.y, r);
        }
    }
}
