using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace MapDesignerTool
{
    /// <summary>
    /// Component for each map item in the Map Browser list.
    /// Attach to both chapter and endless item prefabs.
    /// </summary>
    public class MapBrowserItem : MonoBehaviour
    {
        [Header("UI References")]
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI subtitleText; // Optional: shows order/difficulty
        public Button editButton;
        public Button deleteButton;

        [Header("Visual")]
        public Image backgroundImage;
        public Color chapterColor = new Color(0.2f, 0.6f, 1f, 1f);
        public Color endlessColor = new Color(1f, 0.6f, 0.2f, 1f);

        // Data
        private string _mapId;
        private string _mapType;
        private string _displayName;

        // Events
        public event Action<string, string> OnEditClicked; // (mapType, mapId)
        public event Action<string, string, string> OnDeleteClicked; // (mapType, mapId, displayName)

        void Awake()
        {
            if (editButton) editButton.onClick.AddListener(HandleEditClick);
            if (deleteButton) deleteButton.onClick.AddListener(HandleDeleteClick);
        }

        void OnDestroy()
        {
            if (editButton) editButton.onClick.RemoveListener(HandleEditClick);
            if (deleteButton) deleteButton.onClick.RemoveListener(HandleDeleteClick);
        }

        /// <summary>
        /// Initialize for a chapter map
        /// </summary>
        public void SetupChapter(string mapId, string displayName, int order)
        {
            _mapId = mapId;
            _mapType = "chapter";
            _displayName = displayName;

            if (nameText) nameText.text = displayName;
            if (subtitleText) subtitleText.text = $"Order: {order}";
            if (backgroundImage) backgroundImage.color = chapterColor;
        }

        /// <summary>
        /// Initialize for an endless map
        /// </summary>
        public void SetupEndless(string mapId, string displayName, int difficulty)
        {
            _mapId = mapId;
            _mapType = "endless";
            _displayName = displayName;

            string diffText = difficulty switch
            {
                1 => "Very Easy",
                2 => "Easy",
                3 => "Medium",
                4 => "Hard",
                _ => $"Diff: {difficulty}"
            };

            if (nameText) nameText.text = displayName;
            if (subtitleText) subtitleText.text = diffText;
            if (backgroundImage) backgroundImage.color = endlessColor;
        }

        void HandleEditClick()
        {
            OnEditClicked?.Invoke(_mapType, _mapId);
        }

        void HandleDeleteClick()
        {
            OnDeleteClicked?.Invoke(_mapType, _mapId, _displayName);
        }
    }
}
