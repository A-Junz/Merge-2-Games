using System;
using System.Collections.Generic;
using System.Text;
using MergePrototype.Core;
using MergePrototype.Persistence;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MergePrototype.Presentation
{
    /// <summary>
    /// Prefab UI 与玩法状态之间的绑定层。所有层级、布局和视觉样式来自 GameUi.prefab；
    /// 此类只实例化重复视图、注册交互并刷新动态数据。
    /// </summary>
    public sealed class GamePresenter : MonoBehaviour
    {
        public const float ReferenceWidth = UiLayoutSpec.ReferenceWidth;
        public const float ReferenceHeight = UiLayoutSpec.ReferenceHeight;

        private static readonly Color CardWhite = new Color(0.995f, 0.985f, 0.955f);
        private static readonly Color DarkBrown = new Color(0.32f, 0.22f, 0.13f);
        private static readonly Color ClaimableRewardBg = new Color(0.78f, 0.95f, 0.72f);

        private GameState _state;
        private SaveService _saveService;
        private string _saveError;
        private GameUiView _ui;

        private Text _moneyText;
        private Text _lvText;
        private Text _statusText;
        private Text _levelLabel;
        private Text _levelProgressText;
        private RectTransform _levelFill;

        private readonly List<BoardCellView> _cells = new List<BoardCellView>();
        private readonly List<OrderCardView> _orderCards = new List<OrderCardView>();
        private Button _generatorButton;
        private Button _encyclopediaButton;
        private ScrollRect _orderScrollRect;
        private RectTransform _orderContent;
        private HorizontalLayoutGroup _orderLayout;

        private const float OrderViewportWidth = UiLayoutSpec.OrderViewportWidth;
        private const float OrderCardSpacing = UiLayoutSpec.OrderCardSpacing;
        private const float OrderCardWidth = UiLayoutSpec.OrderCardWidth;

        private RectTransform _canvasRect;
        private RectTransform _dragGhost;
        private Image _dragGhostImage;
        private Image _dragGhostIcon;
        private Text _dragGhostLabel;
        private BoardCellView _dragSource;

        private GameObject _deleteModal;
        private Image _deletePreviewIcon;
        private Text _deletePreviewLabel;
        private GameObject _encyclopediaModal;
        private GameObject _levelUpRewardModal;
        private int _deleteCellIndex = -1;

        public GameState State => _state;
        public IReadOnlyList<BoardCellView> Cells => _cells;
        public IReadOnlyList<OrderCardView> OrderCards => _orderCards;
        public Button GeneratorButton => _generatorButton;
        public Button EncyclopediaButton => _encyclopediaButton;
        public Button RestartButton => _ui == null ? null : _ui.RestartButton;
        public Button AutoCompleteButton => _ui == null ? null : _ui.AutoCompleteButton;

        /// <summary>
        /// 本地激励视频完成回调。默认立即成功；接入广告 SDK 时替换为
        /// SDK 的展示与完成回调，返回 false 表示未完整观看。
        /// </summary>
        public Func<int, bool> RewardedVideoCompletion { get; set; } = _ => true;

        /// <summary>升级结算弹窗的激励视频完成回调。</summary>
        public Func<bool> UpgradeRewardedVideoCompletion { get; set; } = () => true;

        private void Update()
        {
            if (_state == null) return;
            _state.TickEnergy(DateTimeOffset.UtcNow);
            _state.TickOrders(DateTimeOffset.UtcNow);
        }

        public void Initialize(GameState state, SaveService saveService, string loadFallbackReason)
        {
            var ui = GetComponent<GameUiView>();
            if (ui == null)
            {
                var prefab = Resources.Load<GameUiView>("UI/Prefabs/GameUi");
                if (prefab == null)
                    throw new InvalidOperationException("GamePresenter requires Resources/UI/Prefabs/GameUi.prefab");
                ui = Instantiate(prefab, transform);
                // Keep the editable prefab's stable scene name after Unity's
                // Instantiate would otherwise append "(Clone)". Several UI
                // integrations and tooling look up the root by this name.
                ui.name = prefab.name;
            }
            Initialize(state, saveService, loadFallbackReason, ui);
        }

        public void Initialize(GameState state, SaveService saveService, string loadFallbackReason, GameUiView uiView)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (uiView == null) throw new ArgumentNullException(nameof(uiView));
            _saveService = saveService;
            BindUi(uiView);
            BindState(state);

            SetStatus(!string.IsNullOrEmpty(loadFallbackReason)
                ? $"Save data was invalid; started a new game ({loadFallbackReason})"
                : "Welcome! Tap SERVE ITEMS to restock the kitchen.");
            RefreshAll();
        }

        private void BindUi(GameUiView uiView)
        {
            uiView.ValidateReferences();
            _ui = uiView;
            _canvasRect = (RectTransform)uiView.transform;
            _moneyText = uiView.MoneyText;
            _lvText = uiView.LvText;
            _levelLabel = uiView.LevelLabel;
            _levelProgressText = uiView.LevelProgressText;
            _levelFill = uiView.LevelFill;
            _statusText = uiView.StatusText;
            _generatorButton = uiView.GeneratorButton;
            _encyclopediaButton = uiView.EncyclopediaButton;
            _orderScrollRect = uiView.OrderScrollRect;
            _orderContent = uiView.OrderContent;
            _orderLayout = uiView.OrderLayout;
            _dragGhost = uiView.DragGhost.GetComponent<RectTransform>();
            _dragGhostImage = uiView.DragGhostImage;
            _dragGhostIcon = uiView.DragGhostIcon;
            _dragGhostLabel = uiView.DragGhostLabel;
            _deleteModal = uiView.DeleteModal;
            _deletePreviewIcon = uiView.DeletePreviewIcon;
            _deletePreviewLabel = uiView.DeletePreviewLabel;
            _encyclopediaModal = uiView.EncyclopediaModal;
            _levelUpRewardModal = uiView.LevelUpRewardModal;

            BindButton(uiView.CashOutButton, NotifyCashOut);
            BindButton(_generatorButton, NotifyGenerate);
            BindButton(uiView.RestartButton, NotifyRestart);
            BindButton(uiView.AutoCompleteButton, NotifyAutoComplete);
            BindButton(_encyclopediaButton, NotifyToggleEncyclopedia);
            BindModalButtons();
            BindEncyclopediaRewardButtons();

            _orderCards.Clear();
            ClearChildren(_orderContent);
            for (int i = 0; i < OrderModel.SlotCount; i++)
            {
                var card = Instantiate(uiView.OrderCardPrefab, _orderContent);
                card.Bind(this, i);
                ApplyOrderCardLayout(card.transform as RectTransform);
                _orderCards.Add(card);
            }

            _cells.Clear();
            ClearChildren(_ui.BoardGrid);
            for (int i = 0; i < BoardModel.CellCount; i++)
            {
                var cell = Instantiate(uiView.BoardCellPrefab, _ui.BoardGrid);
                cell.Bind(this, i);
                _cells.Add(cell);
            }

            _deleteModal.SetActive(false);
            _encyclopediaModal.SetActive(false);
            _levelUpRewardModal.SetActive(false);
            _dragGhost.gameObject.SetActive(false);
            RefreshEncyclopediaTiles();
        }

        private void BindModalButtons()
        {
            var confirm = FindButton(_deleteModal, "Card/ConfirmDelete");
            var cancel = FindButton(_deleteModal, "Card/CancelDelete");
            var close = FindButton(_encyclopediaModal, "Card/Header/Close")
                ?? FindButton(_encyclopediaModal, "Card/Close");
            var claimAll = FindButton(_levelUpRewardModal, "Card/ClaimAllButton");
            var onlyClaim = FindButton(_levelUpRewardModal, "Card/OnlyClaimButton");
            BindButton(confirm, NotifyConfirmDelete);
            BindButton(cancel, NotifyCancelDelete);
            BindButton(close, NotifyToggleEncyclopedia);
            BindButton(claimAll, NotifyClaimAllLevelUpReward);
            BindButton(onlyClaim, NotifyOnlyClaimLevelUpReward);
        }

        private void BindEncyclopediaRewardButtons()
        {
            var summary = _encyclopediaModal == null
                ? null
                : _encyclopediaModal.transform.Find("Card/Summary");
            var allRewardButton = EnsureEncyclopediaAllRewardButton(summary);
            BindButton(allRewardButton, NotifyClaimEncyclopediaAllReward);

            var content = _encyclopediaModal == null
                ? null
                : _encyclopediaModal.transform.Find("Card/ChainScroll/Viewport/Content");
            if (content == null) return;

            for (int i = 0; i < content.childCount; i++)
            {
                var chain = content.GetChild(i);
                if (!chain.name.StartsWith("Chain_", StringComparison.Ordinal)) continue;
                var button = chain.Find("RewardButton")?.GetComponent<Button>();
                if (button == null) continue;
                string chainId = chain.name.Substring("Chain_".Length);
                BindButton(button, () => NotifyClaimEncyclopediaReward(chainId));
            }
        }

        private static Button FindButton(GameObject root, string path)
        {
            var node = root == null ? null : root.transform.Find(path);
            return node == null ? null : node.GetComponent<Button>();
        }

        private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private static void ClearChildren(RectTransform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
                Destroy(root.GetChild(i).gameObject);
        }

        private void BindState(GameState state)
        {
            if (_state != null) _state.StateChanged -= OnStateChanged;
            _state = state;
            _state.StateChanged += OnStateChanged;
        }

        private void OnStateChanged()
        {
            TrySave();
            RefreshAll();
        }

        private void TrySave()
        {
            if (_saveService == null) return;
            if (_saveService.Save(_state, out string error))
            {
                _saveError = null;
            }
            else
            {
                _saveError = $"Save failed: {error}";
                SetStatus(_saveError);
            }
        }

        public void NotifyGenerate()
        {
            int emptyBefore = _state.Board.GetEmptyIndices().Count;
            if (_state.TryGenerate(out string error))
                SetStatus($"SERVE ITEMS complete: filled {emptyBefore} empty slots!");
            else
                SetStatus(error);

            RefreshAll();
            foreach (int cellIndex in _state.LastLuckyGeneratedCellIndices)
            {
                if (cellIndex >= 0 && cellIndex < _cells.Count)
                    _cells[cellIndex].ShowLucky();
            }
        }

        public void NotifySubmitOrder(int slot)
        {
            int levelBefore = _state.Level;
            if (_state.TrySubmitOrder(slot, out int reward, out string error))
            {
                SetStatus(_state.Level > levelBefore
                    ? $"Order complete +{reward} coins! LEVEL {_state.Level} reached!"
                    : $"Order complete: +{reward} coins!");
            }
            else
            {
                SetStatus(error);
            }
            RefreshAll();
        }

        public void NotifyRewardedVideoOrder(int slot)
        {
            int levelBefore = _state.Level;
            if (RewardedVideoCompletion == null || !RewardedVideoCompletion(slot))
            {
                SetStatus("Rewarded video was not completed.");
                RefreshAll();
                return;
            }

            if (_state.TrySubmitOrderWithRewardedVideo(slot, out int reward, out string error))
            {
                SetStatus(_state.Level > levelBefore
                    ? $"Rewarded video complete +{reward} coins! LEVEL {_state.Level} reached!"
                    : $"Rewarded video complete: +{reward} coins!");
            }
            else
            {
                SetStatus(error);
            }
            RefreshAll();
        }

        public void NotifyClaimAllLevelUpReward()
        {
            if (UpgradeRewardedVideoCompletion == null || !UpgradeRewardedVideoCompletion())
            {
                SetStatus("Rewarded video was not completed.");
                RefreshAll();
                return;
            }

            if (_state.TryClaimPendingLevelUpReward(OrderRewardClaimMode.RewardedVideo,
                out int reward, out string error))
                SetStatus($"Claim All complete: +{reward} coins!");
            else
                SetStatus(error);
            RefreshAll();
        }

        public void NotifyOnlyClaimLevelUpReward()
        {
            if (_state.TryClaimPendingLevelUpReward(OrderRewardClaimMode.Direct,
                out int reward, out string error))
                SetStatus($"Only claim complete: +{reward} coins.");
            else
                SetStatus(error);
            RefreshAll();
        }

        public void NotifySpecialCashOut(BoardCellView cell)
        {
            if (_state.TryCashOutSpecialItem(cell.CellIndex, out int amount, out string error))
                SetStatus($"Special cash item redeemed: +{amount} coins!");
            else
                SetStatus(error);
            RefreshAll();
        }

        public void NotifyCellLongPress(BoardCellView cell)
        {
            if (cell == null || _state == null) return;
            int itemId = _state.Board.GetItemAt(cell.CellIndex);
            if (itemId == ItemCatalog.EmptyId) return;

            var def = _state.Catalog.Get(itemId);
            _deleteCellIndex = cell.CellIndex;
            _deletePreviewIcon.sprite = FoodChainIconProvider.Get(def);
            _deletePreviewIcon.gameObject.SetActive(_deletePreviewIcon.sprite != null);
            _deletePreviewLabel.text = def.DisplayName;
            _encyclopediaModal.SetActive(false);
            _deleteModal.SetActive(true);
        }

        public void NotifyCancelDelete()
        {
            _deleteCellIndex = -1;
            _deleteModal.SetActive(false);
        }

        public void NotifyConfirmDelete()
        {
            if (_deleteCellIndex < 0 || _state == null)
            {
                NotifyCancelDelete();
                return;
            }

            int cellIndex = _deleteCellIndex;
            if (_state.TryDeleteItem(cellIndex, out string error))
                SetStatus("Item deleted");
            else
                SetStatus(error);
            NotifyCancelDelete();
            RefreshAll();
        }

        public void NotifyToggleEncyclopedia()
        {
            bool show = !_encyclopediaModal.activeSelf;
            _deleteModal.SetActive(false);
            _deleteCellIndex = -1;
            _encyclopediaModal.SetActive(show);
            if (show)
            {
                RefreshEncyclopediaTiles();
                RebuildEncyclopediaLayout();
            }
        }

        private void RebuildEncyclopediaLayout()
        {
            if (_encyclopediaModal == null) return;
            var scroll = _encyclopediaModal.transform.Find("Card/ChainScroll")?.GetComponent<ScrollRect>();
            var content = scroll == null ? null : scroll.content;
            if (content == null) return;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            if (scroll.viewport != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.viewport);
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1f;
        }

        public void NotifyCashOut()
        {
            SetStatus($"CASH OUT: {FormatMoney(_state.Coins)} credited (demo)");
        }

        public void NotifyRestart()
        {
            BindState(GameState.CreateNew(_state.Variant));
            TrySave();
            SetStatus("Game restarted!");
            RefreshAll();
        }

        public void NotifyAutoComplete()
        {
            if (_state == null) return;
            int actions = _state.RunAutoComplete(512,
                out int mergeCount, out int submittedOrderCount, out int generatedBatchCount);
            SetStatus(actions == 0
                ? "AUTO: no automatic action available."
                : $"AUTO: merged {mergeCount}, submitted {submittedOrderCount}, served {generatedBatchCount}.");
            RefreshAll();
        }

        public void NotifyCellBeginDrag(BoardCellView cell, PointerEventData eventData)
        {
            if (cell == null || _state.Board.GetUnlockState(cell.CellIndex) != CellUnlockState.Unlocked)
                return;
            int itemId = _state.Board.GetItemAt(cell.CellIndex);
            if (itemId == ItemCatalog.EmptyId) return;
            _dragSource = cell;
            var def = _state.Catalog.Get(itemId);
            _dragGhostImage.color = def != null
                ? new Color(def.ColorR, def.ColorG, def.ColorB)
                : CardWhite;
            _dragGhostIcon.sprite = def != null ? FoodChainIconProvider.Get(def) : null;
            _dragGhostIcon.gameObject.SetActive(_dragGhostIcon.sprite != null);
            _dragGhostLabel.text = def != null && _dragGhostIcon.sprite == null ? def.Level.ToString() : string.Empty;
            _dragGhost.gameObject.SetActive(true);
            _dragGhost.SetAsLastSibling();
            UpdateGhostPosition(eventData);
            cell.SetDragSourceHighlight(true);
        }

        public void NotifyCellDrag(BoardCellView cell, PointerEventData eventData)
        {
            if (_dragSource != null) UpdateGhostPosition(eventData);
        }

        public void NotifyCellEndDrag(BoardCellView cell, PointerEventData eventData)
        {
            if (_dragSource == null) return;
            int sourceIndex = _dragSource.CellIndex;
            _dragSource.SetDragSourceHighlight(false);
            _dragSource = null;
            _dragGhost.gameObject.SetActive(false);

            int targetIndex = FindCellIndexAt(eventData.position);
            if (targetIndex < 0)
            {
                RefreshAll();
                return;
            }

            var result = _state.HandleDrop(sourceIndex, targetIndex);
            if (result.Type == DropResultType.Merged || result.Type == DropResultType.Rejected)
            {
                if (!string.IsNullOrEmpty(result.Message)) SetStatus(result.Message);
            }
            RefreshAll();
        }

        private void UpdateGhostPosition(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect, eventData.position, eventData.pressEventCamera, out Vector2 local);
            _dragGhost.anchoredPosition = local;
        }

        private int FindCellIndexAt(Vector2 screenPosition)
        {
            if (EventSystem.current == null) return -1;
            var pointer = new PointerEventData(EventSystem.current) { position = screenPosition };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, results);
            foreach (var hit in results)
            {
                var cell = hit.gameObject.GetComponentInParent<BoardCellView>();
                if (cell != null) return cell.CellIndex;
            }
            return -1;
        }

        public void SetStatus(string message)
        {
            if (_statusText == null) return;
            if (!string.IsNullOrEmpty(_saveError) && !string.Equals(message, _saveError, StringComparison.Ordinal))
            {
                _statusText.text = _saveError;
                return;
            }
            _statusText.text = message ?? string.Empty;
        }

        public void RefreshAll()
        {
            if (_state == null) return;
            RefreshStatusBar();
            RefreshBoard();
            RefreshOrders();
            RefreshEncyclopediaTiles();
            RefreshLevelUpRewardModal();
        }

        private void RefreshLevelUpRewardModal()
        {
            if (_levelUpRewardModal == null) return;
            bool pending = _state.HasPendingLevelUpReward;
            if (pending)
            {
                _deleteModal.SetActive(false);
                _encyclopediaModal.SetActive(false);
                _levelUpRewardModal.SetActive(true);
                var amount = _levelUpRewardModal.transform.Find("Card/RewardArea/RewardAmount")
                    ?.GetComponent<Text>();
                var direct = _levelUpRewardModal.transform.Find("Card/OnlyClaimButton/Label")
                    ?.GetComponent<Text>();
                if (amount != null) amount.text = $"+{_state.PendingLevelUpReward}";
                if (direct != null) direct.text = $"Only claim +{_state.PendingLevelUpDirectReward}";
            }
            else
            {
                _levelUpRewardModal.SetActive(false);
            }
        }

        private void RefreshStatusBar()
        {
            _moneyText.text = FormatCoinAmount(_state.Coins);
            _lvText.text = _state.Level >= _state.VariantConfig.MaxPlayerLevel
                ? $"✓ LV {_state.Level} MAX"
                : $"✓ LV {_state.Level} » LV {_state.Level + 1}";
            _levelLabel.text = $"LEVEL {_state.Level}";
            _levelProgressText.text = $"{_state.LevelProgress}/{_state.CurrentLevelExperienceRequirement}";

            float fraction = (float)_state.LevelProgress / _state.CurrentLevelExperienceRequirement;
            const float fillStart = 0.16f;
            const float fillEnd = 0.875f;
            float fillMax = fillStart + (fillEnd - fillStart) * Mathf.Clamp01(fraction);
            _levelFill.anchorMin = new Vector2(fillStart, 0.31f);
            _levelFill.anchorMax = new Vector2(Mathf.Max(fillMax, fillStart + 0.001f), 0.59f);
            _levelFill.offsetMax = new Vector2(fraction >= 1f ? -4f : 0f, -2f);
        }

        private void RefreshBoard()
        {
            var orderRequirements = new HashSet<int>();
            for (int slot = 0; slot < _state.Orders.ActiveCount; slot++)
            {
                var order = _state.Orders.GetOrder(slot);
                if (order == null) continue;
                foreach (int requiredId in order.GetRequiredItemIds())
                    orderRequirements.Add(requiredId);
            }

            for (int i = 0; i < _cells.Count; i++)
            {
                int itemId = _state.Board.GetItemAt(i);
                if (itemId == ItemCatalog.EmptyId)
                    _cells[i].Render(0, string.Empty, string.Empty, default);
                else
                {
                    var def = _state.Catalog.Get(itemId);
                    bool isSpecial = _state.Catalog.IsSpecialItem(itemId);
                    string displayName = isSpecial
                        ? _state.Catalog.GetSpecialCashoutAmount(itemId).ToString()
                        : def.DisplayName;
                    _cells[i].Render(itemId, isSpecial ? string.Empty : def.Emoji,
                        isSpecial ? FoodChainIconProvider.GetCurrencyChip() : FoodChainIconProvider.Get(def),
                        displayName, new Color(def.ColorR, def.ColorG, def.ColorB), isSpecial);
                }

                _cells[i].SetUnlockState(_state.Board.GetUnlockState(i));

                bool fullyUnlocked = _state.Board.GetUnlockState(i) == CellUnlockState.Unlocked;
                _cells[i].SetOrderSatisfiedMarker(
                    fullyUnlocked && itemId != ItemCatalog.EmptyId && orderRequirements.Contains(itemId));
            }
        }

        private void RefreshOrders()
        {
            var displaySlots = new List<int>(_state.Orders.ActiveCount);
            for (int slot = 0; slot < _state.Orders.ActiveCount; slot++)
                displaySlots.Add(slot);

            // Surface the most actionable order first while keeping model slot
            // indices stable for submission/save data.
            displaySlots.Sort((left, right) =>
            {
                bool leftComplete = _state.Orders.IsFulfilled(left, _state.Board);
                bool rightComplete = _state.Orders.IsFulfilled(right, _state.Board);
                int leftProgress = _state.Orders.GetProgress(left, _state.Board);
                int rightProgress = _state.Orders.GetProgress(right, _state.Board);
                return OrderLayoutRules.ComparePriority(
                    leftComplete, leftProgress, left,
                    rightComplete, rightProgress, right);
            });

            for (int i = 0; i < _orderCards.Count; i++)
            {
                if (i >= displaySlots.Count)
                {
                    _orderCards[i].gameObject.SetActive(false);
                    continue;
                }

                int slot = displaySlots[i];
                var order = _state.Orders.GetOrder(slot);
                if (order == null)
                {
                    _orderCards[i].gameObject.SetActive(false);
                    continue;
                }

                _orderCards[i].gameObject.SetActive(true);
                _orderCards[i].Bind(this, slot);
                var ids = order.GetRequiredItemIds();
                var itemSprites = new List<Sprite>();
                var itemCompleted = new List<bool>();
                int fulfilledCount = 0;
                for (int j = 0; j < ids.Count; j++)
                {
                    var def = _state.Catalog.Get(ids[j]);
                    itemSprites.Add(FoodChainIconProvider.Get(def));
                    bool completed = _state.Board.CountItemInFullyUnlockedCells(ids[j]) > 0;
                    itemCompleted.Add(completed);
                    if (completed) fulfilledCount++;
                }
                string progress = $"Progress {fulfilledCount}/{ids.Count}";
                int fullReward = order.RewardCoins(_state.Catalog);
                int directReward = OrderRewardRules.CalculateDirectReward(fullReward);
                string reward = order.GrantsExperience
                    ? $"XP +1    Coins +{fullReward}"
                    : $"Coins only    +{fullReward}";
                _orderCards[i].Render(itemSprites, itemCompleted, progress, reward,
                    _state.Orders.IsFulfilled(slot, _state.Board),
                    OrderAvatarSpriteProvider.Get(order.CharacterId), fullReward, directReward,
                    order.RequiresRewardedVideo);
            }

            bool scrollable = OrderLayoutRules.IsScrollable(_state.Orders.ActiveCount);
            _orderScrollRect.horizontal = scrollable;
            _orderScrollRect.enabled = scrollable;
            _orderLayout.childAlignment = scrollable ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            float contentWidth = scrollable
                ? _state.Orders.ActiveCount * OrderCardWidth + Math.Max(0, _state.Orders.ActiveCount - 1) * OrderCardSpacing
                : OrderViewportWidth;
            _orderContent.sizeDelta = new Vector2(contentWidth, 0f);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_orderContent);
            for (int i = 0; i < _orderCards.Count; i++)
                ApplyOrderCardLayout(_orderCards[i].transform as RectTransform);
            // Keep the cards at the same 20px visual inset while the viewport itself spans the full section.
            float contentInset = Mathf.Max(0f, (ReferenceWidth - OrderViewportWidth) * 0.5f);
            // Do not overwrite the user's current horizontal scroll position after a board update.
            if (!scrollable)
                _orderContent.anchoredPosition = new Vector2(contentInset, 0f);
        }

        private static void ApplyOrderCardLayout(RectTransform cardRect)
        {
            if (cardRect == null) return;
            cardRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, UiLayoutSpec.OrderCardWidth);
            cardRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, UiLayoutSpec.OrderCardHeight);
        }

        private void RefreshEncyclopediaTiles()
        {
            var card = _encyclopediaModal.transform.Find("Card");
            if (card == null) return;
            ConfigureEncyclopediaHeader(card);

            var chainContent = card.Find("ChainScroll/Viewport/Content");
            if (chainContent != null)
            {
                RefreshEncyclopediaChainTiles(chainContent);
                return;
            }

            // 兼容旧版 GameUi.prefab：重新生成 Prefab 后会走上面的分链布局。
            var grid = card.Find("IconGrid");
            if (grid == null) return;
            var gridLayout = grid.GetComponent<GridLayoutGroup>();
            if (gridLayout != null)
            {
                gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                gridLayout.constraintCount = 8;
                gridLayout.cellSize = new Vector2(104f, 100f);
                gridLayout.spacing = new Vector2(8f, 8f);
            }
            var catalog = _state == null ? new ItemCatalog() : _state.Catalog;
            foreach (var def in catalog.AllItems)
            {
                if (def.ChainId == "special") continue;
                var tile = grid.Find($"Catalog_{def.Id}");
                if (tile == null) continue;
                RefreshEncyclopediaTile(def, tile);
            }
        }

        private void RefreshEncyclopediaChainTiles(Transform content)
        {
            var catalog = _state == null ? new ItemCatalog() : _state.Catalog;
            var availableChainIds = new HashSet<string>();
            foreach (var def in catalog.AllItems)
            {
                if (def.ChainId != "special") availableChainIds.Add(def.ChainId);
            }

            for (int i = 0; i < content.childCount; i++)
            {
                var chain = content.GetChild(i);
                if (!chain.name.StartsWith("Chain_", StringComparison.Ordinal)) continue;
                string chainId = chain.name.Substring("Chain_".Length);
                chain.gameObject.SetActive(availableChainIds.Contains(chainId));

                var grid = chain.Find("Grid");
                if (grid == null) continue;
                for (int j = 0; j < grid.childCount; j++)
                {
                    var tile = grid.GetChild(j);
                    if (!tile.name.StartsWith("Catalog_", StringComparison.Ordinal)) continue;
                    if (!int.TryParse(tile.name.Substring("Catalog_".Length), out int itemId)) continue;
                    tile.gameObject.SetActive(catalog.IsAvailableItem(itemId));
                }
            }

            foreach (var def in catalog.AllItems)
            {
                if (def.ChainId == "special") continue;
                var chain = content.Find($"Chain_{def.ChainId}");
                if (chain == null) continue;
                var chainTitle = chain.Find("Title")?.GetComponent<Text>();
                if (chainTitle != null)
                    chainTitle.text = GetEncyclopediaChainTitle(def.ChainId);
                RefreshEncyclopediaChainReward(def.ChainId, chain);

                var tile = chain.Find($"Grid/Catalog_{def.Id}");
                if (tile != null) RefreshEncyclopediaTile(def, tile);
                if (def.Level == 1)
                {
                    var chainIcon = chain.Find("Icon")?.GetComponent<Image>();
                    if (chainIcon != null)
                    {
                        chainIcon.sprite = FoodChainIconProvider.Get(def);
                        chainIcon.gameObject.SetActive(chainIcon.sprite != null);
                    }
                }
            }
        }

        private void RefreshEncyclopediaChainReward(string chainId, Transform chain)
        {
            var button = chain.Find("RewardButton")?.GetComponent<Button>();
            if (button == null) return;

            bool claimed = _state != null && _state.IsEncyclopediaChainRewardClaimed(chainId);
            bool claimable = _state != null && _state.CanClaimEncyclopediaChainReward(chainId);
            var background = button.GetComponent<Image>();
            if (background != null)
                background.color = claimable ? ClaimableRewardBg : Color.clear;
            button.interactable = claimable;

            var icon = chain.Find("RewardButton/RewardIcon")?.gameObject;
            var amount = chain.Find("RewardButton/RewardText")?.GetComponent<Text>();
            var check = chain.Find("RewardButton/ClaimedIcon")?.GetComponent<Text>();
            if (icon != null) icon.SetActive(!claimed);
            if (amount != null)
            {
                amount.text = $"+{EncyclopediaRewardRules.ChainCompletionReward}";
                amount.gameObject.SetActive(!claimed);
            }
            if (check != null)
            {
                check.text = "✓";
                check.gameObject.SetActive(claimed);
            }
        }

        private void NotifyClaimEncyclopediaReward(string chainId)
        {
            if (_state == null) return;
            if (_state.TryClaimEncyclopediaChainReward(chainId, out int reward, out string error))
                SetStatus($"Claimed +{reward} coins from {GetEncyclopediaChainTitle(chainId)}!");
            else if (!string.IsNullOrEmpty(error))
                SetStatus(error);
            RefreshAll();
        }

        private void NotifyClaimEncyclopediaAllReward()
        {
            if (_state == null) return;
            if (_state.TryClaimEncyclopediaAllReward(out int reward, out string error))
                SetStatus($"Collection complete: +{reward} coins!");
            else if (!string.IsNullOrEmpty(error))
                SetStatus(error);
            RefreshAll();
        }

        private void RefreshEncyclopediaTile(ItemDef def, Transform tile)
        {
            if (def == null || tile == null) return;
            bool unlocked = _state != null && _state.HasSeenItem(def.Id);
            var tileImage = tile.GetComponent<Image>();
            if (tileImage != null)
            {
                tileImage.color = unlocked
                    ? new Color(0.68f, 0.84f, 0.98f, 1f)
                    : new Color(0.90f, 0.72f, 0.50f, 1f);
            }

            var icon = tile.Find("Icon")?.GetComponent<Image>();
            if (icon != null)
            {
                icon.sprite = FoodChainIconProvider.Get(def);
                icon.gameObject.SetActive(unlocked && icon.sprite != null);
            }

            var name = tile.Find("Name")?.GetComponent<Text>();
            if (name == null) return;
            name.color = unlocked ? DarkBrown : new Color(1f, 0.95f, 0.82f);
            name.fontStyle = FontStyle.Bold;
            if (!unlocked)
            {
                name.text = "?";
                name.fontSize = 30;
                return;
            }

            string reward = def.Level > 1 ? "\n+1" : string.Empty;
            int maxRegularLevel = _state == null ? ItemCatalog.MaxLevel : _state.Catalog.MaxRegularLevel;
            if (def.Level == maxRegularLevel
                && _state != null
                && _state.IsEncyclopediaChainCompleted(def.ChainId))
                reward += "\n+10";
            name.text = def.Level == 1
                ? $"{GetEncyclopediaChainTitle(def.ChainId)}{reward}"
                : $"{def.DisplayName}{reward}";
            name.fontSize = 14;
        }

        private void ConfigureEncyclopediaHeader(Transform card)
        {
            var header = card.Find("Header");
            var title = (header == null ? null : header.Find("Title"))?.GetComponent<Text>()
                ?? card.Find("Title")?.GetComponent<Text>();
            if (title != null)
            {
                title.text = "KITCHEN COLLECTION";
                title.fontStyle = FontStyle.Bold;
            }

            var bookParent = header == null ? card : header;
            var book = bookParent.Find("Book")?.GetComponent<Image>();
            if (book == null)
            {
                book = EnsureEncyclopediaImage(bookParent, "Book");
                SetAnchors(book.rectTransform, 0.04f, 0.84f, 0.16f, 0.98f);
            }
            book.sprite = Resources.Load<Sprite>("UI/EncyclopediaGenerated/encyclopedia_closed_recipe_book")
                ?? Resources.Load<Sprite>("UI/ui_collection_closed_book");
            book.preserveAspect = true;

            var summary = card.Find("Summary");
            var progress = (summary == null ? null : summary.Find("ProgressText"))?.GetComponent<Text>()
                ?? card.Find("ProgressSummary")?.GetComponent<Text>();
            if (progress == null)
            {
                progress = EnsureEncyclopediaText(card, "ProgressSummary", 26,
                    DarkBrown, TextAnchor.MiddleLeft);
                SetAnchors(progress.rectTransform, 0.18f, 0.80f, 0.62f, 0.86f);
            }
            int unlocked = _state == null ? 0 : _state.EncyclopediaUnlockedElementCount;
            int totalElements = _state == null
                ? ItemCatalog.ChainCount * ItemCatalog.MaxLevel
                : _state.Catalog.RegularChainCount * _state.Catalog.MaxRegularLevel;
            progress.text = $"{unlocked}/{totalElements}";
            progress.fontStyle = FontStyle.Bold;

            var reward = (summary == null ? null : summary.Find("RewardText"))?.GetComponent<Text>()
                ?? card.Find("RewardSummary")?.GetComponent<Text>();
            if (reward == null)
            {
                reward = EnsureEncyclopediaText(card, "RewardSummary", 22,
                    new Color(0.12f, 0.48f, 0.12f), TextAnchor.MiddleRight);
                SetAnchors(reward.rectTransform, 0.62f, 0.80f, 0.92f, 0.86f);
            }
            reward.text = $"+{EncyclopediaRewardRules.AllChainsCompletionReward}";
            reward.fontStyle = FontStyle.Bold;
            reward.raycastTarget = false;

            var rewardIconGraphic = summary?.Find("RewardIcon")?.GetComponent<Graphic>();
            if (rewardIconGraphic != null) rewardIconGraphic.raycastTarget = false;

            var allRewardButton = EnsureEncyclopediaAllRewardButton(summary);
            if (allRewardButton != null)
                allRewardButton.interactable = _state != null && _state.CanClaimEncyclopediaAllReward();

            var fill = card.Find("Summary/ProgressTrack/Fill")?.GetComponent<Image>();
            if (fill != null)
            {
                float fraction = totalElements <= 0 ? 0f : (float)unlocked / totalElements;
                fill.rectTransform.anchorMin = new Vector2(0.02f, 0.14f);
                fill.rectTransform.anchorMax = new Vector2(0.02f + 0.96f * Mathf.Clamp01(fraction), 0.86f);
                fill.rectTransform.offsetMin = Vector2.zero;
                fill.rectTransform.offsetMax = Vector2.zero;
            }
        }

        private static Button EnsureEncyclopediaAllRewardButton(Transform summary)
        {
            if (summary == null) return null;
            var button = summary.GetComponent<Button>();
            if (button == null) button = summary.gameObject.AddComponent<Button>();
            var image = summary.GetComponent<Image>();
            if (image == null) image = summary.gameObject.AddComponent<Image>();
            button.targetGraphic = image;
            return button;
        }

        private static Image EnsureEncyclopediaImage(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                var image = existing.GetComponent<Image>();
                if (image != null) return image;
            }

            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            return go.GetComponent<Image>();
        }

        private static Text EnsureEncyclopediaText(Transform parent, string name, int fontSize,
            Color color, TextAnchor alignment)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                var text = existing.GetComponent<Text>();
                if (text != null) return text;
            }

            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var result = go.GetComponent<Text>();
            result.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            result.fontSize = fontSize;
            result.color = color;
            result.alignment = alignment;
            result.horizontalOverflow = HorizontalWrapMode.Overflow;
            result.verticalOverflow = VerticalWrapMode.Overflow;
            result.raycastTarget = false;
            return result;
        }

        private static void SetAnchors(RectTransform rect, float minX, float minY, float maxX, float maxY)
        {
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static string GetEncyclopediaChainTitle(string chainId)
        {
            switch (chainId)
            {
                case "bread": return "BREAD";
                case "pizza": return "PIZZA";
                case "fruit": return "FRUIT";
                case "sushi": return "SUSHI";
                case "noodles": return "NOODLES";
                case "cookware": return "COOKWARE";
                case "dessert": return "DESSERT";
                case "drink": return "DRINKS";
                case "breakfast": return "BREAKFAST";
                case "sauce": return "SAUCES";
                default: return chainId.ToUpperInvariant();
            }
        }

        internal static string FormatMoney(int coins)
        {
            return "Rp" + FormatCoinAmount(coins);
        }

        internal static string FormatCoinAmount(int coins)
        {
            string digits = Mathf.Max(0, coins).ToString();
            var sb = new StringBuilder();
            int first = digits.Length % 3;
            for (int i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (i - first) % 3 == 0) sb.Append('.');
                sb.Append(digits[i]);
            }
            return sb.ToString();
        }
    }
}
