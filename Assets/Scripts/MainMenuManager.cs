using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds and manages the main menu overlay entirely at runtime.
/// Add this component to any persistent GameObject in the game scene.
/// The game is frozen (timeScale=0) until Play is pressed.
/// </summary>
public class MainMenuManager : MonoBehaviour {

    // ── Panels ────────────────────────────────────────────────────────────────
    private GameObject menuPanel;
    private GameObject shopPanel;
    private GameObject scoresPanel;
    private GameObject vipPanel;
    private GameObject gameOverPanel;
    private GameObject characterSelectPanel;
    private GameObject settingsPanel;
    private System.Action settingsBackAction;

    private Canvas    canvas;
    private Font      font;

    // ── Colours ───────────────────────────────────────────────────────────────
    private static readonly Color BG_DARK   = new Color(0.05f, 0.05f, 0.10f, 0.97f);
    private static readonly Color BTN_IDLE  = new Color(0.22f, 0.42f, 0.72f, 1f);
    private static readonly Color BTN_EXIT  = new Color(0.45f, 0.12f, 0.12f, 1f);
    private static readonly Color GOLD_COL  = new Color(1f,   0.82f, 0.20f, 1f);
    private static readonly Color WHITE     = Color.white;

    // ── Upgrade definitions ───────────────────────────────────────────────────
    private struct UpgradeDef {
        public string key, label, desc;
        public int cost, maxLevel;
    }

    private static readonly UpgradeDef[] Upgrades = {
        new UpgradeDef { key="upg_speed",    label="Swift Boots",      desc="+1 move speed per level",   cost=50,  maxLevel=5 },
        new UpgradeDef { key="upg_hp",       label="Iron Constitution", desc="+10 max HP per level",      cost=75,  maxLevel=5 },
        new UpgradeDef { key="upg_cooldown", label="Quick Hands",      desc="-5% weapon cooldown/level", cost=100, maxLevel=3 },
        new UpgradeDef { key="upg_xprate",   label="Scholar's Mind",   desc="+10% XP gain per level",    cost=80,  maxLevel=5 },
    };

    // ── High score keys ───────────────────────────────────────────────────────
    private const int MAX_SCORES = 5;
    // Records stored as JSON: hs_{charName}_0 … hs_{charName}_4
    // "All" key stores the global top-5 across all characters.
    private string currentScoreFilter = "All";  // character filter for scores panel

    // ── Character select state ──
    private string[] availableCharacters;
    private int selectedCharacterIdx = 0;

    // ═════════════════════════════════════════════════════════════════════════
    void Start() {
        Time.timeScale = 0f;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Find or create a dedicated menu canvas (sits above game HUD)
        GameObject cgo = new GameObject("MainMenuCanvas");
        canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        cgo.AddComponent<CanvasScaler>();
        cgo.AddComponent<GraphicRaycaster>();

        settingsBackAction = () => ShowPanel(menuPanel);
        BuildMainMenu();
        BuildUpgradeShop();
        BuildHighScores();
        BuildManageVIP();
        BuildGameOver();
        BuildCharacterSelect();
        BuildSettings();

        ShowPanel(menuPanel);
        // Hide the player character until Play is pressed (player created in Awake, before Start)
        SurvivorMasterScript sms = SurvivorMasterScript.Instance;
        if (sms != null && sms.player != null) sms.player.gameObject.SetActive(false);
    }

