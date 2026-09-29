using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MergePrototype.Presentation
{
    /// <summary>
    /// 棋盘格 Prefab 的数据视图。层级与样式来自 BoardCell.prefab；此类只处理输入和状态刷新。
    /// </summary>
    public sealed class BoardCellView : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerClickHandler, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public int CellIndex { get; private set; }

        [SerializeField] private Image _background;
        [SerializeField] private Image _iconImage;
        [SerializeField] private Text _emojiLabel;
        [SerializeField] private Text _nameLabel;
        [SerializeField] private GameObject _luckyMarker;
        [SerializeField] private CanvasGroup _luckyCanvasGroup;
        [SerializeField] private GameObject _crownMarker;
        [SerializeField] private Sprite _emptyCellSprite;
        [SerializeField] private GameObject _semiLockOverlay;
        [SerializeField] private GameObject _fullLockOverlay;

        private GamePresenter _presenter;
        private Coroutine _luckyRoutine;
        private Image _markerImage;
        private Text _markerLabel;
        private bool _pointerHeld;
        private bool _longPressTriggered;
        private bool _orderSatisfiedMarker;
        private float _pointerDownAt;
        private float _lastTapAt = -1f;
        private bool _hasPreviousTap;
        private bool _hasItem;

        private const float LongPressSeconds = 0.55f;
        private const float DoubleTapSeconds = 0.35f;

        private static readonly Color EmptyColor = new Color(0.56f, 0.75f, 0.92f);
        private static readonly Color BorderFlash = new Color(0.45f, 0.65f, 0.85f);

        public Image Background => _background;
        public Image IconImage => _iconImage;
        public GameObject SemiLockOverlay => _semiLockOverlay;
        public GameObject FullLockOverlay => _fullLockOverlay;

        [Obsolete("Use Bind on a BoardCell prefab instance.")]
        public void Initialize(GamePresenter presenter, int cellIndex)
        {
            Bind(presenter, cellIndex);
        }

        public void Bind(GamePresenter presenter, int cellIndex)
        {
            ValidateReferences();
            _presenter = presenter;
            CellIndex = cellIndex;
            name = $"BoardCell_{cellIndex}";
            _markerImage = _luckyMarker.GetComponent<Image>();
            _markerLabel = _luckyMarker.transform.Find("Label")?.GetComponent<Text>();

            // The background is the cell's only Graphic. Keep it as the UI raycast
            // target so drag, click, and long-press events reach this component even
            // when the icon and labels are deliberately non-interactive.
            _background.raycastTarget = true;
        }

        private void Update()
        {
            if (!_pointerHeld || _longPressTriggered) return;
            if (Time.unscaledTime - _pointerDownAt < LongPressSeconds) return;

            _pointerHeld = false;
            _longPressTriggered = true;
            _presenter?.NotifyCellLongPress(this);
        }

        public void Render(int itemId, string iconText, string displayName, Color color)
        {
            Render(itemId, iconText, null, displayName, color, false);
        }

        public void Render(int itemId, string iconText, Sprite iconSprite, string displayName, Color color)
        {
            Render(itemId, iconText, iconSprite, displayName, color, false);
        }

        public void Render(int itemId, string iconText, Sprite iconSprite, string displayName,
            Color color, bool showName)
        {
            _hasPreviousTap = false;
            _hasItem = itemId != 0;
            if (_crownMarker != null)
                _crownMarker.SetActive(itemId == MergePrototype.Core.ItemCatalog.SpecialLevelFiveId);
            if (!_hasItem)
            {
                HideLucky();
                _background.color = _emptyCellSprite != null ? Color.white : EmptyColor;
                _background.sprite = _emptyCellSprite;
                _background.type = Image.Type.Sliced;
                _iconImage.sprite = null;
                _iconImage.type = Image.Type.Simple;
                _iconImage.fillAmount = 1f;
                _iconImage.gameObject.SetActive(false);
                _emojiLabel.text = string.Empty;
                _emojiLabel.gameObject.SetActive(false);
                _nameLabel.text = string.Empty;
                _nameLabel.gameObject.SetActive(false);
            }
            else
            {
                _background.color = color;
                _background.sprite = null;
                _background.type = Image.Type.Simple;
                _iconImage.sprite = iconSprite;
                bool isSpecialCashItem = showName && iconSprite != null;
                _iconImage.type = isSpecialCashItem ? Image.Type.Filled : Image.Type.Simple;
                _iconImage.fillMethod = Image.FillMethod.Horizontal;
                _iconImage.fillOrigin = 0;
                _iconImage.fillAmount = isSpecialCashItem ? 0.52f : 1f;
                _iconImage.gameObject.SetActive(iconSprite != null);
                _emojiLabel.text = iconText;
                _emojiLabel.gameObject.SetActive(iconSprite == null);
                // Keep ordinary cells icon-first. Special cash items use the name
                // row as their explicit amount label because their icon is only a
                // generic money marker.
                _nameLabel.text = displayName;
                _nameLabel.gameObject.SetActive(showName && !string.IsNullOrEmpty(displayName));
            }
        }

        public void SetUnlockState(MergePrototype.Core.CellUnlockState state)
        {
            if (_semiLockOverlay != null)
                _semiLockOverlay.SetActive(state == MergePrototype.Core.CellUnlockState.SemiUnlocked);
            if (_fullLockOverlay != null)
                _fullLockOverlay.SetActive(state == MergePrototype.Core.CellUnlockState.Locked);
        }

        public void SetDragSourceHighlight(bool active)
        {
            if (_background != null && !_hasItem)
                _background.color = active ? BorderFlash : (_emptyCellSprite != null ? Color.white : EmptyColor);
        }

        public void SetOrderSatisfiedMarker(bool active)
        {
            if (_luckyMarker == null) return;
            _orderSatisfiedMarker = active;
            if (!active)
            {
                HideLucky();
                return;
            }

            if (_luckyRoutine != null)
            {
                StopCoroutine(_luckyRoutine);
                _luckyRoutine = null;
            }

            ApplyOrderSatisfiedMarker();
        }

        private void ApplyOrderSatisfiedMarker()
        {
            _markerImage.color = new Color(0.30f, 0.72f, 0.32f, 0.96f);
            _markerLabel.text = "✓";
            _markerLabel.fontSize = 26;
            _luckyMarker.SetActive(true);
            _luckyCanvasGroup.alpha = 1f;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _pointerHeld = false;
            _presenter?.NotifyCellBeginDrag(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            _presenter?.NotifyCellDrag(this, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _presenter?.NotifyCellEndDrag(this, eventData);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_longPressTriggered)
            {
                _longPressTriggered = false;
                _hasPreviousTap = false;
                return;
            }

            if (!_hasItem) return;

            // StandaloneInputModule does not reliably increment clickCount for
            // two separate touch taps. Keep a small time-based fallback so
            // mobile double-tap gestures reach the same cash-out action.
            bool isDoubleTap = eventData.clickCount >= 2
                || (_hasPreviousTap && Time.unscaledTime - _lastTapAt <= DoubleTapSeconds);
            if (isDoubleTap)
            {
                _hasPreviousTap = false;
                _presenter?.NotifySpecialCashOut(this);
            }
            else
            {
                _hasPreviousTap = true;
                _lastTapAt = Time.unscaledTime;
            }
            _longPressTriggered = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pointerHeld = true;
            _longPressTriggered = false;
            _pointerDownAt = Time.unscaledTime;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pointerHeld = false;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _pointerHeld = false;
        }

        public void ShowLucky()
        {
            if (_luckyMarker == null) return;
            _markerImage.color = new Color(1f, 0.70f, 0.08f, 0.96f);
            _markerLabel.text = "LUCKY!";
            _markerLabel.fontSize = 16;
            _luckyMarker.SetActive(true);
            _luckyCanvasGroup.alpha = 1f;
            if (_luckyRoutine != null) StopCoroutine(_luckyRoutine);
            _luckyRoutine = StartCoroutine(LuckyRoutine());
        }

        private IEnumerator LuckyRoutine()
        {
            yield return new WaitForSecondsRealtime(1.1f);
            float elapsed = 0f;
            while (elapsed < 0.35f)
            {
                elapsed += Time.unscaledDeltaTime;
                _luckyCanvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / 0.35f);
                yield return null;
            }
            _luckyRoutine = null;
            if (_orderSatisfiedMarker) ApplyOrderSatisfiedMarker();
            else _luckyMarker.SetActive(false);
            yield break;
        }

        private void HideLucky()
        {
            if (_luckyRoutine != null)
            {
                StopCoroutine(_luckyRoutine);
                _luckyRoutine = null;
            }
            if (_luckyMarker != null) _luckyMarker.SetActive(false);
        }

        private void ValidateReferences()
        {
            if (_background == null) throw new InvalidOperationException("BoardCellView missing Background");
            if (_iconImage == null) throw new InvalidOperationException("BoardCellView missing IconImage");
            if (_emojiLabel == null) throw new InvalidOperationException("BoardCellView missing EmojiLabel");
            if (_nameLabel == null) throw new InvalidOperationException("BoardCellView missing NameLabel");
            if (_luckyMarker == null) throw new InvalidOperationException("BoardCellView missing LuckyMarker");
            if (_luckyCanvasGroup == null) throw new InvalidOperationException("BoardCellView missing LuckyCanvasGroup");
            if (_crownMarker == null) throw new InvalidOperationException("BoardCellView missing CrownMarker");
            EnsureLockOverlays();
        }

        private void EnsureLockOverlays()
        {
            if (_semiLockOverlay == null)
            {
                var overlay = new GameObject("SemiLockOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                overlay.transform.SetParent(transform, false);
                var rect = (RectTransform)overlay.transform;
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                var image = overlay.GetComponent<Image>();
                image.color = new Color(0.12f, 0.08f, 0.04f, 0.62f);
                image.raycastTarget = false;
                _semiLockOverlay = overlay;
            }
            if (_fullLockOverlay == null)
            {
                var overlay = new GameObject("FullLockOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                overlay.transform.SetParent(transform, false);
                var rect = (RectTransform)overlay.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                var image = overlay.GetComponent<Image>();
                image.color = new Color(0.08f, 0.05f, 0.02f, 0.80f);
                image.raycastTarget = false;
                _fullLockOverlay = overlay;
            }
        }
    }
}
