using System;
using UnityEngine;
using UnityEngine.UI;

namespace MergePrototype.Presentation
{
    /// <summary>
    /// GameUi.prefab 的序列化引用容器。它不保存玩法状态，只暴露可由 Presenter 绑定的数据入口。
    /// </summary>
    public sealed class GameUiView : MonoBehaviour
    {
        [SerializeField] private OrderCardView _orderCardPrefab;
        [SerializeField] private BoardCellView _boardCellPrefab;
        [SerializeField] private RectTransform _orderContent;
        [SerializeField] private RectTransform _boardGrid;
        [SerializeField] private Text _moneyText;
        [SerializeField] private Text _lvText;
        [SerializeField] private Text _levelLabel;
        [SerializeField] private Text _levelProgressText;
        [SerializeField] private Text _statusText;
        [SerializeField] private RectTransform _levelFill;
        [SerializeField] private ScrollRect _orderScrollRect;
        [SerializeField] private HorizontalLayoutGroup _orderLayout;
        [SerializeField] private Button _cashOutButton;
        [SerializeField] private Button _encyclopediaButton;
        [SerializeField] private Button _generatorButton;
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _autoCompleteButton;
        [SerializeField] private GameObject _deleteModal;
        [SerializeField] private GameObject _encyclopediaModal;
        [SerializeField] private GameObject _levelUpRewardModal;
        [SerializeField] private GameObject _dragGhost;
        [SerializeField] private Image _dragGhostImage;
        [SerializeField] private Image _dragGhostIcon;
        [SerializeField] private Text _dragGhostLabel;
        [SerializeField] private Image _deletePreviewIcon;
        [SerializeField] private Text _deletePreviewLabel;

        public OrderCardView OrderCardPrefab => _orderCardPrefab;
        public BoardCellView BoardCellPrefab => _boardCellPrefab;
        public RectTransform OrderContent => _orderContent;
        public RectTransform BoardGrid => _boardGrid;
        public Text MoneyText => _moneyText;
        public Text LvText => _lvText;
        public Text LevelLabel => _levelLabel;
        public Text LevelProgressText => _levelProgressText;
        public Text StatusText => _statusText;
        public RectTransform LevelFill => _levelFill;
        public ScrollRect OrderScrollRect => _orderScrollRect;
        public HorizontalLayoutGroup OrderLayout => _orderLayout;
        public Button CashOutButton => _cashOutButton;
        public Button EncyclopediaButton => _encyclopediaButton;
        public Button GeneratorButton => _generatorButton;
        public Button RestartButton => _restartButton;
        public Button AutoCompleteButton
        {
            get
            {
                EnsureAutoCompleteButton();
                return _autoCompleteButton;
            }
        }
        public GameObject DeleteModal => _deleteModal;
        public GameObject EncyclopediaModal => _encyclopediaModal;
        public GameObject LevelUpRewardModal => _levelUpRewardModal;
        public GameObject DragGhost => _dragGhost;
        public Image DragGhostImage => _dragGhostImage;
        public Image DragGhostIcon => _dragGhostIcon;
        public Text DragGhostLabel => _dragGhostLabel;
        public Image DeletePreviewIcon => _deletePreviewIcon;
        public Text DeletePreviewLabel => _deletePreviewLabel;

        public void ValidateReferences()
        {
            if (_orderCardPrefab == null) throw new InvalidOperationException("GameUiView missing OrderCardPrefab");
            if (_boardCellPrefab == null) throw new InvalidOperationException("GameUiView missing BoardCellPrefab");
            if (_orderContent == null) throw new InvalidOperationException("GameUiView missing OrderContent");
            if (_boardGrid == null) throw new InvalidOperationException("GameUiView missing BoardGrid");
            if (_moneyText == null) throw new InvalidOperationException("GameUiView missing MoneyText");
            if (_lvText == null) throw new InvalidOperationException("GameUiView missing LvText");
            if (_levelLabel == null) throw new InvalidOperationException("GameUiView missing LevelLabel");
            if (_levelProgressText == null) throw new InvalidOperationException("GameUiView missing LevelProgressText");
            if (_statusText == null) throw new InvalidOperationException("GameUiView missing StatusText");
            if (_levelFill == null) throw new InvalidOperationException("GameUiView missing LevelFill");
            if (_orderScrollRect == null) throw new InvalidOperationException("GameUiView missing OrderScrollRect");
            if (_orderLayout == null) throw new InvalidOperationException("GameUiView missing OrderLayout");
            if (_cashOutButton == null) throw new InvalidOperationException("GameUiView missing CashOutButton");
            if (_encyclopediaButton == null) throw new InvalidOperationException("GameUiView missing EncyclopediaButton");
            if (_generatorButton == null) throw new InvalidOperationException("GameUiView missing GeneratorButton");
            EnsureRestartButton();
            EnsureAutoCompleteButton();
            if (_deleteModal == null) throw new InvalidOperationException("GameUiView missing DeleteModal");
            if (_encyclopediaModal == null) throw new InvalidOperationException("GameUiView missing EncyclopediaModal");
            if (_levelUpRewardModal == null) throw new InvalidOperationException("GameUiView missing LevelUpRewardModal");
            if (_dragGhost == null) throw new InvalidOperationException("GameUiView missing DragGhost");
            if (_dragGhostImage == null) throw new InvalidOperationException("GameUiView missing DragGhostImage");
            if (_dragGhostIcon == null) throw new InvalidOperationException("GameUiView missing DragGhostIcon");
            if (_dragGhostLabel == null) throw new InvalidOperationException("GameUiView missing DragGhostLabel");
            if (_deletePreviewIcon == null) throw new InvalidOperationException("GameUiView missing DeletePreviewIcon");
            if (_deletePreviewLabel == null) throw new InvalidOperationException("GameUiView missing DeletePreviewLabel");
        }

        private void EnsureRestartButton()
        {
            if (_restartButton != null) return;
            var go = new GameObject("RestartButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(450f, 58f);
            rect.sizeDelta = new Vector2(140f, 124f);
            var image = go.GetComponent<Image>();
            image.color = new Color(0.68f, 0.37f, 0.14f);
            image.raycastTarget = true;
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var label = labelGo.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = "RESET";
            label.fontSize = 24;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            _restartButton = go.GetComponent<Button>();
        }

        private void EnsureAutoCompleteButton()
        {
            if (_autoCompleteButton != null) return;
            var go = new GameObject("AutoCompleteButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(450f, 196f);
            rect.sizeDelta = new Vector2(140f, 124f);
            var image = go.GetComponent<Image>();
            image.color = new Color(0.22f, 0.62f, 0.20f);
            image.raycastTarget = true;
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var label = labelGo.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = "AUTO";
            label.fontSize = 24;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            _autoCompleteButton = go.GetComponent<Button>();
        }
    }
}
