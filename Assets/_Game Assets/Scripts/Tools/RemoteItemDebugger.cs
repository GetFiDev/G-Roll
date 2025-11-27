using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
#if UNITY_EDITOR
using Sirenix.OdinInspector;
#endif

public class RemoteItemDebugger : MonoBehaviour
{
#if UNITY_EDITOR
    [Button("Fetch All Items")]
#endif
    public async void FetchAllItems()
    {
        try
        {
            var items = await RemoteItemService.FetchAllItemsAsync();
            Debug.Log($"[RemoteItemDebugger] Total items: {items.Count}");
            foreach (KeyValuePair<string, RemoteItemService.ItemData> kv in items)
            {
                string json = JsonUtility.ToJson(kv.Value, true);
                Debug.Log($"[ITEM] {kv.Key}:\n{json}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[RemoteItemDebugger] Error: {ex.Message}");
        }
    }
}