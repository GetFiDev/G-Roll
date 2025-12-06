using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
[ExecuteInEditMode]
public class UIRadialSoftMask : MonoBehaviour
{
    [Range(0, 1.5f)] public float radius = 0.5f;
    [Range(0, 1f)] public float softness = 0.2f;
    [Range(0, 1f)] public float centerX = 0.5f;
    [Range(0, 1f)] public float centerY = 0.5f;
    public bool invert = false;

    private Image _image;
    private Material _materialInstance;
    private static Shader _shader;

    private static readonly int PropRadius = Shader.PropertyToID("_Radius");
    private static readonly int PropSoftness = Shader.PropertyToID("_Softness");
    private static readonly int PropCenterX = Shader.PropertyToID("_CenterX");
    private static readonly int PropCenterY = Shader.PropertyToID("_CenterY");
    private static readonly int PropInvert = Shader.PropertyToID("_Invert");

    private void OnEnable()
    {
        _image = GetComponent<Image>();
        UpdateMaterial();
    }

    private void OnValidate()
    {
        UpdateMaterial();
    }

    private void Update()
    {
        if (Application.isPlaying)
        {
            UpdateMaterial();
        }
    }

    private void UpdateMaterial()
    {
        if (_image == null) return;

        // Ensure we have the correct material
        if (_materialInstance == null || _materialInstance.shader.name != "UI/RadialSoftMask")
        {
            if (_shader == null) _shader = Shader.Find("UI/RadialSoftMask");
            if (_shader == null) return;

            _materialInstance = new Material(_shader);
            _image.material = _materialInstance;
        }

        // Update properties
        if (_materialInstance != null)
        {
            _materialInstance.SetFloat(PropRadius, radius);
            _materialInstance.SetFloat(PropSoftness, softness);
            _materialInstance.SetFloat(PropCenterX, centerX);
            _materialInstance.SetFloat(PropCenterY, centerY);
            _materialInstance.SetFloat(PropInvert, invert ? 1f : 0f);
        }
    }

    private void OnDisable()
    {
        // Revert to default material when disabled/destroyed to avoid leaking or leaving pink objects
        if (_image != null)
        {
            _image.material = null;
        }
        
        if (_materialInstance != null)
        {
            if (Application.isPlaying) Destroy(_materialInstance);
            else DestroyImmediate(_materialInstance);
            _materialInstance = null;
        }
    }
}
