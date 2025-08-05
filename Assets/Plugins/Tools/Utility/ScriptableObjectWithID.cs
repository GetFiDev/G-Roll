using System;
using Sirenix.OdinInspector;
using UnityEngine;

// ReSharper disable InconsistentNaming
public class ScriptableObjectWithID : ScriptableObject
{
    [SerializeField, ReadOnly] private string _objectID;

    [ReadOnly, LabelText("Save Key and ID")]
    public string ObjectID
    {
        get
        {
            if (_objectID == null || string.IsNullOrEmpty(_objectID))
                _objectID = Guid.NewGuid().ToString();

            return _objectID;
        }

        private set => _objectID = value;
    }

#if UNITY_EDITOR
    private void Awake()
    {
        if (_objectID == null || string.IsNullOrEmpty(_objectID))
        {
            _objectID = Guid.NewGuid().ToString();
        }
    }

    [ContextMenu("Update Object ID")]
    private void UpdateObjectID()
    {
        _objectID = Guid.NewGuid().ToString();
    }
#endif
}