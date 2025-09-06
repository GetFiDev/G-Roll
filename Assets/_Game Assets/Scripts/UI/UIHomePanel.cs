using System;
using System.Collections.Generic;
using UnityEngine;

public class UIHomePanel : MonoBehaviour
{
    private async void Start()
    {
        List<GalleryElementInfo> gallery = await FirebaseManager.GetGalleryElementsAsync();

        foreach (var item in gallery)
        {
            Debug.Log($"Title: {item.title}, URL: {item.backgroundImageUrl}");
        }
    }
}


[Serializable]
public class GalleryElementsWrapper
{
    public Dictionary<string, GalleryElementInfo> elements;
}
