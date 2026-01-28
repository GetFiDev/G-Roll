using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GRoll.Presentation.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace GRoll.Presentation.Popups
{
    /// <summary>
    /// Referral popup for sharing referral code and viewing earnings.
    /// </summary>
    public class ReferralPopup : UIPopupBase
    {
        [Header("Referral Code")]
        [SerializeField] private TextMeshProUGUI referralCodeText;
        [SerializeField] private Button copyCodeButton;
        [SerializeField] private Button shareButton;

        [Header("Stats")]
        [SerializeField] private TextMeshProUGUI friendsCountText;
        [SerializeField] private TextMeshProUGUI totalEarningsText;
        [SerializeField] private TextMeshProUGUI pendingEarningsText;

        [Header("Friend List")]
        [SerializeField] private Transform friendListContainer;
        [SerializeField] private GameObject friendItemPrefab;
        [SerializeField] private GameObject noFriendsMessage;

        [Header("Claim")]
        [SerializeField] private Button claimButton;
        [SerializeField] private TextMeshProUGUI claimButtonText;
        [SerializeField] private GameObject claimContainer;

        [Header("Rewards Strip")]
        [SerializeField] private Transform rewardsStripContainer;

        [Header("Buttons")]
        [SerializeField] private Button closeButton;

        [Header("Processing")]
        [SerializeField] private GameObject processingPanel;

        // TODO: Inject IReferralService when available

        private string _referralCode = "";
        private int _friendsCount;
        private int _totalEarnings;
        private int _pendingEarnings;
        private List<GameObject> _instantiatedFriendItems = new();

        protected override async UniTask OnPopupShowAsync(object parameters)
        {
            SetupButtonListeners();
            await RefreshDataAsync();
            UpdateUI();
        }

        private void SetupButtonListeners()
        {
            if (copyCodeButton != null)
            {
                copyCodeButton.onClick.RemoveAllListeners();
                copyCodeButton.onClick.AddListener(OnCopyCodeClicked);
            }

            if (shareButton != null)
            {
                shareButton.onClick.RemoveAllListeners();
                shareButton.onClick.AddListener(OnShareClicked);
            }

            if (claimButton != null)
            {
                claimButton.onClick.RemoveAllListeners();
                claimButton.onClick.AddListener(OnClaimClicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(OnCloseClicked);
            }
        }

        private async UniTask RefreshDataAsync()
        {
            SetProcessing(true);

            try
            {
                // TODO: Fetch from ReferralService
                await UniTask.Delay(300); // Simulate API call

                _referralCode = "ABC123";
                _friendsCount = 0;
                _totalEarnings = 0;
                _pendingEarnings = 0;
            }
            finally
            {
                SetProcessing(false);
            }
        }

        private void UpdateUI()
        {
            UpdateReferralCode();
            UpdateStats();
            UpdateFriendList();
            UpdateClaimButton();
        }

        private void UpdateReferralCode()
        {
            if (referralCodeText != null)
            {
                referralCodeText.text = !string.IsNullOrEmpty(_referralCode) ? _referralCode : "---";
            }
        }

        private void UpdateStats()
        {
            if (friendsCountText != null)
            {
                friendsCountText.text = _friendsCount.ToString();
            }

            if (totalEarningsText != null)
            {
                totalEarningsText.text = _totalEarnings.ToString("N0");
            }

            if (pendingEarningsText != null)
            {
                pendingEarningsText.text = _pendingEarnings > 0 ? $"+{_pendingEarnings}" : "0";
            }
        }

        private void UpdateFriendList()
        {
            ClearFriendItems();

            if (noFriendsMessage != null)
            {
                noFriendsMessage.SetActive(_friendsCount == 0);
            }

            // TODO: Populate friend list from service
        }

        private void UpdateClaimButton()
        {
            if (claimContainer != null)
            {
                claimContainer.SetActive(_pendingEarnings > 0);
            }

            if (claimButton != null)
            {
                claimButton.interactable = _pendingEarnings > 0;
            }

            if (claimButtonText != null)
            {
                claimButtonText.text = _pendingEarnings > 0 ? $"Claim +{_pendingEarnings}" : "No Earnings";
            }
        }

        private void OnCopyCodeClicked()
        {
            if (string.IsNullOrEmpty(_referralCode)) return;

            GUIUtility.systemCopyBuffer = _referralCode;
            FeedbackService?.ShowSuccessToast("Code copied!");
            FeedbackService?.PlaySelectionHaptic();
        }

        private void OnShareClicked()
        {
            if (string.IsNullOrEmpty(_referralCode)) return;

            FeedbackService?.PlaySelectionHaptic();

#if (UNITY_ANDROID || UNITY_IOS) && NATIVE_SHARE_ENABLED
            var shareMessage = $"Join me in G-Roll! Use my referral code: {_referralCode}";
            new NativeShare()
                .SetText(shareMessage)
                .Share();
#else
            GUIUtility.systemCopyBuffer = _referralCode;
            FeedbackService?.ShowInfoToast("Code copied to clipboard");
#endif
        }

        private void OnClaimClicked()
        {
            if (_pendingEarnings <= 0) return;

            FeedbackService?.PlaySelectionHaptic();
            ClaimEarningsAsync().Forget();
        }

        private async UniTaskVoid ClaimEarningsAsync()
        {
            SetProcessing(true);

            try
            {
                // TODO: Call ReferralService.ClaimEarningsAsync()
                await UniTask.Delay(500); // Simulate API call

                var claimed = _pendingEarnings;
                _totalEarnings += _pendingEarnings;
                _pendingEarnings = 0;

                UpdateStats();
                UpdateClaimButton();

                FeedbackService?.ShowSuccessToast($"+{claimed} claimed!");
                FeedbackService?.PlaySuccessHaptic();
            }
            finally
            {
                SetProcessing(false);
            }
        }

        private void OnCloseClicked()
        {
            FeedbackService?.PlaySelectionHaptic();
            Close();
        }

        private void ClearFriendItems()
        {
            foreach (var item in _instantiatedFriendItems)
            {
                if (item != null)
                {
                    Destroy(item);
                }
            }
            _instantiatedFriendItems.Clear();
        }

        private void SetProcessing(bool processing)
        {
            if (processingPanel != null)
            {
                processingPanel.SetActive(processing);
            }
        }

        protected override UniTask OnPopupHideAsync()
        {
            ClearFriendItems();
            return base.OnPopupHideAsync();
        }
    }

#if !UNITY_ANDROID && !UNITY_IOS
    // Stub for NativeShare on non-mobile platforms
    public class NativeShare
    {
        private string _text;
        public NativeShare SetText(string text) { _text = text; return this; }
        public void Share() { }
    }
#endif
}
