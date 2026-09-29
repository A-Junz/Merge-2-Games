using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MergePrototype.Presentation
{
    /// <summary>
    /// 顾客订单卡 Prefab 的数据视图。层级、尺寸和视觉样式全部来自 OrderCard.prefab；
    /// 此类只绑定交互并刷新订单数据。
    /// </summary>
    public sealed class OrderCardView : MonoBehaviour
    {
        public int Slot { get; private set; }

        [SerializeField] private Image _background;
        [SerializeField] private Image _avatarImage;
        [SerializeField] private Text _avatarText;
        [SerializeField] private List<GameObject> _itemRows = new List<GameObject>();
        [SerializeField] private List<Image> _itemIcons = new List<Image>();
        [SerializeField] private List<GameObject> _itemCheckmarks = new List<GameObject>();
        [SerializeField] private Text _progressText;
        [SerializeField] private Text _rewardText;
        [SerializeField] private Text _rewardXpText;
        [SerializeField] private Text _rewardCoinText;
        [SerializeField] private Button _submitButton;
        [SerializeField] private Button _rewardedVideoButton;
        [SerializeField] private Image _submitImage;
        [SerializeField] private Text _submitLabel;
        [SerializeField] private GameObject _completeBanner;

        private GamePresenter _presenter;

        public Button SubmitButton => _submitButton;
        public Button RewardedVideoButton => _rewardedVideoButton;
        public IReadOnlyList<Image> ItemIcons => _itemIcons;
        public Image AvatarImage => _avatarImage;
        public Text AvatarText => _avatarText;

        [Obsolete("Use Bind on an OrderCard prefab instance.")]
        public void Initialize(GamePresenter presenter, int slot)
        {
            Bind(presenter, slot);
        }

        public void Bind(GamePresenter presenter, int slot)
        {
            ValidateReferences();
            _presenter = presenter;
            Slot = slot;
            name = $"OrderCard_{slot}";

            _submitButton.onClick.RemoveAllListeners();
            _submitButton.onClick.AddListener(() => _presenter?.NotifySubmitOrder(Slot));
            _rewardedVideoButton.onClick.RemoveAllListeners();
            _rewardedVideoButton.onClick.AddListener(() => _presenter?.NotifyRewardedVideoOrder(Slot));
        }

        public void Render(IReadOnlyList<Sprite> itemSprites, IReadOnlyList<bool> itemCompleted,
            string progress, string rewardText, bool fulfilled)
        {
            Render(itemSprites, itemCompleted, progress, rewardText, fulfilled, null, 0, 0);
        }

        public void Render(IReadOnlyList<Sprite> itemSprites, IReadOnlyList<bool> itemCompleted,
            string progress, string rewardText, bool fulfilled, Sprite avatarSprite)
        {
            Render(itemSprites, itemCompleted, progress, rewardText, fulfilled, avatarSprite, 0, 0);
        }

        public void Render(IReadOnlyList<Sprite> itemSprites, IReadOnlyList<bool> itemCompleted,
            string progress, string rewardText, bool fulfilled, Sprite avatarSprite,
            int fullReward, int directReward)
        {
            Render(itemSprites, itemCompleted, progress, rewardText, fulfilled, avatarSprite,
                fullReward, directReward, true);
        }

        public void Render(IReadOnlyList<Sprite> itemSprites, IReadOnlyList<bool> itemCompleted,
            string progress, string rewardText, bool fulfilled, Sprite avatarSprite,
            int fullReward, int directReward, bool requiresRewardedVideo)
        {
            _avatarImage.sprite = avatarSprite;
            _avatarImage.gameObject.SetActive(avatarSprite != null);
            _avatarText.gameObject.SetActive(false);

            int itemCount = Mathf.Min(3, itemSprites == null ? 0 : itemSprites.Count);
            for (int i = 0; i < _itemRows.Count; i++)
            {
                bool active = i < itemCount;
                _itemRows[i].SetActive(active);
                if (!active) continue;

                _itemIcons[i].sprite = itemSprites[i];
                _itemIcons[i].gameObject.SetActive(itemSprites[i] != null);
                bool completed = itemCompleted != null && i < itemCompleted.Count && itemCompleted[i];
                _itemCheckmarks[i].SetActive(completed);
            }

            _progressText.text = progress;
            _rewardText.text = rewardText;
            bool grantsExperience = !string.IsNullOrEmpty(rewardText)
                && rewardText.IndexOf("XP +1", StringComparison.Ordinal) >= 0;
            _rewardXpText.text = grantsExperience ? "+1" : string.Empty;
            _rewardCoinText.text = fullReward > 0 ? $"+{fullReward}" : string.Empty;
            _submitImage.color = fulfilled
                ? new Color(1f, 0.67f, 0.14f, 1f)
                : Color.clear;
            _submitLabel.text = fulfilled
                ? (requiresRewardedVideo
                    ? $"»  ONLY CLAIM  +{(directReward > 0 ? directReward : 1)}"
                    : $"»  CLAIM ALL  +{(fullReward > 0 ? fullReward : 1)}")
                : string.Empty;
            _submitLabel.color = new Color(0.35f, 0.16f, 0.06f, 1f);
            _submitLabel.gameObject.SetActive(fulfilled);
            var rewardedLabel = _rewardedVideoButton.transform.Find("Label")?.GetComponent<Text>();
            if (rewardedLabel != null)
            {
                rewardedLabel.text = fulfilled && requiresRewardedVideo ? "▶  CLAIM ALL" : string.Empty;
            }
            _completeBanner.SetActive(false);
            _submitButton.gameObject.SetActive(fulfilled);
            _rewardedVideoButton.gameObject.SetActive(fulfilled && requiresRewardedVideo);
        }

        private void ValidateReferences()
        {
            if (_background == null) throw new InvalidOperationException("OrderCardView missing Background");
            if (_avatarImage == null) throw new InvalidOperationException("OrderCardView missing AvatarImage");
            if (_avatarText == null) throw new InvalidOperationException("OrderCardView missing AvatarText");
            if (_itemRows == null || _itemRows.Count != 3)
                throw new InvalidOperationException("OrderCardView requires exactly 3 ItemRows");
            if (_itemIcons == null || _itemIcons.Count != 3)
                throw new InvalidOperationException("OrderCardView requires exactly 3 ItemIcons");
            if (_itemCheckmarks == null || _itemCheckmarks.Count != 3)
                throw new InvalidOperationException("OrderCardView requires exactly 3 ItemCheckmarks");
            if (_progressText == null) throw new InvalidOperationException("OrderCardView missing ProgressText");
            if (_rewardText == null) throw new InvalidOperationException("OrderCardView missing RewardText");
            if (_rewardXpText == null) throw new InvalidOperationException("OrderCardView missing RewardXpText");
            if (_rewardCoinText == null) throw new InvalidOperationException("OrderCardView missing RewardCoinText");
            if (_submitButton == null) throw new InvalidOperationException("OrderCardView missing SubmitButton");
            if (_rewardedVideoButton == null) throw new InvalidOperationException("OrderCardView missing RewardedVideoButton");
            if (_submitImage == null) throw new InvalidOperationException("OrderCardView missing SubmitImage");
            if (_submitLabel == null) throw new InvalidOperationException("OrderCardView missing SubmitLabel");
            if (_completeBanner == null) throw new InvalidOperationException("OrderCardView missing CompleteBanner");
        }
    }
}
