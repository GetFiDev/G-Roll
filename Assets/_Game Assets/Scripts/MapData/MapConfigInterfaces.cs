using System.Collections.Generic;
using UnityEngine;

namespace MapDesignerTool
{
    public enum ConfigType
    {
        Float,      // Slider
        Bool,       // Toggle
        Int,        // Integer Slider/Field (Reserved)
        FloatInput  // Input Field (Text) for float values
    }

    [System.Serializable]
    public struct ConfigDefinition
    {
        public string key;          // "speed", "clockwise"
        public string displayName;  // "Rotation Speed"
        public ConfigType type;
        public float min;           // For slider
        public float max;           // For slider
        public float defaultValue; 
        public bool defaultBool;    // For bool types
    }

    public interface IMapConfigurable
    {
        /// <summary>
        /// Returns a list of configurable properties supported by this object.
        /// </summary>
        List<ConfigDefinition> GetConfigDefinitions();

        /// <summary>
        /// Applies the configuration values to the object.
        /// Called when UI changes or when map is loaded.
        /// </summary>
        void ApplyConfig(Dictionary<string, string> config);
    }
}
