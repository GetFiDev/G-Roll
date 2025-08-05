using UnityEngine;

[System.Serializable]
public class ProbabilityEntry<T> where T : Object
{
    public T data;

    [Range(0f, 100f)]
    public float probability = 100f;
}