    void SetGameplayUIVisible(bool visible) {
        if (MinimapSystem.Instance != null) MinimapSystem.Instance.SetVisible(visible);
        GameHUD hud = Object.FindFirstObjectByType<GameHUD>();
        if (hud != null) hud.SetVisible(visible);
        SurvivorMasterScript sms = SurvivorMasterScript.Instance;
        if (sms != null && sms.player != null) sms.player.gameObject.SetActive(visible);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // MAIN MENU
    // ═════════════════════════════════════════════════════════════════════════
    void BuildMainMenu() {
        menuPanel = MakeFullPanel("MainMenu");

        // Title
        MakeText(menuPanel.transform, "SURVIVORS", 52, FontStyle.Bold, WHITE,
            TextAnchor.MiddleCenter, new Vector2(0, 0.78f), new Vector2(0, 0.93f));
        MakeText(menuPanel.transform, "LIKE", 36, FontStyle.Italic, GOLD_COL,
            TextAnchor.MiddleCenter, new Vector2(0, 0.63f), new Vector2(0, 0.76f));

        // Buttons (6 total with Settings)
        float[] yAnchors = { 0.54f, 0.44f, 0.34f, 0.24f, 0.14f, 0.04f };
        string[] labels  = { "Play", "Upgrade Shop", "High Scores", "Manage VIP", "Settings", "Exit" };
        System.Action[] actions = {
            OnPlay,
            () => ShowPanel(shopPanel),
            () => { BuildHighScores(); ShowPanel(scoresPanel); },
            () => ShowPanel(vipPanel),
            () => { settingsBackAction = () => ShowPanel(menuPanel); BuildSettings(); ShowPanel(settingsPanel); },
            () => Application.Quit()
        };
        Color[] colors = {
            new Color(0.12f, 0.55f, 0.22f, 1f),  // Play - bright green
            BTN_IDLE, BTN_IDLE, BTN_IDLE,          // nav buttons
            new Color(0.35f, 0.25f, 0.65f, 1f),   // Settings - purple
            BTN_EXIT                               // Exit
        };

        for (int i = 0; i < labels.Length; i++) {
            int idx = i;
            float y = yAnchors[i];
            MakeButton(menuPanel.transform, labels[i], colors[i], WHITE, 22,
                new Vector2(0.3f, y), new Vector2(0.7f, y + 0.09f), actions[idx]);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // UPGRADE SHOP
    // ═════════════════════════════════════════════════════════════════════════
    void BuildUpgradeShop() {
        if (shopPanel != null) Destroy(shopPanel);
        shopPanel = MakeFullPanel("ShopPanel");

        // ── Title ──
        MakeText(shopPanel.transform, "Upgrade Shop", 32, FontStyle.Bold, GOLD_COL,
            TextAnchor.MiddleCenter, new Vector2(0, 0.88f), new Vector2(0, 0.97f));

        // ── Gold (prominent, at top) ──
        int gold  = PlayerPrefs.GetInt("TotalGold", 0);
        int spent = PlayerPrefs.GetInt("TotalGoldSpent", 0);
        // Top-right gold display matching the game HUD style
        MakeText(shopPanel.transform, $"\u25c6 {gold}", 22, FontStyle.Bold, GOLD_COL,
            TextAnchor.MiddleRight, new Vector2(0.65f, 0.91f), new Vector2(0.97f, 0.98f));
        MakeText(shopPanel.transform, $"\u25c6 {gold} gold", 26, FontStyle.Bold, GOLD_COL,
            TextAnchor.MiddleCenter, new Vector2(0, 0.80f), new Vector2(0, 0.89f));
        MakeText(shopPanel.transform, $"Spent this session: {spent}g", 15, FontStyle.Italic,
            new Color(0.75f, 0.75f, 0.75f, 1f),
            TextAnchor.MiddleCenter, new Vector2(0, 0.74f), new Vector2(0, 0.81f));

        float startY = 0.66f;
        float stepY  = 0.13f;

        for (int i = 0; i < Upgrades.Length; i++) {
            int idx  = i;
            var upg  = Upgrades[i];
            int lvl  = PlayerPrefs.GetInt(upg.key, 0);
            bool maxed = lvl >= upg.maxLevel;
            int  cost  = upg.cost * (lvl + 1);

            float yTop = startY - i * stepY;
            float yBot = yTop - stepY + 0.02f;

            // Row background
            MakeImage(shopPanel.transform, new Vector2(0.05f, yBot), new Vector2(0.95f, yTop),
                new Color(0.12f, 0.15f, 0.22f, 0.8f));

            // Label + desc
            MakeText(shopPanel.transform, $"{upg.label}  (Lv {lvl}/{upg.maxLevel})", 18, FontStyle.Bold, WHITE,
                TextAnchor.MiddleLeft, new Vector2(0.07f, yBot + 0.05f), new Vector2(0.65f, yTop));
            MakeText(shopPanel.transform, upg.desc, 14, FontStyle.Normal, new Color(0.7f, 0.85f, 1f, 1f),
                TextAnchor.MiddleLeft, new Vector2(0.07f, yBot), new Vector2(0.65f, yBot + 0.06f));

            // Buy button
            Color btnColor = maxed ? new Color(0.3f, 0.3f, 0.3f, 1f)
                           : gold >= cost ? new Color(0.12f, 0.45f, 0.18f, 1f)
                           : new Color(0.4f, 0.15f, 0.15f, 1f);
            string btnLabel = maxed ? "MAX" : $"Buy  {cost}g";
            MakeButton(shopPanel.transform, btnLabel, btnColor, WHITE, 16,
                new Vector2(0.67f, yBot + 0.02f), new Vector2(0.93f, yTop - 0.01f),
                maxed ? (System.Action)null : () => {
                    if (TrySpendGold(Upgrades[idx].cost * (PlayerPrefs.GetInt(Upgrades[idx].key, 0) + 1))) {
                        PlayerPrefs.SetInt(Upgrades[idx].key, PlayerPrefs.GetInt(Upgrades[idx].key, 0) + 1);
                        PlayerPrefs.Save();
                        BuildUpgradeShop();
                        ShowPanel(shopPanel);
                    }
                });
        }

        // ── Bottom row: Back | Refund All ──
        MakeButton(shopPanel.transform, "Back", BTN_IDLE, WHITE, 20,
            new Vector2(0.05f, 0.02f), new Vector2(0.45f, 0.10f),
            () => ShowPanel(menuPanel));
        string refundLabel = spent > 0 ? $"Refund All ({spent}g)" : "Refund All";
        MakeButton(shopPanel.transform, refundLabel,
            spent > 0 ? new Color(0.55f, 0.35f, 0.05f, 1f) : new Color(0.25f, 0.25f, 0.25f, 1f),
            WHITE, 20,
            new Vector2(0.55f, 0.02f), new Vector2(0.95f, 0.10f),
            spent > 0 ? (System.Action)ShowRefundConfirm : null);
    }

    void ShowRefundConfirm() {
        int spent = PlayerPrefs.GetInt("TotalGoldSpent", 0);

        // Full-screen dim overlay parented to main canvas (above shop)
        GameObject overlay = new GameObject("RefundConfirmOverlay");
        overlay.transform.SetParent(canvas.transform, false);
        RectTransform ovrt = overlay.AddComponent<RectTransform>();
        ovrt.anchorMin = Vector2.zero; ovrt.anchorMax = Vector2.one;
        ovrt.offsetMin = Vector2.zero; ovrt.offsetMax = Vector2.zero;
        overlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

        // Dialog box
        GameObject dialog = new GameObject("Dialog");
        dialog.transform.SetParent(overlay.transform, false);
        RectTransform drt = dialog.AddComponent<RectTransform>();
        drt.anchorMin = new Vector2(0.2f, 0.35f); drt.anchorMax = new Vector2(0.8f, 0.65f);
        drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;
        Image dbg = dialog.AddComponent<Image>();
        dbg.color = new Color(0.08f, 0.10f, 0.16f, 1f);

        MakeText(dialog.transform, "Refund All Upgrades?", 24, FontStyle.Bold, WHITE,
            TextAnchor.MiddleCenter, new Vector2(0.05f, 0.62f), new Vector2(0.95f, 0.95f));
        MakeText(dialog.transform, $"Refund {spent}g and reset all upgrade levels.",
            16, FontStyle.Normal, GOLD_COL,
            TextAnchor.MiddleCenter, new Vector2(0.05f, 0.38f), new Vector2(0.95f, 0.62f));

        MakeButton(dialog.transform, "Yes", new Color(0.12f, 0.45f, 0.18f, 1f), WHITE, 20,
            new Vector2(0.05f, 0.05f), new Vector2(0.45f, 0.35f), () => {
                int refund = PlayerPrefs.GetInt("TotalGoldSpent", 0);
                int newGold = PlayerPrefs.GetInt("TotalGold", 0) + refund;
                PlayerPrefs.SetInt("TotalGold", newGold);
                SurvivorMasterScript.GlobalGold = newGold;
                PlayerPrefs.SetInt("TotalGoldSpent", 0);
                foreach (var upg in Upgrades) PlayerPrefs.SetInt(upg.key, 0);
                PlayerPrefs.Save();
                Destroy(overlay);
                BuildUpgradeShop();
                ShowPanel(shopPanel);
            });
        MakeButton(dialog.transform, "No", BTN_EXIT, WHITE, 20,
            new Vector2(0.55f, 0.05f), new Vector2(0.95f, 0.35f),
            () => Destroy(overlay));
    }

    bool TrySpendGold(int amount) {
        int gold = PlayerPrefs.GetInt("TotalGold", 0);
        if (gold < amount) return false;
        gold -= amount;
        PlayerPrefs.SetInt("TotalGold", gold);
        SurvivorMasterScript.GlobalGold = gold;
        int spent = PlayerPrefs.GetInt("TotalGoldSpent", 0) + amount;
        PlayerPrefs.SetInt("TotalGoldSpent", spent);
        PlayerPrefs.Save();
        return true;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // HIGH SCORES
    // ═════════════════════════════════════════════════════════════════════════
    void BuildHighScores() {
        if (scoresPanel != null) Destroy(scoresPanel);
        scoresPanel = MakeFullPanel("ScoresPanel");

        MakeText(scoresPanel.transform, "High Scores", 36, FontStyle.Bold, GOLD_COL,
            TextAnchor.MiddleCenter, new Vector2(0, 0.88f), new Vector2(0, 0.97f));

        // ── Filter dropdown ─────────────────────────────────────────────────
        var options = new List<string> { "All" };
        foreach (var ch in GetAvailableCharacters()) options.Add(ch);

        const float dropRowH   = 0.065f;
        const int   maxDropVis = 7;
        int   clampedDrop  = Mathf.Min(options.Count, maxDropVis);
        float dropRowPx    = dropRowH * Screen.height;

        // Display bar (click to toggle list)
        GameObject filterBar = new GameObject("FilterDisplay");
        filterBar.transform.SetParent(scoresPanel.transform, false);
        RectTransform fbRT = filterBar.AddComponent<RectTransform>();
        fbRT.anchorMin = new Vector2(0.05f, 0.80f); fbRT.anchorMax = new Vector2(0.55f, 0.87f);
        fbRT.offsetMin = Vector2.zero; fbRT.offsetMax = Vector2.zero;
        filterBar.AddComponent<Image>().color = new Color(0.15f, 0.22f, 0.38f, 1f);
        Button fbBtn = filterBar.AddComponent<Button>();
        ColorBlock fbCB = fbBtn.colors;
        fbCB.normalColor      = new Color(0.15f, 0.22f, 0.38f, 1f);
        fbCB.highlightedColor = new Color(0.25f, 0.35f, 0.55f, 1f);
        fbCB.pressedColor     = new Color(0.10f, 0.16f, 0.28f, 1f);
        fbBtn.colors = fbCB;
        MakeText(filterBar.transform, "Filter:", 13, FontStyle.Normal,
            new Color(0.65f, 0.65f, 0.65f, 1f), TextAnchor.MiddleLeft,
            new Vector2(0.03f, 0.1f), new Vector2(0.26f, 0.9f));
        MakeText(filterBar.transform, currentScoreFilter, 18, FontStyle.Bold, WHITE,
            TextAnchor.MiddleLeft, new Vector2(0.27f, 0.05f), new Vector2(0.87f, 0.95f));
        MakeText(filterBar.transform, "\u25bc", 13, FontStyle.Normal,
            new Color(0.75f, 0.75f, 0.75f, 1f), TextAnchor.MiddleRight,
            new Vector2(0.87f, 0.1f), new Vector2(0.97f, 0.9f));

        // Scrollable drop list
        GameObject dropList = new GameObject("FilterDropList");
        dropList.transform.SetParent(scoresPanel.transform, false);
        RectTransform dlRT = dropList.AddComponent<RectTransform>();
        dlRT.anchorMin = new Vector2(0.05f, 0.80f - clampedDrop * dropRowH);
        dlRT.anchorMax = new Vector2(0.55f, 0.80f);
        dlRT.offsetMin = Vector2.zero; dlRT.offsetMax = Vector2.zero;
        dropList.AddComponent<Image>().color = new Color(0.08f, 0.12f, 0.20f, 0.98f);
        dropList.SetActive(false);

        var dlScroll = dropList.AddComponent<ScrollRect>();
        dlScroll.horizontal = false; dlScroll.vertical = true;
        dlScroll.scrollSensitivity = 30f;
        dlScroll.movementType = ScrollRect.MovementType.Clamped;

        GameObject dlVp = new GameObject("Viewport");
        dlVp.transform.SetParent(dropList.transform, false);
        RectTransform dlVpRT = dlVp.AddComponent<RectTransform>();
        dlVpRT.anchorMin = Vector2.zero; dlVpRT.anchorMax = Vector2.one;
        dlVpRT.offsetMin = Vector2.zero; dlVpRT.offsetMax = Vector2.zero;
        dlVp.AddComponent<RectMask2D>();

        GameObject dlCnt = new GameObject("Content");
        dlCnt.transform.SetParent(dlVp.transform, false);
        RectTransform dlCntRT = dlCnt.AddComponent<RectTransform>();
        dlCntRT.anchorMin = new Vector2(0f, 1f); dlCntRT.anchorMax = new Vector2(1f, 1f);
        dlCntRT.pivot = new Vector2(0.5f, 1f);
        dlCntRT.offsetMin = new Vector2(0f, -(options.Count * dropRowPx));
        dlCntRT.offsetMax = Vector2.zero;
        dlScroll.viewport = dlVpRT; dlScroll.content = dlCntRT;
        dlScroll.normalizedPosition = new Vector2(0, 1);

        Color hlCol  = new Color(0.12f, 0.45f, 0.18f, 1f);
        Color itemBg = new Color(0.12f, 0.18f, 0.28f, 1f);
        for (int i = 0; i < options.Count; i++) {
            string lbl = options[i];
            bool   sel = lbl == currentScoreFilter;
            GameObject optGO = new GameObject("Opt_" + lbl);
            optGO.transform.SetParent(dlCnt.transform, false);
            RectTransform ort = optGO.AddComponent<RectTransform>();
            ort.anchorMin = new Vector2(0f, 1f); ort.anchorMax = new Vector2(1f, 1f);
            ort.pivot = new Vector2(0.5f, 1f);
            ort.offsetMin = new Vector2(2f, -(i + 1) * dropRowPx + 1f);
            ort.offsetMax = new Vector2(-2f, -i * dropRowPx);
            Color oc = sel ? hlCol : itemBg;
            optGO.AddComponent<Image>().color = oc;
            Button ob = optGO.AddComponent<Button>();
            ColorBlock ocb = ob.colors;
            ocb.normalColor      = oc;
            ocb.highlightedColor = Color.Lerp(oc, Color.white, 0.25f);
            ocb.pressedColor     = Color.Lerp(oc, Color.black, 0.25f);
            ob.colors = ocb;
            ob.onClick.AddListener(() => {
                currentScoreFilter = lbl;
                BuildHighScores();
                ShowPanel(scoresPanel);
            });
            GameObject tgo = new GameObject("Label");
            tgo.transform.SetParent(optGO.transform, false);
            RectTransform trt = tgo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(8f, 2f); trt.offsetMax = new Vector2(-8f, -2f);
            Text t = tgo.AddComponent<Text>();
            t.text = lbl; t.font = font; t.fontSize = 18; t.color = WHITE;
            t.alignment = TextAnchor.MiddleLeft;
        }
        fbBtn.onClick.AddListener(() => {
            bool show = !dropList.activeSelf;
            dropList.SetActive(show);
            if (show) dropList.transform.SetAsLastSibling();
        });

        // ── Column header row (fixed, above scroll) ──────────────────────────
        float chBot = 0.78f, chTop = 0.795f;
        MakeText(scoresPanel.transform, "#",       12, FontStyle.Bold, GOLD_COL, TextAnchor.MiddleCenter, new Vector2(0.02f, chBot), new Vector2(0.06f, chTop));
        MakeText(scoresPanel.transform, "Char",    12, FontStyle.Bold, GOLD_COL, TextAnchor.MiddleLeft,   new Vector2(0.07f, chBot), new Vector2(0.22f, chTop));
        MakeText(scoresPanel.transform, "Lv",      12, FontStyle.Bold, GOLD_COL, TextAnchor.MiddleCenter, new Vector2(0.22f, chBot), new Vector2(0.30f, chTop));
        MakeText(scoresPanel.transform, "Time",    12, FontStyle.Bold, GOLD_COL, TextAnchor.MiddleCenter, new Vector2(0.30f, chBot), new Vector2(0.42f, chTop));
        MakeText(scoresPanel.transform, "Kills",   12, FontStyle.Bold, GOLD_COL, TextAnchor.MiddleCenter, new Vector2(0.42f, chBot), new Vector2(0.54f, chTop));
        MakeText(scoresPanel.transform, "Dmg Out", 12, FontStyle.Bold, GOLD_COL, TextAnchor.MiddleCenter, new Vector2(0.54f, chBot), new Vector2(0.70f, chTop));
        MakeText(scoresPanel.transform, "Dmg In",  12, FontStyle.Bold, GOLD_COL, TextAnchor.MiddleCenter, new Vector2(0.70f, chBot), new Vector2(0.86f, chTop));
        MakeText(scoresPanel.transform, "Gold",    12, FontStyle.Bold, GOLD_COL, TextAnchor.MiddleCenter, new Vector2(0.86f, chBot), new Vector2(0.99f, chTop));

        // ── Records ScrollRect ────────────────────────────────────────────────
        var records = LoadRecords(currentScoreFilter);
        if (records.Count == 0) {
            MakeText(scoresPanel.transform, "No runs recorded yet.", 18, FontStyle.Normal,
                new Color(0.6f, 0.6f, 0.6f, 1f), TextAnchor.MiddleCenter,
                new Vector2(0, 0.45f), new Vector2(0, 0.55f));
        } else {
            const float REC_ROW_PX = 52f;
            float totalRecH = records.Count * REC_ROW_PX;

            GameObject recScrGO = new GameObject("RecordsScroll");
            recScrGO.transform.SetParent(scoresPanel.transform, false);
            RectTransform rsRT = recScrGO.AddComponent<RectTransform>();
            rsRT.anchorMin = new Vector2(0f, 0.11f); rsRT.anchorMax = new Vector2(1f, 0.778f);
            rsRT.offsetMin = Vector2.zero; rsRT.offsetMax = Vector2.zero;
            recScrGO.AddComponent<Image>().color = Color.clear;
            var rs = recScrGO.AddComponent<ScrollRect>();
            rs.horizontal = false; rs.vertical = true;
            rs.scrollSensitivity = 30f;
            rs.movementType = ScrollRect.MovementType.Clamped;

            GameObject rsVp = new GameObject("Viewport");
            rsVp.transform.SetParent(recScrGO.transform, false);
            RectTransform rsVpRT = rsVp.AddComponent<RectTransform>();
            rsVpRT.anchorMin = Vector2.zero; rsVpRT.anchorMax = Vector2.one;
            rsVpRT.offsetMin = Vector2.zero; rsVpRT.offsetMax = Vector2.zero;
            rsVp.AddComponent<RectMask2D>();

            GameObject rsCnt = new GameObject("Content");
            rsCnt.transform.SetParent(rsVp.transform, false);
            RectTransform rsCntRT = rsCnt.AddComponent<RectTransform>();
            rsCntRT.anchorMin = new Vector2(0f, 1f); rsCntRT.anchorMax = new Vector2(1f, 1f);
            rsCntRT.pivot = new Vector2(0.5f, 1f);
            rsCntRT.offsetMin = new Vector2(0f, -totalRecH);
            rsCntRT.offsetMax = Vector2.zero;
            rs.viewport = rsVpRT; rs.content = rsCntRT;
            rs.normalizedPosition = new Vector2(0, 1);
            Transform rsCt = rsCnt.transform;

            void Cell(float rowTop, float rowBot, string txt, int fs,
                      TextAnchor align, float x0, float x1, Color color) {
                GameObject cgo = new GameObject("Cell");
                cgo.transform.SetParent(rsCt, false);
                RectTransform crt = cgo.AddComponent<RectTransform>();
                crt.anchorMin = new Vector2(x0, 1f); crt.anchorMax = new Vector2(x1, 1f);
                crt.pivot = new Vector2((x0 + x1) * 0.5f, 1f);
                crt.offsetMin = new Vector2(2f, -(rowBot - 2f));
                crt.offsetMax = new Vector2(-2f, -(rowTop + 2f));
                Text tx = cgo.AddComponent<Text>();
                tx.text = txt; tx.font = font; tx.fontSize = fs;
                tx.color = color; tx.alignment = align;
            }

            for (int i = 0; i < records.Count; i++) {
                var r = records[i];
                float rowTop = i * REC_ROW_PX;
                float rowBot = rowTop + REC_ROW_PX;
                Color rowCol = i == 0 ? GOLD_COL : WHITE;
                string rankStr = i < 3 ? new[] { "1", "2", "3" }[i] : $"{i + 1}";
                string timeStr = $"{r.timePlayed / 60:00}:{r.timePlayed % 60:00}";

                // Stripe background
                GameObject bgGO = new GameObject("RowBg");
                bgGO.transform.SetParent(rsCt, false);
                RectTransform bgRT = bgGO.AddComponent<RectTransform>();
                bgRT.anchorMin = new Vector2(0f, 1f); bgRT.anchorMax = new Vector2(1f, 1f);
                bgRT.pivot = new Vector2(0.5f, 1f);
                bgRT.offsetMin = new Vector2(1f, -(rowBot - 1f));
                bgRT.offsetMax = new Vector2(-1f, -rowTop);
                bgGO.AddComponent<Image>().color =
                    new Color(0.12f, 0.15f, 0.22f, i % 2 == 0 ? 0.6f : 0.3f);

                FontStyle fs = i == 0 ? FontStyle.Bold : FontStyle.Normal;
                Cell(rowTop, rowBot, rankStr,               i == 0 ? 15 : 14, TextAnchor.MiddleCenter, 0.02f, 0.06f, rowCol);
                Cell(rowTop, rowBot, r.characterName,       13, TextAnchor.MiddleLeft,   0.07f, 0.22f, rowCol);
                Cell(rowTop, rowBot, r.level.ToString(),    13, TextAnchor.MiddleCenter, 0.22f, 0.30f, rowCol);
                Cell(rowTop, rowBot, timeStr,               13, TextAnchor.MiddleCenter, 0.30f, 0.42f, rowCol);
                Cell(rowTop, rowBot, r.enemiesKilled.ToString(), 13, TextAnchor.MiddleCenter, 0.42f, 0.54f, rowCol);
                Cell(rowTop, rowBot, r.damageDealt.ToString(),   13, TextAnchor.MiddleCenter, 0.54f, 0.70f, rowCol);
                Cell(rowTop, rowBot, r.damageReceived.ToString(),13, TextAnchor.MiddleCenter, 0.70f, 0.86f, rowCol);
                Cell(rowTop, rowBot, r.goldGained.ToString(),    13, TextAnchor.MiddleCenter, 0.86f, 0.99f, rowCol);
            }
        }

        MakeButton(scoresPanel.transform, "Back", BTN_IDLE, WHITE, 20,
            new Vector2(0.35f, 0.01f), new Vector2(0.65f, 0.09f),
            () => ShowPanel(menuPanel));
    }

    // ── Record persistence ────────────────────────────────────────────────────
    static List<RunRecord> LoadRecords(string characterFilter) {
        string key = $"hs_{(characterFilter == "All" ? "All" : characterFilter)}_";
        var list = new List<RunRecord>();
        for (int i = 0; i < MAX_SCORES; i++) {
            string json = PlayerPrefs.GetString(key + i, "");
            if (!string.IsNullOrEmpty(json)) {
                try { list.Add(JsonUtility.FromJson<RunRecord>(json)); } catch { }
            }
        }
        // Sort by time descending as primary ranking
        list.Sort((a, b) => b.timePlayed.CompareTo(a.timePlayed));
        return list;
    }

    /// <summary>Persist a RunRecord into per-character AND global "All" leaderboards.</summary>
    public static void SubmitRecord(RunRecord record) {
        PersistRecordToKey(record, $"hs_{record.characterName}_");
        PersistRecordToKey(record, "hs_All_");
        PlayerPrefs.Save();
    }

    static void PersistRecordToKey(RunRecord record, string keyPrefix) {
        var list = new List<RunRecord>();
        for (int i = 0; i < MAX_SCORES; i++) {
            string json = PlayerPrefs.GetString(keyPrefix + i, "");
            if (!string.IsNullOrEmpty(json))
                try { list.Add(JsonUtility.FromJson<RunRecord>(json)); } catch { }
        }
        list.Add(record);
        list.Sort((a, b) => b.timePlayed.CompareTo(a.timePlayed));
        for (int i = 0; i < Mathf.Min(list.Count, MAX_SCORES); i++)
            PlayerPrefs.SetString(keyPrefix + i, JsonUtility.ToJson(list[i]));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // MANAGE VIP
    // ═════════════════════════════════════════════════════════════════════════
    void BuildManageVIP() {
        if (vipPanel != null) Destroy(vipPanel);
        vipPanel = MakeFullPanel("VIPPanel");

        MakeText(vipPanel.transform, "VIP Status", 36, FontStyle.Bold, GOLD_COL,
            TextAnchor.MiddleCenter, new Vector2(0, 0.82f), new Vector2(0, 0.94f));

        long expiry = long.Parse(PlayerPrefs.GetString("vip_expiry", "0"));
        long now    = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        bool isVIP  = expiry > now;
        int  daysLeft = isVIP ? (int)((expiry - now) / 86400) + 1 : 0;

        string statusText = isVIP
            ? $"✓ VIP Active — {daysLeft} day{(daysLeft != 1 ? "s" : "")} remaining"
            : "✗ No active VIP subscription";
        Color statusColor = isVIP ? new Color(0.3f, 1f, 0.5f, 1f) : new Color(0.8f, 0.3f, 0.3f, 1f);

        MakeText(vipPanel.transform, statusText, 20, FontStyle.Bold, statusColor,
            TextAnchor.MiddleCenter, new Vector2(0, 0.68f), new Vector2(0, 0.78f));

        MakeText(vipPanel.transform,
            "VIP Benefits:\n• Double gold from all sources\n• Exclusive cosmetic badge\n• Priority matchmaking (coming soon)",
            17, FontStyle.Normal, WHITE, TextAnchor.MiddleLeft,
            new Vector2(0.15f, 0.42f), new Vector2(0.85f, 0.66f));

        // Watch Ad button
        MakeButton(vipPanel.transform, "Watch Ad for +1 Day VIP",
            new Color(0.55f, 0.20f, 0.80f, 1f), WHITE, 20,
            new Vector2(0.2f, 0.28f), new Vector2(0.8f, 0.38f),
            OnWatchAdForVIP);

        MakeText(vipPanel.transform, "(Ad integration placeholder — grants VIP immediately in dev mode)",
            12, FontStyle.Italic, new Color(0.5f, 0.5f, 0.5f, 1f),
            TextAnchor.MiddleCenter, new Vector2(0, 0.20f), new Vector2(0, 0.27f));

        MakeButton(vipPanel.transform, "Back", BTN_IDLE, WHITE, 20,
            new Vector2(0.35f, 0.02f), new Vector2(0.65f, 0.10f),
            () => ShowPanel(menuPanel));
    }

    void OnWatchAdForVIP() {
        // Placeholder: in production, invoke ad SDK here.
        // For now, grant 1 day immediately.
        long now    = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long expiry = long.Parse(PlayerPrefs.GetString("vip_expiry", "0"));
        long current = Mathf.Max((int)now, (int)expiry);
        long newExpiry = current + 86400; // +1 day
        PlayerPrefs.SetString("vip_expiry", newExpiry.ToString());
        PlayerPrefs.Save();
        BuildManageVIP();
        ShowPanel(vipPanel);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // GAME OVER
    // ═════════════════════════════════════════════════════════════════════════
    void BuildGameOver(RunRecord record = null) {
        if (gameOverPanel != null) Destroy(gameOverPanel);
        gameOverPanel = MakeFullPanel("GameOverPanel");

        MakeText(gameOverPanel.transform, "YOU DIED", 56, FontStyle.Bold, new Color(0.9f, 0.1f, 0.1f, 1f),
            TextAnchor.MiddleCenter, new Vector2(0, 0.84f), new Vector2(0, 0.97f));

        if (record != null) {
            // ── Run summary panel ──
            MakeImage(gameOverPanel.transform, new Vector2(0.05f, 0.38f), new Vector2(0.95f, 0.82f),
                new Color(0.08f, 0.10f, 0.18f, 0.9f));

            string timeStr = $"{record.timePlayed/60:00}:{record.timePlayed%60:00}";
            float  rowH    = 0.07f;
            float  startY  = 0.77f;
            (string label, string value)[] rows = {
                ("Character",       record.characterName),
                ("Level Reached",   record.level.ToString()),
                ("Survival Time",   timeStr),
                ("Enemies Killed",  record.enemiesKilled.ToString()),
                ("Damage Dealt",    record.damageDealt.ToString()),
                ("Damage Received", record.damageReceived.ToString()),
                ("XP Gained",       record.xpGained.ToString()),
                ("Gold Gained",     record.goldGained.ToString()),
            };
            for (int i = 0; i < rows.Length; i++) {
                float yTop = startY - i * rowH;
                float yBot = yTop   - rowH + 0.005f;
                Color vc   = i == 0 ? GOLD_COL : WHITE;
                MakeText(gameOverPanel.transform, rows[i].label, 16, FontStyle.Bold,
                    new Color(0.7f, 0.85f, 1f, 1f), TextAnchor.MiddleLeft,
                    new Vector2(0.10f, yBot), new Vector2(0.55f, yTop));
                MakeText(gameOverPanel.transform, rows[i].value, 16, FontStyle.Bold,
                    vc, TextAnchor.MiddleRight,
                    new Vector2(0.55f, yBot), new Vector2(0.90f, yTop));
            }
        } else {
            MakeText(gameOverPanel.transform, "Your survival time has been recorded.",
                16, FontStyle.Italic, new Color(0.7f, 0.7f, 0.7f, 1f),
                TextAnchor.MiddleCenter, new Vector2(0.1f, 0.72f), new Vector2(0.9f, 0.82f));
        }

        // ── Buttons ──
        MakeButton(gameOverPanel.transform, "Play Again",
            new Color(0.12f, 0.45f, 0.18f, 1f), WHITE, 22,
            new Vector2(0.05f, 0.22f), new Vector2(0.47f, 0.35f),
            OnPlayAgain);

        MakeButton(gameOverPanel.transform, "Main Menu",
            BTN_IDLE, WHITE, 22,
            new Vector2(0.53f, 0.22f), new Vector2(0.95f, 0.35f),
            () => {
                EnemySpawner sp = Object.FindFirstObjectByType<EnemySpawner>();
                if (sp != null) sp.StopSpawning();
                canvas.gameObject.SetActive(true);
                BuildHighScores();
                ShowPanel(menuPanel);
            });

        MakeButton(gameOverPanel.transform, "View High Scores",
            new Color(0.20f, 0.20f, 0.35f, 1f), WHITE, 18,
            new Vector2(0.25f, 0.10f), new Vector2(0.75f, 0.20f),
            () => { BuildHighScores(); ShowPanel(scoresPanel); });
    }

    void OnPlayAgain() {
        // Stop existing enemies
        EnemySpawner spawner = Object.FindFirstObjectByType<EnemySpawner>();
        if (spawner != null) spawner.StopSpawning();

        // Kill all gems
        XpGem.Init();

        // Reset weapon system: clear levels, active weapons, orbs, cooldowns.
        WeaponSystem ws = WeaponSystem.Instance;
        if (ws != null) ws.ResetForNewRun();

        // Reset run state (stats, timer, regen, XP, HP).
        SurvivorMasterScript sms = SurvivorMasterScript.Instance;
        if (sms != null) {
            sms.ResetRunStats();
            RunUpgrades.Reset();
            // Re-assign the starting weapon for the current class.
            if (ZenithDatabaseLoader.Instance != null)
                ZenithDatabaseLoader.Instance.AssignStartingWeapon();
            if (sms.player != null)
                XpGem.SpawnStartingGems(sms.player.position, 3);
        }

        // Start spawner again
        if (spawner != null) spawner.StartSpawning();

        SetGameplayUIVisible(true);
        Time.timeScale = 1f;
        canvas.gameObject.SetActive(false);
    }

    /// <summary>Called by SurvivorMasterScript when the player dies.</summary>
    public void ShowGameOver(RunRecord record = null) {
        // Persist gold earned this run before anything else
        PlayerPrefs.SetInt("TotalGold", SurvivorMasterScript.GlobalGold);
        PlayerPrefs.Save();

        if (record != null) SubmitRecord(record);
        SetGameplayUIVisible(false);
        canvas.gameObject.SetActive(true);
        BuildGameOver(record);
        ShowPanel(gameOverPanel);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PLAY
    // ═════════════════════════════════════════════════════════════════════════
    void OnPlay() {
        BuildCharacterSelect();
        ShowPanel(characterSelectPanel);
    }

    void BuildCharacterSelect() {
        if (characterSelectPanel != null) Destroy(characterSelectPanel);
        characterSelectPanel = MakeFullPanel("CharacterSelectPanel");

        MakeText(characterSelectPanel.transform, "Select Your Character", 36, FontStyle.Bold, GOLD_COL,
            TextAnchor.MiddleCenter, new Vector2(0, 0.82f), new Vector2(0, 0.94f));

        availableCharacters = GetAvailableCharacters();
        string prev = PlayerPrefs.GetString("SelectedCharacter", "Mage");
        selectedCharacterIdx = System.Array.IndexOf(availableCharacters, prev);
        if (selectedCharacterIdx < 0) selectedCharacterIdx = 0;

        Color highlightCol = new Color(0.12f, 0.45f, 0.18f, 1f);
        Color rowCol       = new Color(0.12f, 0.18f, 0.28f, 1f);
        string initLabel   = availableCharacters.Length > 0 ? availableCharacters[selectedCharacterIdx] : "— None —";

        // ── Display bar (click to open/close list) ──────────────────────────
        GameObject displayBar = new GameObject("DropDisplay");
        displayBar.transform.SetParent(characterSelectPanel.transform, false);
        RectTransform dbRT = displayBar.AddComponent<RectTransform>();
        dbRT.anchorMin = new Vector2(0.2f, 0.56f);
        dbRT.anchorMax = new Vector2(0.8f, 0.64f);
        dbRT.offsetMin = Vector2.zero; dbRT.offsetMax = Vector2.zero;
        displayBar.AddComponent<Image>().color = new Color(0.15f, 0.22f, 0.38f, 1f);
        Button dbBtn = displayBar.AddComponent<Button>();
        ColorBlock dbCB = dbBtn.colors;
        dbCB.normalColor      = new Color(0.15f, 0.22f, 0.38f, 1f);
        dbCB.highlightedColor = new Color(0.25f, 0.35f, 0.55f, 1f);
        dbCB.pressedColor     = new Color(0.10f, 0.16f, 0.28f, 1f);
        dbBtn.colors = dbCB;

        // Label showing current selection
        Text selLabel = MakeText(displayBar.transform, initLabel, 22, FontStyle.Bold, WHITE,
            TextAnchor.MiddleLeft, new Vector2(0.05f, 0f), new Vector2(0.85f, 1f));
        // Arrow indicator
        MakeText(displayBar.transform, "\u25bc", 18, FontStyle.Normal, new Color(0.75f, 0.75f, 0.75f, 1f),
            TextAnchor.MiddleRight, new Vector2(0.85f, 0.1f), new Vector2(0.97f, 0.9f));

        // ── Scrollable drop list ─────────────────────────────────────────────
        const float rowH       = 0.07f;
        const int   maxVisible = 6;
        int   clampedVisible   = Mathf.Min(availableCharacters.Length, maxVisible);
        float rowPx            = rowH * Screen.height;  // pixel height of one visible row

        // Outer panel — fixed height = clampedVisible rows, sits just below the display bar.
        GameObject dropList = new GameObject("DropList");
        dropList.transform.SetParent(characterSelectPanel.transform, false);
        RectTransform dlRT = dropList.AddComponent<RectTransform>();
        dlRT.anchorMin = new Vector2(0.2f, 0.56f - clampedVisible * rowH);
        dlRT.anchorMax = new Vector2(0.8f, 0.56f);
        dlRT.offsetMin = Vector2.zero; dlRT.offsetMax = Vector2.zero;
        dropList.AddComponent<Image>().color = new Color(0.08f, 0.12f, 0.20f, 0.98f);
        dropList.SetActive(false);

        // ScrollRect drives vertical scrolling of the content.
        var scroll = dropList.AddComponent<ScrollRect>();
        scroll.horizontal        = false;
        scroll.vertical          = true;
        scroll.scrollSensitivity = 30f;
        scroll.movementType      = ScrollRect.MovementType.Clamped;

        // Viewport — masks content that extends beyond the visible window.
        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(dropList.transform, false);
        RectTransform vpRT = viewport.AddComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero; vpRT.offsetMax = Vector2.zero;
        viewport.AddComponent<RectMask2D>();

        // Content — tall enough to hold all items; ScrollRect moves this inside the viewport.
        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        RectTransform contRT = content.AddComponent<RectTransform>();
        contRT.anchorMin = new Vector2(0f, 1f);
        contRT.anchorMax = new Vector2(1f, 1f);
        contRT.pivot     = new Vector2(0.5f, 1f);
        contRT.offsetMin = new Vector2(0f, -(availableCharacters.Length * rowPx));
        contRT.offsetMax = new Vector2(0f, 0f);

        scroll.viewport = vpRT;
        scroll.content  = contRT;
        scroll.normalizedPosition = new Vector2(0, 1);

        // One button per character, stacked inside content.
        Image[] itemImgs = new Image[availableCharacters.Length];
        for (int i = 0; i < availableCharacters.Length; i++) {
            int idx = i;

            GameObject itemGO = new GameObject("Item_" + availableCharacters[i]);
            itemGO.transform.SetParent(content.transform, false);
            RectTransform irt = itemGO.AddComponent<RectTransform>();
            // Anchor to top-left corner; use offsets for absolute pixel placement.
            irt.anchorMin = new Vector2(0f, 1f);
            irt.anchorMax = new Vector2(1f, 1f);
            irt.pivot     = new Vector2(0.5f, 1f);
            irt.offsetMin = new Vector2( 2f, -(i + 1) * rowPx + 1f);
            irt.offsetMax = new Vector2(-2f, -i * rowPx);

            Image imgComp = itemGO.AddComponent<Image>();
            imgComp.color = idx == selectedCharacterIdx ? highlightCol : rowCol;
            itemImgs[i]   = imgComp;

            Button itemBtn = itemGO.AddComponent<Button>();
            ColorBlock icb = itemBtn.colors;
            icb.normalColor      = imgComp.color;
            icb.highlightedColor = Color.Lerp(imgComp.color, Color.white, 0.25f);
            icb.pressedColor     = Color.Lerp(imgComp.color, Color.black, 0.25f);
            itemBtn.colors = icb;
            itemBtn.onClick.AddListener(() => {
                selectedCharacterIdx = idx;
                selLabel.text = availableCharacters[idx];
                for (int j = 0; j < itemImgs.Length; j++)
                    if (itemImgs[j] != null)
                        itemImgs[j].color = j == idx ? highlightCol : rowCol;
                dropList.SetActive(false);
            });

            GameObject tgo = new GameObject("Label");
            tgo.transform.SetParent(itemGO.transform, false);
            RectTransform trt = tgo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(8f, 2f); trt.offsetMax = new Vector2(-8f, -2f);
            Text t = tgo.AddComponent<Text>();
            t.text = availableCharacters[i]; t.font = font;
            t.fontSize = 20; t.color = WHITE;
            t.alignment = TextAnchor.MiddleLeft;
        }

        // Toggle list; bring to front so it renders above Start/Back buttons.
        dbBtn.onClick.AddListener(() => {
            bool show = !dropList.activeSelf;
            dropList.SetActive(show);
            if (show) dropList.transform.SetAsLastSibling();
        });

        // Start Game / Back
        MakeButton(characterSelectPanel.transform, "Start Game", new Color(0.12f, 0.45f, 0.18f, 1f), WHITE, 24,
            new Vector2(0.32f, 0.08f), new Vector2(0.68f, 0.16f), OnCharacterSelected);
        MakeButton(characterSelectPanel.transform, "Back", BTN_IDLE, WHITE, 20,
            new Vector2(0.05f, 0.02f), new Vector2(0.30f, 0.08f), () => ShowPanel(menuPanel));
    }

    void OnCharacterSelected() {
        if (availableCharacters == null || availableCharacters.Length == 0) return;
        string chosen = availableCharacters[selectedCharacterIdx];
        PlayerPrefs.SetString("SelectedCharacter", chosen);
        PlayerPrefs.Save();
        // Reload sprites for the chosen character (player is inactive during menus, so include inactive)
        PlayerAnimator anim = Object.FindFirstObjectByType<PlayerAnimator>(FindObjectsInactive.Include);
        if (anim != null) anim.LoadClipsForCharacter(chosen);
        // Reset per-run bonuses and run stats
        RunUpgrades.Reset();
        SurvivorMasterScript sms = SurvivorMasterScript.Instance;
        if (sms != null) {
            sms.ResetRunStats();
            // Set the character class so starting weapons and ultimates work correctly.
            if (System.Enum.TryParse<CharacterClass>(chosen, true, out CharacterClass cls))
                sms.currentClass = cls;
            // Assign the class-specific starting weapon now that the class is known.
            if (ZenithDatabaseLoader.Instance != null)
                ZenithDatabaseLoader.Instance.AssignStartingWeapon();
            if (sms.player != null)
                XpGem.SpawnStartingGems(sms.player.position, 3);
        }
        // Start enemy spawner
        EnemySpawner spawner = Object.FindFirstObjectByType<EnemySpawner>();
        if (spawner != null) spawner.StartSpawning();
        // Show gameplay UI and player, start the game
        SetGameplayUIVisible(true);
        Time.timeScale = 1f;
        canvas.gameObject.SetActive(false);
    }

    string[] GetAvailableCharacters() {
        // All values in the CharacterClass enum are selectable.
        // Characters without a sprite folder simply use the default idle animation.
        var names = System.Enum.GetNames(typeof(CharacterClass));
        System.Array.Sort(names, System.StringComparer.OrdinalIgnoreCase);
        return names;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // SETTINGS
    // ═════════════════════════════════════════════════════════════════════════
    void BuildSettings() {
        if (settingsPanel != null) Destroy(settingsPanel);
        settingsPanel = MakeFullPanel("SettingsPanel");

        MakeText(settingsPanel.transform, "Settings", 38, FontStyle.Bold, GOLD_COL,
            TextAnchor.MiddleCenter, new Vector2(0, 0.90f), new Vector2(0, 0.98f));

        MakeButton(settingsPanel.transform, "Apply & Back", BTN_IDLE, WHITE, 22,
            new Vector2(0.3f, 0.005f), new Vector2(0.7f, 0.065f),
            () => { ApplySettings(); settingsBackAction?.Invoke(); });

        // ── Scroll region (below title, above Back button) ────────────────────
        GameObject scrollGO = new GameObject("SettingsScroll");
        scrollGO.transform.SetParent(settingsPanel.transform, false);
        RectTransform scrollRT = scrollGO.AddComponent<RectTransform>();
        scrollRT.anchorMin = new Vector2(0f, 0.08f); scrollRT.anchorMax = new Vector2(1f, 0.89f);
        scrollRT.offsetMin = Vector2.zero; scrollRT.offsetMax = Vector2.zero;
        scrollGO.AddComponent<Image>().color = Color.clear;
        var sc = scrollGO.AddComponent<ScrollRect>();
        sc.horizontal = false; sc.vertical = true;
        sc.scrollSensitivity = 40f; sc.movementType = ScrollRect.MovementType.Clamped;

        GameObject vpGO = new GameObject("Viewport");
        vpGO.transform.SetParent(scrollGO.transform, false);
        RectTransform vpRT = vpGO.AddComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero; vpRT.offsetMax = Vector2.zero;
        vpGO.AddComponent<RectMask2D>();

        GameObject cntGO = new GameObject("Content");
        cntGO.transform.SetParent(vpGO.transform, false);
        RectTransform cntRT = cntGO.AddComponent<RectTransform>();
        cntRT.anchorMin = new Vector2(0f, 1f); cntRT.anchorMax = new Vector2(1f, 1f);
        cntRT.pivot = new Vector2(0.5f, 1f);
        sc.viewport = vpRT; sc.content = cntRT;
        Transform ct = cntGO.transform;

        float cur = 10f;
        const float HEADER_H  = 48f;
        const float DIVIDER_H = 4f;
        const float STEPPER_H = 80f;
        const float TOGGLE_H  = 60f;
        const float LAYOUT_H  = 72f;
        const float SOON_H    = 46f;
        const float SPACER_H  = 18f;

        // Returns a row container anchored at the current pixel offset in content.
        // The row spans 0.04–0.96 of content width (4% side indent).
        Transform Row(float h) {
            GameObject go = new GameObject("Row");
            go.transform.SetParent(ct, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.04f, 1f); rt.anchorMax = new Vector2(0.96f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0f, -(cur + h)); rt.offsetMax = new Vector2(0f, -cur);
            cur += h;
            return go.transform;
        }

        // Full-width row (0–1) — used for divider line.
        Transform FullRow(float h) {
            GameObject go = new GameObject("FullRow");
            go.transform.SetParent(ct, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0f, -(cur + h)); rt.offsetMax = new Vector2(0f, -cur);
            cur += h;
            return go.transform;
        }

        void Spacer() { cur += SPACER_H; }

        void SectionHeader(string title) {
            Transform r = Row(HEADER_H);
            MakeText(r, title, 22, FontStyle.Bold, new Color(0.55f, 0.82f, 1f),
                TextAnchor.MiddleLeft, new Vector2(0.02f, 0.1f), new Vector2(0.5f, 0.9f));
            Transform d = FullRow(DIVIDER_H);
            MakeImage(d, new Vector2(0.05f, 0.1f), new Vector2(0.95f, 0.9f),
                new Color(0.4f, 0.6f, 0.9f, 0.6f));
        }

        // ── AUDIO ──────────────────────────────────────────────────────────────
        SectionHeader("AUDIO");

        float sfxVol = PlayerPrefs.GetFloat("sfxVolume", 0.8f);
        {
            Transform r = Row(STEPPER_H);
            MakeImage(r, Vector2.zero, Vector2.one, new Color(0.12f, 0.15f, 0.22f, 0.8f));
            MakeText(r, "Sound Effects", 17, FontStyle.Normal, WHITE,
                TextAnchor.MiddleLeft, new Vector2(0.02f, 0.1f), new Vector2(0.52f, 0.9f));
            MakeButton(r, "\u25c4", new Color(0.20f, 0.28f, 0.48f, 1f), WHITE, 20,
                new Vector2(0.54f, 0.1f), new Vector2(0.64f, 0.9f),
                () => { PlayerPrefs.SetFloat("sfxVolume", Mathf.Max(0f, Mathf.Round((sfxVol - 0.1f) * 10f) / 10f)); ApplySettings(); BuildSettings(); ShowPanel(settingsPanel); });
            MakeText(r, $"{Mathf.RoundToInt(sfxVol * 100)}%", 18, FontStyle.Bold, GOLD_COL,
                TextAnchor.MiddleCenter, new Vector2(0.64f, 0.05f), new Vector2(0.78f, 0.95f));
            MakeButton(r, "\u25ba", new Color(0.20f, 0.28f, 0.48f, 1f), WHITE, 20,
                new Vector2(0.78f, 0.1f), new Vector2(0.88f, 0.9f),
                () => { PlayerPrefs.SetFloat("sfxVolume", Mathf.Min(1f, Mathf.Round((sfxVol + 0.1f) * 10f) / 10f)); ApplySettings(); BuildSettings(); ShowPanel(settingsPanel); });
        }

        float musicVol = PlayerPrefs.GetFloat("musicVolume", 0.5f);
        {
            Transform r = Row(STEPPER_H);
            MakeImage(r, Vector2.zero, Vector2.one, new Color(0.12f, 0.15f, 0.22f, 0.8f));
            MakeText(r, "Background Music", 17, FontStyle.Normal, WHITE,
                TextAnchor.MiddleLeft, new Vector2(0.02f, 0.1f), new Vector2(0.52f, 0.9f));
            MakeButton(r, "\u25c4", new Color(0.20f, 0.28f, 0.48f, 1f), WHITE, 20,
                new Vector2(0.54f, 0.1f), new Vector2(0.64f, 0.9f),
                () => { PlayerPrefs.SetFloat("musicVolume", Mathf.Max(0f, Mathf.Round((musicVol - 0.1f) * 10f) / 10f)); ApplySettings(); BuildSettings(); ShowPanel(settingsPanel); });
            MakeText(r, $"{Mathf.RoundToInt(musicVol * 100)}%", 18, FontStyle.Bold, GOLD_COL,
                TextAnchor.MiddleCenter, new Vector2(0.64f, 0.05f), new Vector2(0.78f, 0.95f));
            MakeButton(r, "\u25ba", new Color(0.20f, 0.28f, 0.48f, 1f), WHITE, 20,
                new Vector2(0.78f, 0.1f), new Vector2(0.88f, 0.9f),
                () => { PlayerPrefs.SetFloat("musicVolume", Mathf.Min(1f, Mathf.Round((musicVol + 0.1f) * 10f) / 10f)); ApplySettings(); BuildSettings(); ShowPanel(settingsPanel); });
        }

        // ── HUD ────────────────────────────────────────────────────────────────
        Spacer();
        SectionHeader("HUD");

        bool showMinimap = PlayerPrefs.GetInt("showMinimap", 1) == 1;
        {
            Transform r = Row(TOGGLE_H);
            MakeImage(r, Vector2.zero, Vector2.one, new Color(0.12f, 0.15f, 0.22f, 0.8f));
            MakeText(r, "Show Minimap", 18, FontStyle.Normal, WHITE,
                TextAnchor.MiddleLeft, new Vector2(0.02f, 0.05f), new Vector2(0.66f, 0.95f));
            Color mmCol = showMinimap ? new Color(0.12f, 0.45f, 0.18f, 1f) : new Color(0.45f, 0.12f, 0.12f, 1f);
            MakeButton(r, showMinimap ? "ON" : "OFF", mmCol, WHITE, 18,
                new Vector2(0.68f, 0.1f), new Vector2(0.86f, 0.9f),
                () => {
                    int newVal = showMinimap ? 0 : 1;
                    PlayerPrefs.SetInt("showMinimap", newVal);
                    if (Time.timeScale > 0f) MinimapSystem.Instance?.SetVisible(newVal == 1);
                    BuildSettings(); ShowPanel(settingsPanel);
                });
        }

        {
            Transform r = Row(LAYOUT_H);
            MakeImage(r, Vector2.zero, Vector2.one, new Color(0.12f, 0.15f, 0.22f, 0.8f));
            MakeText(r, "HUD Layout", 18, FontStyle.Normal, WHITE,
                TextAnchor.MiddleLeft, new Vector2(0.02f, 0.1f), new Vector2(0.52f, 0.9f));
            MakeButton(r, "Horizontal \u2713", new Color(0.12f, 0.45f, 0.18f, 1f), WHITE, 15,
                new Vector2(0.54f, 0.12f), new Vector2(0.74f, 0.88f), null);
            MakeButton(r, "Vertical", new Color(0.18f, 0.18f, 0.22f, 0.7f),
                new Color(0.45f, 0.45f, 0.45f), 15,
                new Vector2(0.75f, 0.12f), new Vector2(0.94f, 0.88f), null);
        }

        {
            Transform r = Row(SOON_H);
            MakeText(r, "(Vertical layout coming soon)", 12, FontStyle.Italic,
                new Color(0.45f, 0.45f, 0.45f), TextAnchor.MiddleLeft,
                new Vector2(0.04f, 0.1f), new Vector2(0.96f, 0.9f));
        }

        // ── GAMEPLAY ──────────────────────────────────────────────────────────
        Spacer();
        SectionHeader("GAMEPLAY");

        bool showDmgNums  = PlayerPrefs.GetInt("showDamageNumbers", 1) == 1;
        bool showHpNormal = PlayerPrefs.GetInt("showHpNormal",      0) == 1;
        bool showHpMini   = PlayerPrefs.GetInt("showHpMiniBoss",    1) == 1;
        bool showHpBoss   = PlayerPrefs.GetInt("showHpBoss",        1) == 1;

        void TogRow(string label, bool current, string prefKey) {
            Transform r = Row(TOGGLE_H);
            MakeImage(r, Vector2.zero, Vector2.one, new Color(0.12f, 0.15f, 0.22f, 0.8f));
            MakeText(r, label, 16, FontStyle.Normal, WHITE,
                TextAnchor.MiddleLeft, new Vector2(0.02f, 0.05f), new Vector2(0.68f, 0.95f));
            Color btnCol = current ? new Color(0.12f, 0.45f, 0.18f, 1f) : new Color(0.45f, 0.12f, 0.12f, 1f);
            MakeButton(r, current ? "ON" : "OFF", btnCol, WHITE, 16,
                new Vector2(0.70f, 0.1f), new Vector2(0.86f, 0.9f),
                () => { PlayerPrefs.SetInt(prefKey, current ? 0 : 1); PlayerPrefs.Save(); BuildSettings(); ShowPanel(settingsPanel); });
        }

        TogRow("Damage Numbers",       showDmgNums,  "showDamageNumbers");
        TogRow("Enemy HP (Normal)",    showHpNormal, "showHpNormal");
        TogRow("Enemy HP (Mini-Boss)", showHpMini,   "showHpMiniBoss");
        TogRow("Enemy HP (Boss)",      showHpBoss,   "showHpBoss");

        cur += 20f; // bottom padding
        cntRT.offsetMin = new Vector2(0f, -cur);
        cntRT.offsetMax = Vector2.zero;
        sc.normalizedPosition = new Vector2(0, 1);
    }

    void SettingsStepperRow(Transform parent, string label, float yTop, float val,
        System.Action onDecrease, System.Action onIncrease) {
        float yBot = yTop - 0.09f;
        MakeImage(parent, new Vector2(0.05f, yBot), new Vector2(0.95f, yTop),
            new Color(0.12f, 0.15f, 0.22f, 0.8f));
        MakeText(parent, label, 17, FontStyle.Normal, WHITE,
            TextAnchor.MiddleLeft, new Vector2(0.07f, yBot), new Vector2(0.55f, yTop));
        MakeButton(parent, "\u25c4", new Color(0.20f, 0.28f, 0.48f, 1f), WHITE, 20,
            new Vector2(0.57f, yBot + 0.01f), new Vector2(0.67f, yTop - 0.01f), onDecrease);
        MakeText(parent, $"{Mathf.RoundToInt(val * 100)}%", 18, FontStyle.Bold, GOLD_COL,
            TextAnchor.MiddleCenter, new Vector2(0.67f, yBot), new Vector2(0.81f, yTop));
        MakeButton(parent, "\u25ba", new Color(0.20f, 0.28f, 0.48f, 1f), WHITE, 20,
            new Vector2(0.81f, yBot + 0.01f), new Vector2(0.91f, yTop - 0.01f), onIncrease);
    }

    // Single ON/OFF row — rowH = 0.06
    void SettingsToggleRow(Transform parent, string label, float yTop, bool current, string prefKey) {
        float yBot = yTop - 0.06f;
        MakeImage(parent, new Vector2(0.05f, yBot), new Vector2(0.95f, yTop),
            new Color(0.12f, 0.15f, 0.22f, 0.8f));
        MakeText(parent, label, 16, FontStyle.Normal, WHITE,
            TextAnchor.MiddleLeft, new Vector2(0.07f, yBot), new Vector2(0.72f, yTop));
        Color btnCol = current ? new Color(0.12f, 0.45f, 0.18f, 1f) : new Color(0.45f, 0.12f, 0.12f, 1f);
        MakeButton(parent, current ? "ON" : "OFF", btnCol, WHITE, 16,
            new Vector2(0.73f, yBot + 0.005f), new Vector2(0.88f, yTop - 0.005f),
            () => {
                PlayerPrefs.SetInt(prefKey, current ? 0 : 1);
                PlayerPrefs.Save();
                BuildSettings(); ShowPanel(settingsPanel);
            });
    }

    void ApplySettings() {
        float sfxVol   = PlayerPrefs.GetFloat("sfxVolume",   0.8f);
        float musicVol = PlayerPrefs.GetFloat("musicVolume", 0.5f);
        bool  showMini = PlayerPrefs.GetInt("showMinimap", 1) == 1;

        AudioListener.volume = sfxVol;
        foreach (var src in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None)) {
            string n = src.gameObject.name;
            if (n.IndexOf("music", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("bgm",   System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                src.gameObject.CompareTag("Music"))
                src.volume = musicVol;
        }
        if (Time.timeScale > 0f && MinimapSystem.Instance != null)
            MinimapSystem.Instance.SetVisible(showMini);
        PlayerPrefs.Save();
    }

    /// <summary>Called from GameHUD pause menu to open settings over the gameplay canvas.</summary>
    public void ShowSettings(System.Action onBack) {
        canvas.gameObject.SetActive(true);
        settingsBackAction = () => {
            settingsPanel?.SetActive(false);
            canvas.gameObject.SetActive(false);
            onBack?.Invoke();
        };
        menuPanel?.SetActive(false);
        shopPanel?.SetActive(false);
        scoresPanel?.SetActive(false);
        vipPanel?.SetActive(false);
        gameOverPanel?.SetActive(false);
        characterSelectPanel?.SetActive(false);
        BuildSettings();
        settingsPanel?.SetActive(true);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PANEL MANAGEMENT
    // ═════════════════════════════════════════════════════════════════════════
    void ShowPanel(GameObject target) {
        menuPanel?.SetActive(false);
        shopPanel?.SetActive(false);
        scoresPanel?.SetActive(false);
        vipPanel?.SetActive(false);
        gameOverPanel?.SetActive(false);
        characterSelectPanel?.SetActive(false);
        settingsPanel?.SetActive(false);
        target?.SetActive(true);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // UI HELPERS
    // ═════════════════════════════════════════════════════════════════════════
    GameObject MakeFullPanel(string name) {
        GameObject go = new GameObject(name);
        go.transform.SetParent(canvas.transform, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        Image bg = go.AddComponent<Image>();
        bg.color = BG_DARK;
        go.SetActive(false);
        return go;
    }

    void MakeImage(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color) {
        GameObject go = new GameObject("Row");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        go.AddComponent<Image>().color = color;
    }

    Text MakeText(Transform parent, string content, int size, FontStyle style, Color color,
                  TextAnchor align, Vector2 anchorMin, Vector2 anchorMax) {
        GameObject go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        Text txt = go.AddComponent<Text>();
        txt.text      = content;
        txt.font      = font;
        txt.fontSize  = size;
        txt.fontStyle = style;
        txt.color     = color;
        txt.alignment = align;
        return txt;
    }

    void MakeButton(Transform parent, string label, Color bgColor, Color textColor, int fontSize,
                    Vector2 anchorMin, Vector2 anchorMax, System.Action onClick) {
        GameObject go = new GameObject("Btn_" + label);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        Image img = go.AddComponent<Image>();
        img.color = bgColor;

        Button btn = go.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor      = bgColor;
        cb.highlightedColor = Color.Lerp(bgColor, Color.white, 0.25f);
        cb.pressedColor     = Color.Lerp(bgColor, Color.black, 0.25f);
        btn.colors = cb;

        if (onClick != null)
            btn.onClick.AddListener(() => onClick());
        else
            btn.interactable = false;

        // Label child
        GameObject textGO = new GameObject("Label");
        textGO.transform.SetParent(go.transform, false);
        RectTransform trt = textGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        Text txt = textGO.AddComponent<Text>();
        txt.text      = label;
        txt.font      = font;
        txt.fontSize  = fontSize;
        txt.fontStyle = FontStyle.Bold;
        txt.color     = textColor;
        txt.alignment = TextAnchor.MiddleCenter;
    }
}
