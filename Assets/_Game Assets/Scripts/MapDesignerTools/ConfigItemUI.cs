using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MapDesignerTool
{
    public class ConfigItemUI : MonoBehaviour
    {
        [Header("Common")]
        public TextMeshProUGUI labelText;

        [Header("For Sliders")]
        public Slider slider;
        public TextMeshProUGUI valueText;

        [Header("For Toggles")]
        public Toggle toggle;

        [Header("For Input")]
        public TMP_InputField inputField;

        [Header("Transform Controls")]
        public Button moveXPos;
        public Button moveXNeg;
        public Button moveZPos;
        public Button moveZNeg;
        public Button rotateBtn;
    }
}
