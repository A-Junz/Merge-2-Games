using System.Collections.Generic;
using System.IO;
using System.Linq;
using MergePrototype.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MergePrototype.EditorTools
{
    /// <summary>
    /// 生成可由设计人员直接编辑的主界面、订单卡和棋盘格 Prefab。
    /// 运行时只实例化这些资产，不调用本类。
    /// </summary>
    public static class UiPrefabBuilder
    {
        private const string PrefabFolder = "Assets/Resources/UI/Prefabs";
        private const string UiFolder = "Assets/Resources/UI";
        private const string LevelUpPopupUiFolder = "Assets/Resources/UI/LevelUpPopup";
        private const string EncyclopediaUiFolder = "Assets/Resources/UI/EncyclopediaGenerated";

        private static readonly Color CreamBg = new Color(0.98f, 0.84f, 0.66f);
        private static readonly Color KitchenWall = new Color(0.98f, 0.78f, 0.47f);
        private static readonly Color KitchenWood = new Color(0.68f, 0.37f, 0.14f);
        private static readonly Color WoodBrown = new Color(0.52f, 0.29f, 0.12f);
        private static readonly Color CardWhite = new Color(0.995f, 0.985f, 0.955f);
        private static readonly Color DarkBrown = new Color(0.32f, 0.22f, 0.13f);
        private static readonly Color BlueBtn = new Color(0.09f, 0.53f, 0.90f);
        private static readonly Color OrangeBtn = new Color(0.98f, 0.52f, 0.06f);
        private static readonly Color GreenOk = new Color(0.18f, 0.76f, 0.23f);
        private static readonly Color ClaimableRewardBg = new Color(0.78f, 0.95f, 0.72f);

        [MenuItem("Merge Prototype/Build UI Prefabs")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(PrefabFolder);
            AssetDatabase.Refresh();

            string cellPath = $"{PrefabFolder}/BoardCell.prefab";
            string orderPath = $"{PrefabFolder}/OrderCard.prefab";
            string rootPath = $"{PrefabFolder}/GameUi.prefab";

            var cellSource = BuildBoardCell();
            var cellPrefab = SavePrefab(cellSource, cellPath);
            Object.DestroyImmediate(cellSource);

            var orderSource = BuildOrderCard();
            var orderPrefab = SavePrefab(orderSource, orderPath);
            Object.DestroyImmediate(orderSource);
            var root = BuildGameUi(cellPrefab, orderPrefab);
            SavePrefab(root, rootPath);

            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var loadedRoot = AssetDatabase.LoadAssetAtPath<GameUiView>(rootPath);
            loadedRoot.ValidateReferences();
            Debug.Log($"[UiPrefabBuilder] Built {rootPath}, {orderPath}, {cellPath}");
        }

        private static GameObject BuildBoardCell()
        {
            var root = CreateRect("BoardCell", null);
            var view = root.AddComponent<BoardCellView>();
            var background = AddImage("Background", root.transform,
                LoadSprite("ui_board_cell_empty"), Color.white, Image.Type.Sliced);
            background.raycastTarget = true;
            Stretch(background.rectTransform);

            var icon = AddImage("IconImage", root.transform, null, Color.white, Image.Type.Simple);
            Anchor(icon.rectTransform, 0.08f, 0.05f, 0.92f, 0.98f);
            icon.preserveAspect = true;

            var emoji = AddText("EmojiLabel", root.transform, string.Empty, 48, Color.white, TextAnchor.MiddleCenter);
            Anchor(emoji.rectTransform, 0f, 0.30f, 1f, 1f);
            emoji.fontStyle = FontStyle.Bold;

            var name = AddText("NameLabel", root.transform, string.Empty, 20, Color.white, TextAnchor.MiddleCenter);
            Anchor(name.rectTransform, 0f, 0f, 1f, 0.32f);
            name.fontStyle = FontStyle.Bold;
            name.gameObject.SetActive(false);

            var semiLock = AddImage("SemiLockOverlay", root.transform, null,
                new Color(0.12f, 0.08f, 0.04f, 0.62f), Image.Type.Simple);
            Anchor(semiLock.rectTransform, 0.50f, 0f, 1f, 1f);
            semiLock.raycastTarget = false;

            var fullLock = AddImage("FullLockOverlay", root.transform, null,
                new Color(0.08f, 0.05f, 0.02f, 0.80f), Image.Type.Simple);
            Stretch(fullLock.rectTransform);
            fullLock.raycastTarget = false;

            var lucky = AddImage("LuckyMarker", root.transform, null,
                new Color(0.30f, 0.72f, 0.32f, 0.96f), Image.Type.Sliced);
            Anchor(lucky.rectTransform, 0.68f, 0.02f, 0.98f, 0.34f);
            var luckyCanvas = lucky.gameObject.AddComponent<CanvasGroup>();
            var luckyLabel = AddText("Label", lucky.transform, "✓", 26,
                Color.white, TextAnchor.MiddleCenter);
            Stretch(luckyLabel.rectTransform);
            lucky.gameObject.SetActive(false);

            var crown = AddText("CrownMarker", root.transform, "♛", 34,
                new Color(1f, 0.78f, 0.08f, 1f), TextAnchor.MiddleCenter);
            Anchor(crown.rectTransform, 0.02f, 0.02f, 0.32f, 0.36f);
            crown.fontStyle = FontStyle.Bold;
            crown.raycastTarget = false;
            var crownOutline = crown.gameObject.AddComponent<Outline>();
            crownOutline.effectColor = new Color(0.38f, 0.16f, 0.02f, 0.95f);
            crownOutline.effectDistance = new Vector2(2f, -2f);
            crown.gameObject.SetActive(false);

            SetObject(view, "_background", background);
            SetObject(view, "_iconImage", icon);
            SetObject(view, "_emojiLabel", emoji);
            SetObject(view, "_nameLabel", name);
            SetObject(view, "_luckyMarker", lucky.gameObject);
            SetObject(view, "_luckyCanvasGroup", luckyCanvas);
            SetObject(view, "_crownMarker", crown.gameObject);
            SetObject(view, "_emptyCellSprite", LoadSprite("ui_board_cell_empty"));
            SetObject(view, "_semiLockOverlay", semiLock.gameObject);
            SetObject(view, "_fullLockOverlay", fullLock.gameObject);
            semiLock.gameObject.SetActive(false);
            fullLock.gameObject.SetActive(false);
            return root;
        }

        private static GameObject BuildOrderCard()
        {
            var root = CreateRect("OrderCard", null);
            var rootRect = root.transform as RectTransform;
            rootRect.sizeDelta = new Vector2(UiLayoutSpec.OrderCardWidth, UiLayoutSpec.OrderCardHeight);
            var rootLayout = root.AddComponent<LayoutElement>();
            rootLayout.minWidth = UiLayoutSpec.OrderCardWidth;
            rootLayout.preferredWidth = UiLayoutSpec.OrderCardWidth;
            rootLayout.flexibleWidth = 0f;
            rootLayout.minHeight = UiLayoutSpec.OrderCardHeight;
            rootLayout.preferredHeight = UiLayoutSpec.OrderCardHeight;
            rootLayout.flexibleHeight = 0f;
            var view = root.AddComponent<OrderCardView>();
            var background = AddImage("Background", root.transform,
                LoadSprite("ui_order_card_frame"), CardWhite, Image.Type.Sliced);
            Stretch(background.rectTransform);

            var hero = AddImage("HeroPanel", root.transform, null,
                new Color(1f, 0.93f, 0.75f, 0.42f), Image.Type.Sliced);
            Anchor(hero.rectTransform, 0.035f, 0.38f, 0.965f, 0.95f);

            var avatar = AddImage("AvatarImage", root.transform, null,
                Color.white, Image.Type.Simple);
            Anchor(avatar.rectTransform, 0.035f, 0.39f, 0.50f, 0.95f);
            avatar.preserveAspect = true;
            var avatarText = AddText("AvatarText", avatar.transform, "C", 48, Color.white, TextAnchor.MiddleCenter);
            Stretch(avatarText.rectTransform);
            avatarText.fontStyle = FontStyle.Bold;
            avatarText.gameObject.SetActive(false);

            var itemList = CreateRect("ItemList", root.transform);
            Anchor(itemList.transform as RectTransform, 0.49f, 0.43f, 0.96f, 0.92f);
            var listLayout = itemList.gameObject.AddComponent<VerticalLayoutGroup>();
            listLayout.padding = new RectOffset(0, 2, 0, 0);
            listLayout.spacing = 2f;
            listLayout.childAlignment = TextAnchor.MiddleCenter;
            listLayout.childControlWidth = true;
            listLayout.childControlHeight = true;
            listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = true;

            var rows = new List<GameObject>();
            var icons = new List<Image>();
            var checks = new List<GameObject>();
            for (int i = 0; i < 3; i++)
            {
                var row = AddImage($"ItemRow_{i}", itemList.transform, null,
                    new Color(1f, 1f, 1f, 0.08f), Image.Type.Sliced);
                var rowLayout = row.gameObject.AddComponent<LayoutElement>();
                rowLayout.minHeight = 50f;
                rowLayout.preferredHeight = 58f;
                rowLayout.flexibleHeight = 1f;
                var icon = AddImage("Icon", row.transform, null, Color.white, Image.Type.Simple);
                Anchor(icon.rectTransform, 0.04f, 0.02f, 0.94f, 0.98f);
                icon.preserveAspect = true;
                var check = AddImage("Checkmark", row.transform, null,
                    new Color(0.30f, 0.72f, 0.32f), Image.Type.Sliced);
                Anchor(check.rectTransform, 0.67f, 0.04f, 1.02f, 0.38f);
                var checkText = AddText("Label", check.transform, "✓", 24, Color.white, TextAnchor.MiddleCenter);
                Stretch(checkText.rectTransform);
                rows.Add(row.gameObject);
                icons.Add(icon);
                checks.Add(check.gameObject);
            }

            var progress = AddText("Progress", root.transform, string.Empty, 24,
                new Color(0.45f, 0.35f, 0.25f), TextAnchor.MiddleCenter);
            AnchorBottomStretch(progress.rectTransform, 8f, 14f, 42f);
            progress.gameObject.SetActive(false);
            var rewardBar = AddImage("RewardBar", root.transform, null,
                new Color(1f, 0.96f, 0.83f, 1f), Image.Type.Sliced);
            AnchorBottomStretch(rewardBar.rectTransform, 7f, 14f, 44f);
            var rewardXpIcon = CreateRect("RewardXpIcon", rewardBar.transform);
            Anchor(rewardXpIcon.transform as RectTransform, 0.02f, 0.08f, 0.16f, 0.92f);
            rewardXpIcon.AddComponent<RectMask2D>();
            var rewardXpSprite = AddImage("Icon", rewardXpIcon.transform,
                LoadSprite("ui_reward_smile_chip"), Color.white, Image.Type.Simple);
            rewardXpSprite.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            rewardXpSprite.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            rewardXpSprite.rectTransform.pivot = new Vector2(0f, 0.5f);
            rewardXpSprite.rectTransform.sizeDelta = new Vector2(72f, 42f);
            rewardXpSprite.rectTransform.anchoredPosition = Vector2.zero;
            rewardXpSprite.preserveAspect = true;

            var rewardCoinIcon = CreateRect("RewardCoinIcon", rewardBar.transform);
            Anchor(rewardCoinIcon.transform as RectTransform, 0.55f, 0.08f, 0.70f, 0.92f);
            rewardCoinIcon.AddComponent<RectMask2D>();
            var rewardCoinSprite = AddImage("Icon", rewardCoinIcon.transform,
                LoadSprite("ui_reward_cash_chip"), Color.white, Image.Type.Simple);
            rewardCoinSprite.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            rewardCoinSprite.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            rewardCoinSprite.rectTransform.pivot = new Vector2(0f, 0.5f);
            rewardCoinSprite.rectTransform.sizeDelta = new Vector2(88f, 42f);
            rewardCoinSprite.rectTransform.anchoredPosition = Vector2.zero;
            rewardCoinSprite.preserveAspect = true;
            var reward = AddText("Reward", rewardBar.transform, string.Empty, 22,
                DarkBrown, TextAnchor.MiddleCenter);
            Anchor(reward.rectTransform, 0.14f, 0f, 0.98f, 1f);
            reward.gameObject.SetActive(false);
            var rewardXpText = AddText("RewardXpText", rewardBar.transform, "+1", 22,
                DarkBrown, TextAnchor.MiddleCenter);
            Anchor(rewardXpText.rectTransform, 0.15f, 0f, 0.50f, 1f);
            var rewardCoinText = AddText("RewardCoinText", rewardBar.transform, "+32", 22,
                DarkBrown, TextAnchor.MiddleCenter);
            Anchor(rewardCoinText.rectTransform, 0.68f, 0f, 0.98f, 1f);

            var submit = AddButton("SubmitButton", root.transform, "SUBMIT", 28,
                new Color(1f, 0.67f, 0.14f));
            AnchorBottomStretch(submit.transform as RectTransform, 54f, 14f, 40f);
            var submitImage = submit.GetComponent<Image>();
            var submitLabel = submit.transform.Find("Label").GetComponent<Text>();
            submitImage.color = Color.clear;
            submitLabel.gameObject.SetActive(false);

            var rewarded = AddButton("RewardedVideoButton", root.transform,
                "▶  CLAIM ALL", 20, new Color(0.24f, 0.82f, 0.08f));
            AnchorBottomStretch(rewarded.transform as RectTransform, 98f, 14f, 40f);
            rewarded.gameObject.SetActive(false);

            var complete = AddImage("CompleteBanner", root.transform,
                LoadSprite("ui_order_complete_badge"), new Color(0.30f, 0.72f, 0.32f), Image.Type.Sliced);
            AnchorBottomStretch(complete.rectTransform, 54f, 14f, 40f);
            complete.gameObject.SetActive(false);

            SetObject(view, "_background", background);
            SetObject(view, "_avatarImage", avatar);
            SetObject(view, "_avatarText", avatarText);
            SetObjectList(view, "_itemRows", rows);
            SetObjectList(view, "_itemIcons", icons);
            SetObjectList(view, "_itemCheckmarks", checks);
            SetObject(view, "_progressText", progress);
            SetObject(view, "_rewardText", reward);
            SetObject(view, "_rewardXpText", rewardXpText);
            SetObject(view, "_rewardCoinText", rewardCoinText);
            SetObject(view, "_submitButton", submit);
            SetObject(view, "_rewardedVideoButton", rewarded);
            SetObject(view, "_submitImage", submitImage);
            SetObject(view, "_submitLabel", submitLabel);
            SetObject(view, "_completeBanner", complete.gameObject);
            return root;
        }

        private static GameObject BuildGameUi(GameObject cellPrefab, GameObject orderPrefab)
        {
            var root = CreateRect("GameUi", null);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = UiLayoutSpec.CanvasMatchWidthOrHeight;
            root.AddComponent<GraphicRaycaster>();
            root.AddComponent<SafeAreaLayout>();
            var view = root.AddComponent<GameUiView>();

            var background = AddImage("Background", root.transform, null,
                new Color(CreamBg.r, CreamBg.g, CreamBg.b, 0f), Image.Type.Simple);
            Stretch(background.rectTransform);

            var wall = AddImage("KitchenBackdrop", root.transform, null, KitchenWall, Image.Type.Simple);
            AnchorTop(wall.rectTransform, 0f, 0f, 1f, 560f);
            var wood = AddImage("KitchenCounter", root.transform, null, KitchenWood, Image.Type.Simple);
            AnchorTop(wood.rectTransform, 0f, -560f, 1f, 90f);
            var backdrop = AddImage("BackdropArtwork", root.transform,
                LoadSprite("ui_gameplay_backdrop"), Color.white, Image.Type.Simple);
            Stretch(backdrop.rectTransform);
            backdrop.preserveAspect = false;
            backdrop.raycastTarget = false;
            wall.gameObject.SetActive(false);
            wood.gameObject.SetActive(false);

            var topStatus = CreateRect("TopStatus", root.transform);
            AnchorTop(topStatus.transform as RectTransform, 0f, -24f, 980f, 108f);
            var money = AddImage("MoneyPill", topStatus.transform, null, CardWhite, Image.Type.Sliced);
            Anchor(money.rectTransform, 0.01f, 0.10f, 0.34f, 0.90f);
            var moneyIcon = CreateRect("MoneyIcon", money.transform);
            Anchor(moneyIcon.transform as RectTransform, 0.04f, 0.14f, 0.28f, 0.86f);
            var moneyIconImage = AddImage("Icon", moneyIcon.transform,
                LoadSprite("ui_reward_cash_chip"), Color.white, Image.Type.Filled);
            Stretch(moneyIconImage.rectTransform);
            moneyIconImage.preserveAspect = true;
            moneyIconImage.fillMethod = Image.FillMethod.Horizontal;
            moneyIconImage.fillOrigin = 0;
            moneyIconImage.fillAmount = 0.52f;
            var moneyText = AddText("MoneyText", money.transform, "0", 40, DarkBrown, TextAnchor.MiddleCenter);
            Anchor(moneyText.rectTransform, 0.28f, 0f, 0.98f, 1f);
            var cash = AddButton("CashOutButton", topStatus.transform, "CASH OUT", 34, BlueBtn);
            Anchor(cash.transform as RectTransform, 0.36f, 0.08f, 0.69f, 0.92f);
            var lv = AddImage("LevelCapsule", topStatus.transform, null, CardWhite, Image.Type.Sliced);
            Anchor(lv.rectTransform, 0.70f, 0.10f, 0.99f, 0.90f);
            var lvText = AddText("LvText", lv.transform, string.Empty, 32, DarkBrown, TextAnchor.MiddleCenter);
            Stretch(lvText.rectTransform);

            var level = AddImage("LevelPanel", root.transform, LoadSprite("ui_level_panel_base"), CardWhite, Image.Type.Sliced);
            AnchorTop(level.rectTransform, 0.5f, -132f, 780f, 182f);
            var levelLabel = AddText("LevelLabel", level.transform, "LEVEL", 40, DarkBrown, TextAnchor.MiddleCenter);
            Anchor(levelLabel.rectTransform, 0.28f, 0.72f, 0.72f, 0.98f);
            levelLabel.fontStyle = FontStyle.Bold;
            var smile = AddImage("SmileIcon", level.transform, LoadSprite("ui_reward_smile_chip"),
                Color.white, Image.Type.Filled);
            Anchor(smile.rectTransform, 0.055f, 0.20f, 0.18f, 0.75f);
            smile.preserveAspect = true;
            smile.fillMethod = Image.FillMethod.Horizontal;
            smile.fillOrigin = 0;
            smile.fillAmount = 0.58f;
            var smileText = AddText("Face", smile.transform, "☺", 56, DarkBrown, TextAnchor.MiddleCenter);
            Stretch(smileText.rectTransform);
            smileText.gameObject.SetActive(false);
            var fill = AddImage("LevelFill", level.transform, null, GreenOk, Image.Type.Sliced);
            Anchor(fill.rectTransform, 0.16f, 0.31f, 0.161f, 0.59f);
            var progress = AddText("LevelProgressText", level.transform, "0/7", 32, DarkBrown, TextAnchor.MiddleCenter);
            Anchor(progress.rectTransform, 0.16f, 0.30f, 0.87f, 0.60f);
            var orders = CreateRect("OrdersSection", root.transform);
            AnchorTop(orders.transform as RectTransform, 0f, -330f, 1080f, 370f);
            var header = AddImage("OrdersHeader", orders.transform, LoadSprite("ui_orders_header"), WoodBrown, Image.Type.Sliced);
            AnchorTop(header.rectTransform, 0f, 0f, 1080f, 64f);
            var ordersPanelArtwork = AddImage("OrdersPanelArtwork", orders.transform,
                LoadSprite("ui_orders_panel_base"), Color.white, Image.Type.Simple);
            Anchor(ordersPanelArtwork.rectTransform, 0.01f, 0.05f, 0.99f, 0.82f);
            ordersPanelArtwork.raycastTarget = false;
            var viewport = AddImage("OrderViewport", orders.transform, null, new Color(1f, 1f, 1f, 0f), Image.Type.Simple);
            // The viewport spans the whole order section so transparent margins also receive swipes.
            Anchor(viewport.rectTransform, 0f, 0f, 1f, 0.86f);
            viewport.raycastTarget = true;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = false;
            scroll.viewport = viewport.rectTransform;
            var content = CreateRect("OrderContent", viewport.transform);
            var contentRt = content.transform as RectTransform;
            contentRt.anchorMin = new Vector2(0f, 0f);
            contentRt.anchorMax = new Vector2(0f, 1f);
            contentRt.pivot = new Vector2(0f, 0.5f);
            contentRt.anchoredPosition = new Vector2(
                (UiLayoutSpec.ReferenceWidth - UiLayoutSpec.OrderViewportWidth) * 0.5f, 0f);
            contentRt.sizeDelta = new Vector2(UiLayoutSpec.OrderViewportWidth, 0f);
            var orderLayout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            orderLayout.spacing = 16f;
            orderLayout.childAlignment = TextAnchor.MiddleCenter;
            orderLayout.childForceExpandWidth = false;
            orderLayout.childForceExpandHeight = false;
            orderLayout.childControlWidth = true;
            orderLayout.childControlHeight = true;
            scroll.content = content.transform as RectTransform;

            var board = AddImage("BoardFrame", root.transform, LoadSprite("ui_board_frame_empty"), WoodBrown, Image.Type.Sliced);
            AnchorTop(board.rectTransform, 0.5f, -690f, 1040f, 1000f);
            var boardPanel = AddImage("BoardPanel", board.transform, null, new Color(1f, 1f, 1f, 0f), Image.Type.Simple);
            Stretch(boardPanel.rectTransform);
            boardPanel.rectTransform.offsetMin = new Vector2(10f, 10f);
            boardPanel.rectTransform.offsetMax = new Vector2(-10f, -10f);
            var grid = CreateRect("BoardGrid", boardPanel.transform);
            Stretch(grid.transform as RectTransform);
            var gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 7;
            gridLayout.cellSize = new Vector2(140f, 102f);
            gridLayout.spacing = new Vector2(5f, 5f);
            gridLayout.padding = new RectOffset(4, 4, 4, 4);

            var status = AddText("StatusText", root.transform, string.Empty, 28, DarkBrown, TextAnchor.MiddleCenter);
            AnchorBottomFixed(status.rectTransform, 206f, 0f, 1080f, 44f);
            var bottom = AddImage("BottomBar", root.transform, null, new Color(0.96f, 0.80f, 0.78f), Image.Type.Simple);
            AnchorBottomFull(bottom.rectTransform, 260f);
            var collection = AddButton("EncyclopediaButton", root.transform, string.Empty, 24, WoodBrown);
            AnchorBottomFixed(collection.transform as RectTransform, 66f, -500f, 132f, 132f);
            collection.GetComponent<Image>().sprite = LoadSprite("ui_collection_closed_book");
            collection.GetComponent<Image>().type = Image.Type.Simple;
            collection.transform.Find("Label").gameObject.SetActive(false);
            var generator = AddButton("GeneratorButton", root.transform, string.Empty, 44, OrangeBtn);
            AnchorBottomFixed(generator.transform as RectTransform, 58f, 0f, 720f, 184f);
            generator.GetComponent<Image>().sprite = LoadSprite("ui_serve_items_button");
            generator.GetComponent<Image>().type = Image.Type.Simple;
            generator.transform.Find("Label").gameObject.SetActive(false);
            var restart = AddButton("RestartButton", root.transform, "RESET", 24, new Color(0.68f, 0.37f, 0.14f));
            AnchorBottomFixed(restart.transform as RectTransform, 58f, 450f, 140f, 124f);
            var autoComplete = AddButton("AutoCompleteButton", root.transform, "AUTO", 24,
                new Color(0.22f, 0.62f, 0.20f));
            AnchorBottomFixed(autoComplete.transform as RectTransform, 196f, 450f, 140f, 124f);
            // Kept as a hidden compatibility node for existing prefab/UI
            // checks and old scene references. The live reset control is the
            // dedicated RestartButton above.
            var legacyRestart = AddButton("LegacyRestartButton", root.transform, string.Empty, 24,
                new Color(0.68f, 0.37f, 0.14f));
            legacyRestart.gameObject.SetActive(false);

            var dragGhost = AddImage("DragGhost", root.transform, null, CardWhite, Image.Type.Simple);
            dragGhost.rectTransform.sizeDelta = new Vector2(150f, 110f);
            var dragIcon = AddImage("Icon", dragGhost.transform, null, Color.white, Image.Type.Simple);
            Anchor(dragIcon.rectTransform, 0.08f, 0.08f, 0.92f, 0.92f);
            var dragLabel = AddText("Label", dragGhost.transform, string.Empty, 64, Color.white, TextAnchor.MiddleCenter);
            Stretch(dragLabel.rectTransform);
            dragGhost.gameObject.SetActive(false);

            var deleteModal = BuildModal(root.transform, "DeleteModal", "DELETE THIS ITEM?");
            var encyclopediaModal = BuildModal(root.transform, "EncyclopediaModal", "FOOD COLLECTION");
            var levelUpRewardModal = BuildLevelUpRewardModal(root.transform);
            deleteModal.SetActive(false);
            encyclopediaModal.SetActive(false);
            levelUpRewardModal.SetActive(false);

            SetObject(view, "_orderCardPrefab", orderPrefab.GetComponent<OrderCardView>());
            SetObject(view, "_boardCellPrefab", cellPrefab.GetComponent<BoardCellView>());
            SetObject(view, "_orderContent", content.transform as RectTransform);
            SetObject(view, "_boardGrid", grid.transform as RectTransform);
            SetObject(view, "_orderScrollRect", scroll);
            SetObject(view, "_orderLayout", orderLayout);
            SetObject(view, "_moneyText", moneyText);
            SetObject(view, "_lvText", lvText);
            SetObject(view, "_levelLabel", levelLabel);
            SetObject(view, "_levelProgressText", progress);
            SetObject(view, "_statusText", status);
            SetObject(view, "_levelFill", fill.rectTransform);
            SetObject(view, "_cashOutButton", cash);
            SetObject(view, "_encyclopediaButton", collection);
            SetObject(view, "_generatorButton", generator);
            SetObject(view, "_restartButton", restart);
            SetObject(view, "_autoCompleteButton", autoComplete);
            SetObject(view, "_deleteModal", deleteModal);
            SetObject(view, "_encyclopediaModal", encyclopediaModal);
            SetObject(view, "_levelUpRewardModal", levelUpRewardModal);
            SetObject(view, "_dragGhost", dragGhost.gameObject);
            SetObject(view, "_dragGhostImage", dragGhost);
            SetObject(view, "_dragGhostIcon", dragIcon);
            SetObject(view, "_dragGhostLabel", dragLabel);
            SetObject(view, "_deletePreviewIcon", deleteModal.transform.Find("Card/PreviewIcon").GetComponent<Image>());
            SetObject(view, "_deletePreviewLabel", deleteModal.transform.Find("Card/ItemLabel").GetComponent<Text>());
            return root;
        }

        private static GameObject BuildLevelUpRewardModal(Transform parent)
        {
            var overlay = AddImage("LevelUpRewardModal", parent, null,
                new Color(0.08f, 0.05f, 0.02f, 0.78f), Image.Type.Simple);
            Stretch(overlay.rectTransform);
            overlay.raycastTarget = true;

            var card = AddImage("Card", overlay.transform,
                LoadLevelUpPopupSprite("popup_panel"), Color.white, Image.Type.Sliced);
            Anchor(card.rectTransform, 0.09f, 0.19f, 0.91f, 0.81f);
            card.raycastTarget = true;

            var ribbon = AddImage("TitleRibbon", card.transform,
                LoadLevelUpPopupSprite("title_ribbon"), Color.white, Image.Type.Simple);
            Anchor(ribbon.rectTransform, 0.06f, 0.76f, 0.94f, 0.94f);
            ribbon.preserveAspect = true;

            var hat = AddImage("ChefHat", card.transform,
                LoadLevelUpPopupSprite("chef_hat"), Color.white, Image.Type.Simple);
            Anchor(hat.rectTransform, 0.37f, 0.79f, 0.63f, 0.99f);
            hat.preserveAspect = true;

            var title = AddText("Title", card.transform, "LEVEL UP REWARD", 42,
                DarkBrown, TextAnchor.MiddleCenter);
            Anchor(title.rectTransform, 0.08f, 0.76f, 0.92f, 0.90f);
            title.fontStyle = FontStyle.Bold;

            var rewardArea = AddImage("RewardArea", card.transform,
                LoadLevelUpPopupSprite("reward_area"), Color.white, Image.Type.Sliced);
            Anchor(rewardArea.rectTransform, 0.10f, 0.41f, 0.90f, 0.70f);

            var cash = AddImage("CashIcon", rewardArea.transform,
                LoadLevelUpPopupSprite("cash_icon"), Color.white, Image.Type.Simple);
            Anchor(cash.rectTransform, 0.12f, 0.13f, 0.39f, 0.90f);
            cash.preserveAspect = true;

            var amount = AddText("RewardAmount", rewardArea.transform, "+0", 66,
                new Color(0.10f, 0.48f, 0.16f), TextAnchor.MiddleLeft);
            Anchor(amount.rectTransform, 0.44f, 0.12f, 0.94f, 0.90f);
            amount.fontStyle = FontStyle.Bold;

            var claimAll = AddButton("ClaimAllButton", card.transform, string.Empty, 38,
                Color.white);
            claimAll.GetComponent<Image>().sprite = LoadLevelUpPopupSprite("claim_all_bg");
            claimAll.GetComponent<Image>().type = Image.Type.Sliced;
            claimAll.GetComponent<Image>().color = Color.white;
            Anchor(claimAll.transform as RectTransform, 0.10f, 0.20f, 0.90f, 0.35f);
            var videoIcon = AddImage("VideoIcon", claimAll.transform,
                LoadLevelUpPopupSprite("video_icon"), Color.white, Image.Type.Simple);
            Anchor(videoIcon.rectTransform, 0.09f, 0.18f, 0.25f, 0.82f);
            videoIcon.preserveAspect = true;
            var claimAllLabel = claimAll.transform.Find("Label").GetComponent<Text>();
            claimAllLabel.text = "Claim All";
            Anchor(claimAllLabel.rectTransform, 0.22f, 0f, 0.95f, 1f);
            claimAllLabel.fontSize = 38;

            var onlyClaim = AddButton("OnlyClaimButton", card.transform, string.Empty, 30,
                Color.white);
            onlyClaim.GetComponent<Image>().sprite = LoadLevelUpPopupSprite("only_claim_bg");
            onlyClaim.GetComponent<Image>().type = Image.Type.Sliced;
            onlyClaim.GetComponent<Image>().color = Color.white;
            Anchor(onlyClaim.transform as RectTransform, 0.10f, 0.06f, 0.90f, 0.17f);
            var skipIcon = AddImage("SkipIcon", onlyClaim.transform,
                LoadLevelUpPopupSprite("only_claim_icon"), Color.white, Image.Type.Simple);
            Anchor(skipIcon.rectTransform, 0.06f, 0.16f, 0.18f, 0.84f);
            skipIcon.preserveAspect = true;
            var onlyClaimLabel = onlyClaim.transform.Find("Label").GetComponent<Text>();
            onlyClaimLabel.text = "Only claim +0";
            Anchor(onlyClaimLabel.rectTransform, 0.16f, 0f, 0.96f, 1f);
            onlyClaimLabel.fontSize = 30;
            onlyClaimLabel.color = DarkBrown;
            return overlay.gameObject;
        }

        private static GameObject BuildModal(Transform parent, string name, string title)
        {
            var overlay = AddImage(name, parent, null, new Color(0.08f, 0.05f, 0.02f, 0.72f), Image.Type.Simple);
            Stretch(overlay.rectTransform);
            var card = AddImage("Card", overlay.transform, null, CardWhite, Image.Type.Sliced);
            bool encyclopedia = name == "EncyclopediaModal";
            if (encyclopedia)
            {
                card.sprite = LoadEncyclopediaSprite("encyclopedia_modal_body");
                card.color = Color.white;
            }
            Anchor(card.rectTransform,
                encyclopedia ? 0.05f : 0.08f,
                encyclopedia ? 0.05f : 0.20f,
                encyclopedia ? 0.95f : 0.92f,
                encyclopedia ? 0.95f : 0.80f);
            var label = AddText("Title", card.transform,
                encyclopedia ? "KITCHEN COLLECTION" : title, 42, DarkBrown, TextAnchor.MiddleCenter);
            Anchor(label.rectTransform, 0.05f, 0.86f, 0.95f, 0.98f);
            label.fontStyle = FontStyle.Bold;
            if (encyclopedia) label.gameObject.SetActive(false);
            if (name == "DeleteModal")
            {
                var preview = AddImage("PreviewIcon", card.transform, null, Color.white, Image.Type.Simple);
                Anchor(preview.rectTransform, 0.28f, 0.38f, 0.48f, 0.68f);
                var itemLabel = AddText("ItemLabel", card.transform, string.Empty, 28, DarkBrown, TextAnchor.MiddleLeft);
                Anchor(itemLabel.rectTransform, 0.50f, 0.42f, 0.94f, 0.60f);
                var confirm = AddButton("ConfirmDelete", card.transform, "DELETE", 28, new Color(0.86f, 0.22f, 0.12f));
                AnchorBottomFixed(confirm.transform as RectTransform, 42f, -150f, 220f, 64f);
                var cancel = AddButton("CancelDelete", card.transform, "CANCEL", 28, new Color(0.60f, 0.50f, 0.40f));
                AnchorBottomFixed(cancel.transform as RectTransform, 42f, 150f, 220f, 64f);
            }
            else
            {
                BuildEncyclopediaContents(card.transform);
            }
            return overlay.gameObject;
        }

        private static void BuildEncyclopediaContents(Transform card)
        {
            var header = AddImage("Header", card,
                LoadEncyclopediaSprite("encyclopedia_header_wood_green"), Color.white, Image.Type.Simple);
            Anchor(header.rectTransform, 0.03f, 0.88f, 0.97f, 0.99f);

            var book = AddImage("Book", header.transform,
                LoadEncyclopediaSprite("encyclopedia_closed_recipe_book"),
                Color.white, Image.Type.Simple);
            Anchor(book.rectTransform, 0.01f, 0.04f, 0.13f, 0.96f);
            book.preserveAspect = true;

            var title = AddText("Title", header.transform, "KITCHEN COLLECTION", 42,
                DarkBrown, TextAnchor.MiddleCenter);
            Anchor(title.rectTransform, 0.13f, 0f, 0.87f, 1f);
            title.fontStyle = FontStyle.Bold;

            var close = AddButton("Close", header.transform, "", 34,
                new Color(0.62f, 0.45f, 0.30f));
            Anchor(close.transform as RectTransform, 0.90f, 0.08f, 0.99f, 0.92f);
            var closeImage = close.GetComponent<Image>();
            closeImage.sprite = LoadEncyclopediaSprite("encyclopedia_close_button");
            closeImage.type = Image.Type.Simple;
            closeImage.color = Color.white;
            close.transform.Find("Label").gameObject.SetActive(false);

            var summary = AddImage("Summary", card, null,
                new Color(1f, 0.97f, 0.88f, 0.96f), Image.Type.Sliced);
            Anchor(summary.rectTransform, 0.03f, 0.76f, 0.97f, 0.87f);

            var summaryBook = AddImage("SummaryBook", summary.transform,
                LoadEncyclopediaSprite("encyclopedia_closed_recipe_book"), Color.white, Image.Type.Simple);
            Anchor(summaryBook.rectTransform, 0.03f, 0.08f, 0.16f, 0.92f);
            summaryBook.preserveAspect = true;

            var track = AddImage("ProgressTrack", summary.transform,
                LoadSprite("ui_level_progress_track"), Color.white, Image.Type.Sliced);
            Anchor(track.rectTransform, 0.19f, 0.18f, 0.70f, 0.82f);
            var fill = AddImage("Fill", track.transform, null, GreenOk, Image.Type.Simple);
            Anchor(fill.rectTransform, 0.02f, 0.14f, 0.02f, 0.86f);

            var progress = AddText("ProgressText", summary.transform, "0/80",
                28, DarkBrown, TextAnchor.MiddleCenter);
            Anchor(progress.rectTransform, 0.24f, 0.18f, 0.65f, 0.82f);
            progress.fontStyle = FontStyle.Bold;

            var reward = AddText("RewardText", summary.transform,
                "+100", 30, new Color(0.12f, 0.48f, 0.12f), TextAnchor.MiddleLeft);
            Anchor(reward.rectTransform, 0.80f, 0.08f, 0.97f, 0.92f);
            reward.fontStyle = FontStyle.Bold;

            var rewardIcon = AddImage("RewardIcon", summary.transform,
                LoadEncyclopediaSprite("encyclopedia_cash_reward"), Color.white, Image.Type.Simple);
            Anchor(rewardIcon.rectTransform, 0.72f, 0.10f, 0.80f, 0.90f);
            rewardIcon.preserveAspect = true;

            var allRewardButton = summary.gameObject.AddComponent<Button>();
            allRewardButton.targetGraphic = summary.GetComponent<Image>();

            var scroll = AddImage("ChainScroll", card, null,
                new Color(0.98f, 0.84f, 0.66f, 0.18f), Image.Type.Sliced);
            Anchor(scroll.rectTransform, 0.03f, 0.04f, 0.97f, 0.74f);
            scroll.raycastTarget = true;
            var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 36f;

            var viewport = AddImage("Viewport", scroll.transform, null, Color.clear, Image.Type.Simple);
            Stretch(viewport.rectTransform);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = CreateRect("Content", viewport.transform);
            var contentRect = content.transform as RectTransform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            var vertical = content.AddComponent<VerticalLayoutGroup>();
            vertical.spacing = 12f;
            vertical.padding = new RectOffset(4, 4, 4, 4);
            vertical.childAlignment = TextAnchor.UpperCenter;
            vertical.childControlWidth = true;
            vertical.childControlHeight = false;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;
            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.viewport = viewport.rectTransform;
            scrollRect.content = contentRect;

            var catalog = new MergePrototype.Core.ItemCatalog();
            foreach (string chainId in catalog.AllItems
                .Where(def => def.ChainId != "special")
                .Select(def => def.ChainId)
                .Distinct())
            {
                var chain = AddImage($"Chain_{chainId}", content.transform, null,
                    CardWhite, Image.Type.Sliced);
                chain.sprite = LoadEncyclopediaSprite("encyclopedia_chain_card");
                chain.color = Color.white;
                var chainLayout = chain.gameObject.AddComponent<LayoutElement>();
                chainLayout.minHeight = 166f;
                chainLayout.preferredHeight = 166f;

                var chainIcon = AddImage("Icon", chain.transform, null, Color.white, Image.Type.Simple);
                Anchor(chainIcon.rectTransform, 0.02f, 0.78f, 0.11f, 0.98f);
                chainIcon.preserveAspect = true;
                var chainTitle = AddText("Title", chain.transform,
                    chainId == "drink" ? "DRINKS" : chainId.ToUpperInvariant(),
                    24, DarkBrown, TextAnchor.MiddleLeft);
                Anchor(chainTitle.rectTransform, 0.12f, 0.78f, 0.62f, 0.98f);
                chainTitle.fontStyle = FontStyle.Bold;
                var rewardButton = AddButton("RewardButton", chain.transform, string.Empty, 22,
                    Color.clear);
                Anchor(rewardButton.transform as RectTransform, 0.70f, 0.77f, 0.98f, 0.99f);
                var rewardButtonImage = rewardButton.GetComponent<Image>();
                rewardButtonImage.type = Image.Type.Sliced;
                rewardButtonImage.color = Color.clear;
                rewardButton.transform.Find("Label").gameObject.SetActive(false);

                var chainRewardIcon = AddImage("RewardIcon", rewardButton.transform,
                    LoadEncyclopediaSprite("encyclopedia_cash_reward"), Color.white, Image.Type.Simple);
                Anchor(chainRewardIcon.rectTransform, 0.02f, 0.08f, 0.30f, 0.92f);
                chainRewardIcon.preserveAspect = true;

                var chainRewardText = AddText("RewardText", rewardButton.transform, "+10", 22,
                    new Color(0.12f, 0.48f, 0.12f), TextAnchor.MiddleLeft);
                Anchor(chainRewardText.rectTransform, 0.30f, 0f, 1f, 1f);
                chainRewardText.fontStyle = FontStyle.Bold;

                var claimedIcon = AddText("ClaimedIcon", rewardButton.transform, "✓", 34,
                    new Color(0.12f, 0.58f, 0.16f), TextAnchor.MiddleCenter);
                Stretch(claimedIcon.rectTransform);
                claimedIcon.fontStyle = FontStyle.Bold;
                claimedIcon.gameObject.SetActive(false);

                var grid = CreateRect("Grid", chain.transform);
                Anchor(grid.transform as RectTransform, 0.02f, 0.04f, 0.98f, 0.76f);
                var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
                layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                layout.constraintCount = 8;
                layout.cellSize = new Vector2(104f, 104f);
                layout.spacing = new Vector2(5f, 5f);
                foreach (var def in catalog.AllItems)
                {
                    if (def.ChainId != chainId) continue;
                    var tile = AddImage($"Catalog_{def.Id}", grid.transform,
                        null, new Color(0.80f, 0.90f, 0.98f, 0.80f), Image.Type.Sliced);
                    var tileIcon = AddImage("Icon", tile.transform, null, Color.white, Image.Type.Simple);
                    Anchor(tileIcon.rectTransform, 0.08f, 0.22f, 0.92f, 0.96f);
                    tileIcon.preserveAspect = true;
                    var tileLabel = AddText("Name", tile.transform, "?", 24,
                        new Color(1f, 0.95f, 0.82f), TextAnchor.MiddleCenter);
                    Anchor(tileLabel.rectTransform, 0.02f, 0f, 0.98f, 0.28f);
                    tileLabel.fontStyle = FontStyle.Bold;
                }
            }
        }

        private static GameObject SavePrefab(GameObject source, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(source, path);
            return prefab;
        }

        private static Sprite LoadSprite(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{UiFolder}/{name}.png");
        }

        private static Sprite LoadLevelUpPopupSprite(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{LevelUpPopupUiFolder}/{name}.png");
        }

        private static Sprite LoadEncyclopediaSprite(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{EncyclopediaUiFolder}/{name}.png");
        }

        private static GameObject CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        private static Image AddImage(string name, Transform parent, Sprite sprite, Color color, Image.Type type)
        {
            var go = CreateRect(name, parent);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = type;
            image.raycastTarget = false;
            return image;
        }

        private static Text AddText(string name, Transform parent, string value, int size, Color color, TextAnchor anchor)
        {
            var go = CreateRect(name, parent);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static Button AddButton(string name, Transform parent, string label, int size, Color color)
        {
            var image = AddImage(name, parent, null, color, Image.Type.Sliced);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            var text = AddText("Label", image.transform, label, size, Color.white, TextAnchor.MiddleCenter);
            text.fontStyle = FontStyle.Bold;
            Stretch(text.rectTransform);
            return button;
        }

        private static void SetObject(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectList(Object target, string field, IEnumerable<Object> values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            var list = values.ToList();
            property.arraySize = list.Count;
            for (int i = 0; i < list.Count; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void Anchor(RectTransform rt, float minX, float minY, float maxX, float maxY)
        {
            rt.anchorMin = new Vector2(minX, minY);
            rt.anchorMax = new Vector2(maxX, maxY);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void AnchorTop(RectTransform rt, float posX, float posY, float width, float height)
        {
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(posX, posY);
            rt.sizeDelta = new Vector2(width, height);
        }

        private static void AnchorBottomFull(RectTransform rt, float height)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, height);
        }

        private static void AnchorBottomFixed(RectTransform rt, float posY, float posX, float width, float height)
        {
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(posX, posY);
            rt.sizeDelta = new Vector2(width, height);
        }

        private static void AnchorBottomStretch(RectTransform rt, float posY, float margin, float height)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, posY);
            rt.sizeDelta = new Vector2(-margin * 2f, height);
        }
    }
}
