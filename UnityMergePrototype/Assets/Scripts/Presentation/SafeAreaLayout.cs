using UnityEngine;

namespace MergePrototype.Presentation
{
    /// <summary>
    /// Keeps the fixed-width portrait composition inside device safe areas.
    /// Extra height on long screens remains as breathing room between the board
    /// and the bottom action area instead of scaling the gameplay artwork.
    /// </summary>
    public sealed class SafeAreaLayout : MonoBehaviour
    {
        private static readonly string[] TopAnchoredNames =
        {
            "TopStatus", "LevelPanel", "OrdersSection", "BoardFrame"
        };

        private static readonly string[] BottomAnchoredNames =
        {
            "StatusText", "EncyclopediaButton", "GeneratorButton", "RestartButton"
        };

        private RectTransform[] _topAnchored;
        private Vector2[] _topBasePositions;
        private RectTransform[] _bottomAnchored;
        private Vector2[] _bottomBasePositions;
        private RectTransform _bottomBar;
        private Vector2 _bottomBarBasePosition;
        private Vector2 _bottomBarBaseSize;
        private int _lastScreenWidth = -1;
        private int _lastScreenHeight = -1;
        private Rect _lastSafeArea;

        public void Awake()
        {
            CacheLayout();
            ApplyLayout();
        }

        public void OnEnable()
        {
            if (_topAnchored == null) CacheLayout();
            ApplyLayout();
        }

        public void Update()
        {
            if (_lastScreenWidth == Screen.width
                && _lastScreenHeight == Screen.height
                && _lastSafeArea == Screen.safeArea)
                return;

            ApplyLayout();
        }

        public static SafeAreaInsets CalculateInsets(Rect safeArea,
            int screenWidth, int screenHeight, float canvasScale)
        {
            float scale = Mathf.Max(0.0001f, canvasScale);
            return new SafeAreaInsets(
                Mathf.Max(0f, safeArea.yMin) / scale,
                Mathf.Max(0f, screenHeight - safeArea.yMax) / scale,
                Mathf.Max(0f, safeArea.xMin) / scale,
                Mathf.Max(0f, screenWidth - safeArea.xMax) / scale);
        }

        private void CacheLayout()
        {
            _topAnchored = FindRects(TopAnchoredNames);
            _topBasePositions = CapturePositions(_topAnchored);
            _bottomAnchored = FindRects(BottomAnchoredNames);
            _bottomBasePositions = CapturePositions(_bottomAnchored);

            var bottom = transform.Find("BottomBar") as RectTransform;
            _bottomBar = bottom;
            if (_bottomBar != null)
            {
                _bottomBarBasePosition = _bottomBar.anchoredPosition;
                _bottomBarBaseSize = _bottomBar.sizeDelta;
            }
        }

        private void ApplyLayout()
        {
            if (_topAnchored == null) CacheLayout();

            var canvas = GetComponent<Canvas>();
            float canvasScale = canvas == null ? 1f : canvas.scaleFactor;
            SafeAreaInsets insets = CalculateInsets(
                Screen.safeArea, Screen.width, Screen.height, canvasScale);

            for (int i = 0; i < _topAnchored.Length; i++)
            {
                if (_topAnchored[i] == null) continue;
                _topAnchored[i].anchoredPosition = _topBasePositions[i]
                    + Vector2.down * insets.Top;
            }

            for (int i = 0; i < _bottomAnchored.Length; i++)
            {
                if (_bottomAnchored[i] == null) continue;
                _bottomAnchored[i].anchoredPosition = _bottomBasePositions[i]
                    + Vector2.up * insets.Bottom;
            }

            if (_bottomBar != null)
            {
                _bottomBar.anchoredPosition = _bottomBarBasePosition
                    + Vector2.up * insets.Bottom;
                _bottomBar.sizeDelta = new Vector2(
                    _bottomBarBaseSize.x,
                    _bottomBarBaseSize.y + insets.Bottom);
            }

            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            _lastSafeArea = Screen.safeArea;
        }

        private RectTransform[] FindRects(string[] names)
        {
            var result = new RectTransform[names.Length];
            for (int i = 0; i < names.Length; i++)
                result[i] = transform.Find(names[i]) as RectTransform;
            return result;
        }

        private static Vector2[] CapturePositions(RectTransform[] rects)
        {
            var positions = new Vector2[rects.Length];
            for (int i = 0; i < rects.Length; i++)
                positions[i] = rects[i] == null ? Vector2.zero : rects[i].anchoredPosition;
            return positions;
        }
    }

    public struct SafeAreaInsets
    {
        public SafeAreaInsets(float bottom, float top, float left, float right)
        {
            Bottom = bottom;
            Top = top;
            Left = left;
            Right = right;
        }

        public float Bottom { get; }
        public float Top { get; }
        public float Left { get; }
        public float Right { get; }
    }
}
