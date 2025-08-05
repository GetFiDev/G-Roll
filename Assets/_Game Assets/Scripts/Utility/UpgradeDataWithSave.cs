using System;
using UnityEngine;

[Serializable]
public class UpgradeDataWithSave<T> : UpgradeData<int> where T : struct, IConvertible, IComparable, IFormattable
{
    public override int CurrentLevel
    {
        get
        {
            if (string.IsNullOrEmpty(SaveID))
                throw new NullReferenceException("SaveID is null");

            return PlayerPrefs.GetInt(SaveID + "_Level", 0);
        }
        protected set
        {
            if (string.IsNullOrEmpty(SaveID))
                throw new NullReferenceException("SaveID is null");

            PlayerPrefs.SetInt(SaveID + "_Level", value);
        }
    }

    public string SaveID { get; private set; }

    public void SetSaveID(string id)
    {
        SaveID = id;
    }
}