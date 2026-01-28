using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GRoll.Core.Interfaces.Services;
using GRoll.Presentation.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace GRoll.Presentation.Popups
{
    /// <summary>
    /// Elite Pass popup showing premium subscription benefits and purchase option.
    /// </summary>
    public class ElitePassPopup : UIPopupBase
    {
        [Serializable]
        public class BenefitDisplay
        {
            public Image iconImage;
            public TextMeshProUGUI titleText;
            public TextMeshProUGUI descriptionText;
        }

        public class ElitePassResult
        {
            public bool Purchased { get; set; }
        }

        [Header("Header")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI subtitleText;
        [SerializeField] private Image headerImage;

        [Header("Benefits")]
        [SerializeField] private Transform benefitsContainer;
        [SerializeField] private BenefitDisplay[] benefitDisplays;

        [Header("Price")]
        [SerializeField] private TextMeshProUGUI priceText;
        [SerializeField] private TextMeshProUGUI originalPriceText;
        [SerializeField] private GameObject discountBadge;
        [SerializeField] private TextMeshProUGUI discountText;

        [Header("Status")]
        [SerializeField] private GameObject activeStatusContainer;
        [SerializeField] private TextMeshProUGUI activeStatusText;
        [SerializeField] private TextMeshProUGUI expirationText;

        [Header("Buttons")]
        [SerializeField] private Button purchaseButton;
        [SerializeField] private TextMeshProUGUI purchaseButtonText;
        [SerializeField] private Button restoreButton;
        [SerializeField] private Button closeButton;

        [Header("Processing")]
        [SerializeField] private GameObject processingPanel;

        [Header("Terms")]
        [SerializeField] private Button termsButton;
        [SerializeField] private string termsUrl = "https://example.com/terms";

        [Inject] private IIAPService _iapService;

        private bool _isEliteActive;
        private string _eliteProductId = "elite_pass_monthly";

        protected override async UniTask OnPopupShowAsync(object parameters)
        {
            SetupButtonListeners();
            await RefreshStatusAsync();
            SetupBenefits();
        }

        private void SetupButtonListeners()
        {
            if (purchaseButton != null)
            {
                purchaseButton.onClick.RemoveAllListeners();
                purchaseButton.onClick.AddListener(OnPurchaseClicked);
            }

            if (restoreButton != null)
            {
                restoreButton.onClick.RemoveAllListeners();
                restoreButton.onClick.AddListener(OnRestoreClicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(OnCloseClicked);
            }

            if (termsButton != null)
            {
                termsButton.onClick.RemoveAllListeners();
                termsButton.onClick.AddListener(OnTermsClicked);
            }
        }

        private async UniTask RefreshStatusAsync()
        {
            if (_iapService == null)
            {
                SetupUIForPurchase();
                return;
            }

            SetProcessing(true);

            try
            {
                _isEliteActive = await _iapService.IsSubscriptionActiveAsync(_eliteProductId);

                if (_isEliteActive)
                {
                    SetupUIForActive();
                }
                else
                {
                    SetupUIForPurchase();
                }
            }
            finally
            {
                SetProcessing(false);
            }
        }

        private void SetupUIForActive()
        {
            if (titleText != null)
            {
                titleText.text = "Elite Pass Active";
            }

            if (subtitleText != null)
            {
                subtitleText.text = "Enjoy your premium benefits!";
            }

            if (activeStatusContainer != null)
            {
                activeStatusContainer.SetActive(true);
            }

            if (activeStatusText != null)
            {
                activeStatusText.text = "✓ Active";
            }

            if (purchaseButton != null)
            {
                purchaseButton.gameObject.SetActive(false);
            }

            // Hide price section
            if (priceText != null)
            {
                priceText.gameObject.SetActive(false);
            }

            if (discountBadge != null)
            {
                discountBadge.SetActive(false);
            }
        }

        private void SetupUIForPurchase()
        {
            if (titleText != null)
            {
                titleText.text = "Elite Pass";
            }

            if (subtitleText != null)
            {
                subtitleText.text = "Unlock exclusive benefits!";
            }

            if (activeStatusContainer != null)
            {
                activeStatusContainer.SetActive(false);
            }

            if (purchaseButton != null)
            {
                purchaseButton.gameObject.SetActive(true);
            }

            UpdatePriceDisplay();
        }

        private void UpdatePriceDisplay()
        {
            if (_iapService == null)
            {
                if (priceText != null)
                {
                    priceText.text = "$4.99/month";
                }
                return;
            }

            var product = _iapService.GetProduct(_eliteProductId);
            if (product != null)
            {
                if (priceText != null)
                {
                    priceText.text = $"{product.LocalizedPrice}/month";
                }
            }
        }

        private void SetupBenefits()
        {
            var benefits = GetEliteBenefits();

            for (var i = 0; i < benefitDisplays.Length; i++)
            {
                if (i < benefits.Count)
                {
                    var benefit = benefits[i];
                    var display = benefitDisplays[i];

                    if (display.titleText != null)
                    {
                        display.titleText.text = benefit.Title;
                    }

                    if (display.descriptionText != null)
                    {
                        display.descriptionText.text = benefit.Description;
                    }
                }
            }
        }

        private List<EliteBenefit> GetEliteBenefits()
        {
            return new List<EliteBenefit>
            {
                new EliteBenefit
                {
                    Title = "No Ads",
                    Description = "Remove all advertisements from the game"
                },
                new EliteBenefit
                {
                    Title = "Double Coins",
                    Description = "Earn 2x coins from all gameplay"
                },
                new EliteBenefit
                {
                    Title = "Exclusive Items",
                    Description = "Access to Elite-only shop items"
                },
                new EliteBenefit
                {
                    Title = "Priority Support",
                    Description = "Get faster response from our support team"
                }
            };
        }

        private void OnPurchaseClicked()
        {
            FeedbackService?.PlaySelectionHaptic();
            PurchaseAsync().Forget();
        }

        private async UniTaskVoid PurchaseAsync()
        {
            if (_iapService == null)
            {
                FeedbackService?.ShowErrorToast("Store not available");
                return;
            }

            SetProcessing(true);

            try
            {
                var result = await _iapService.PurchaseAsync(_eliteProductId);

                if (result.Success)
                {
                    _isEliteActive = true;
                    SetupUIForActive();

                    FeedbackService?.ShowSuccessToast("Elite Pass activated!");
                    FeedbackService?.PlaySuccessHaptic();

                    // Delay close to show success state
                    await UniTask.Delay(1500);
                    CloseWithResult(new ElitePassResult { Purchased = true });
                }
                else
                {
                    if (!result.WasCancelled)
                    {
                        FeedbackService?.ShowErrorToast(result.ErrorMessage ?? "Purchase failed");
                        FeedbackService?.PlayErrorHaptic();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ElitePassPopup] Purchase error: {ex.Message}");
                FeedbackService?.ShowErrorToast("Purchase failed");
                FeedbackService?.PlayErrorHaptic();
            }
            finally
            {
                SetProcessing(false);
            }
        }

        private void OnRestoreClicked()
        {
            FeedbackService?.PlaySelectionHaptic();
            RestoreAsync().Forget();
        }

        private async UniTaskVoid RestoreAsync()
        {
            if (_iapService == null)
            {
                FeedbackService?.ShowErrorToast("Store not available");
                return;
            }

            SetProcessing(true);

            try
            {
                var restoreResult = await _iapService.RestorePurchasesAsync();

                if (restoreResult.Success)
                {
                    _isEliteActive = await _iapService.IsSubscriptionActiveAsync(_eliteProductId);

                    if (_isEliteActive)
                    {
                        SetupUIForActive();
                        FeedbackService?.ShowSuccessToast("Elite Pass restored!");
                        FeedbackService?.PlaySuccessHaptic();
                    }
                    else
                    {
                        FeedbackService?.ShowInfoToast("No active subscription found");
                    }
                }
                else
                {
                    FeedbackService?.ShowInfoToast("Nothing to restore");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ElitePassPopup] Restore error: {ex.Message}");
                FeedbackService?.ShowErrorToast("Restore failed");
            }
            finally
            {
                SetProcessing(false);
            }
        }

        private void OnTermsClicked()
        {
            FeedbackService?.PlaySelectionHaptic();
            Application.OpenURL(termsUrl);
        }

        private void OnCloseClicked()
        {
            FeedbackService?.PlaySelectionHaptic();
            CloseWithResult(new ElitePassResult { Purchased = false });
        }

        public override bool OnBackPressed()
        {
            OnCloseClicked();
            return true;
        }

        private void SetProcessing(bool processing)
        {
            if (processingPanel != null)
            {
                processingPanel.SetActive(processing);
            }

            if (purchaseButton != null)
            {
                purchaseButton.interactable = !processing;
            }

            if (restoreButton != null)
            {
                restoreButton.interactable = !processing;
            }

            if (closeButton != null)
            {
                closeButton.interactable = !processing;
            }
        }

        private class EliteBenefit
        {
            public string Title { get; set; }
            public string Description { get; set; }
        }
    }
}
