using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ─── Per-run upgrade bonuses (reset on each new game, separate from PersistentUpgrades) ──
public static class RunUpgrades {
    public static float XPRateBonus       = 0f; // additive, e.g. 0.2 = +20 % XP this run
    public static float CooldownReduction = 0f; // fraction faster, e.g. 0.1 = 10 % less wait
    public static float DamageBonus       = 0f; // additive damage multiplier placeholder

    public static void Reset() {
        XPRateBonus       = 0f;
        CooldownReduction = 0f;
        DamageBonus       = 0f;
    }
}

// ─── Data class for one selectable upgrade card ───────────────────────────────
public class UpgradeOption {
    public string id;
    public string title;
    public string description;
    public Sprite icon;                    // optional sprite icon
    public string iconLabel = "*";         // fallback text if no sprite
    public Color  iconColor = Color.white;
    public System.Action onSelect;
}

// ─── Level-up overlay manager ─────────────────────────────────────────────────
/// <summary>
/// Procedural level-up overlay. Created automatically by SurvivorMasterScript.
/// Call Show(cardCount) to display N upgrade cards;  the chosen upgrade fires and
/// the panel closes itself, resuming time.
///
/// To provide custom card sets (e.g. per-character specials) call
///   LevelUpManager.Instance.Show(2, myCustomList);
/// </summary>
public class LevelUpManager : MonoBehaviour {

    public static LevelUpManager Instance;

    private Canvas    lvlCanvas;
    private GameObject panel;
    private Font      font;

    // ── Default upgrade pool — add entries freely; Show() picks N at random ──
    static readonly UpgradeOption[] UpgradePool = {
        new UpgradeOption {
            id          = "speed_1",
            title       = "Swift Boots",
            description = "+2 movement speed.\nGet to those gems faster.",
            iconLabel   = "SPD", iconColor = new Color(0.4f, 0.85f, 1f),
            onSelect    = () => { var pm = Object.FindFirstObjectByType<PlayerMovement>(); if (pm) pm.moveSpeed += 2f; }
        },
        new UpgradeOption {
            id          = "speed_2",
            title       = "Fleet Foot",
            description = "+4 movement speed.\nLeave enemies in the dust.",
            iconLabel   = "SPD", iconColor = new Color(0.2f, 0.7f, 1f),
            onSelect    = () => { var pm = Object.FindFirstObjectByType<PlayerMovement>(); if (pm) pm.moveSpeed += 4f; }
        },
        new UpgradeOption {
            id          = "max_hp",
            title       = "Iron Constitution",
            description = "+20 maximum HP.\nTank more hits.",
            iconLabel   = "HP+", iconColor = new Color(1f, 0.35f, 0.35f),
            onSelect    = () => SurvivorMasterScript.Instance?.BonusMaxHP(20f)
        },
        new UpgradeOption {
            id          = "xp_rate",
            title       = "Scholar's Mind",
            description = "+20% XP gain\nfor the rest of this run.",
            iconLabel   = "XP+", iconColor = new Color(0.4f, 1f, 0.5f),
            onSelect    = () => RunUpgrades.XPRateBonus += 0.20f
        },
        new UpgradeOption {
            id          = "cooldown",
            title       = "Quick Hands",
            description = "-10% weapon cooldown\nfor this run.",
            iconLabel   = "CD-", iconColor = new Color(1f, 1f, 0.3f),
            onSelect    = () => RunUpgrades.CooldownReduction += 0.10f
        },
        new UpgradeOption {
            id          = "gem_magnet",
            title       = "Magnetism",
            description = "Gem pickup radius\n+50% for this run.",
            iconLabel   = "MAG", iconColor = new Color(0.8f, 0.5f, 1f),
            onSelect    = () => XpGem.PickupRadiusMultiplier += 0.50f
        },
        new UpgradeOption {
            id          = "hp_regen",
            title       = "Regeneration",
            description = "Recover 2 HP\nevery 5 seconds.",
            iconLabel   = "REG", iconColor = new Color(0.2f, 1f, 0.4f),
            onSelect    = () => SurvivorMasterScript.Instance?.EnableRegen(2f, 5f)
        },
        new UpgradeOption {
            id          = "damage",
            title       = "Forged Weapons",
            description = "+25% weapon damage\nfor this run.",
            iconLabel   = "DMG", iconColor = new Color(1f, 0.5f, 0.1f),
            onSelect    = () => RunUpgrades.DamageBonus += 0.25f
        },
    };

    // ─────────────────────────────────────────────────────────────────────────
    void Awake() {
        Instance = this;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject cgo = new GameObject("LevelUpCanvas");
        DontDestroyOnLoad(cgo);
        lvlCanvas = cgo.AddComponent<Canvas>();
        lvlCanvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        lvlCanvas.sortingOrder = 50; // above HUD (1), below main menu (100)
        cgo.AddComponent<CanvasScaler>();
        cgo.AddComponent<GraphicRaycaster>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Show N randomly chosen upgrade cards.  Pauses the game.</summary>
    public void Show(int cardCount = 3) => Show(cardCount, null);

    /// <summary>Show a specific list of upgrade cards.  Pass null to use the random pool.</summary>
    public void Show(int cardCount, List<UpgradeOption> customOptions) {
        Time.timeScale = 0f;
        var options = customOptions ?? GetRandomOptions(cardCount);
        BuildPanel(options);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Panel
    // ─────────────────────────────────────────────────────────────────────────

    void BuildPanel(List<UpgradeOption> options) {
        if (panel != null) Destroy(panel);

        panel = new GameObject("LevelUpPanel");
        panel.transform.SetParent(lvlCanvas.transform, false);
        RectTransform prt = panel.AddComponent<RectTransform>();
        prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
        prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
        panel.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.80f);

        // ── Header ──
        MakeText(panel.transform, "LEVEL UP!", 52, FontStyle.Bold,
            new Color(1f, 0.85f, 0.2f, 1f), TextAnchor.MiddleCenter,
            new Vector2(0.1f, 0.83f), new Vector2(0.9f, 0.97f));
        MakeText(panel.transform, "Choose an upgrade", 22, FontStyle.Italic,
            Color.white, TextAnchor.MiddleCenter,
            new Vector2(0.1f, 0.74f), new Vector2(0.9f, 0.84f));

        // ── Cards — N equal horizontal partitions ──
        int   n       = options.Count;
        float margin  = 0.02f;
        float cardBot = 0.09f;
        float cardTop = 0.72f;

        for (int i = 0; i < n; i++) {
            int idx = i;
            BuildCard(panel.transform, options[i],
                new Vector2((float)i / n + margin,       cardBot),
                new Vector2((float)(i + 1) / n - margin, cardTop),
                () => { options[idx].onSelect?.Invoke(); Close(); });
        }
    }

    void BuildCard(Transform parent, UpgradeOption opt,
                   Vector2 ancMin, Vector2 ancMax, System.Action onClick) {
        GameObject card = new GameObject("Card_" + opt.id);
        card.transform.SetParent(parent, false);
        RectTransform crt = card.AddComponent<RectTransform>();
        crt.anchorMin = ancMin; crt.anchorMax = ancMax;
        crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;

        Image bg = card.AddComponent<Image>();
        bg.color = new Color(0.10f, 0.14f, 0.22f, 0.97f);

        Button btn = card.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor      = bg.color;
        cb.highlightedColor = new Color(0.18f, 0.28f, 0.48f, 1f);
        cb.pressedColor     = new Color(0.06f, 0.09f, 0.15f, 1f);
        btn.colors = cb;
        btn.onClick.AddListener(() => onClick());

        // Icon area (top 38%)
        if (opt.icon != null) {
            GameObject iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(card.transform, false);
            RectTransform irt = iconGO.AddComponent<RectTransform>();
            irt.anchorMin = new Vector2(0.15f, 0.60f); irt.anchorMax = new Vector2(0.85f, 0.95f);
            irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
            iconGO.AddComponent<Image>().sprite = opt.icon;
        } else {
            Text lbl = MakeText(card.transform, opt.iconLabel, 36, FontStyle.Bold, opt.iconColor,
                TextAnchor.MiddleCenter, new Vector2(0.05f, 0.60f), new Vector2(0.95f, 0.95f));
            lbl.resizeTextForBestFit = true;
            lbl.resizeTextMinSize    = 12;
            lbl.resizeTextMaxSize    = 48;
        }

        // Title
        MakeText(card.transform, opt.title, 17, FontStyle.Bold, Color.white,
            TextAnchor.MiddleCenter, new Vector2(0.04f, 0.43f), new Vector2(0.96f, 0.61f));

        // Description (wrapping)
        Text desc = MakeText(card.transform, opt.description, 13, FontStyle.Normal,
            new Color(0.75f, 0.88f, 1f, 1f), TextAnchor.UpperCenter,
            new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.44f));
        desc.horizontalOverflow = HorizontalWrapMode.Wrap;
        desc.verticalOverflow   = VerticalWrapMode.Overflow;
    }

    void Close() {
        if (panel != null) Destroy(panel);
        Time.timeScale = 1f;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    List<UpgradeOption> GetRandomOptions(int count) {
        var pool = new List<UpgradeOption>(UpgradePool);

        // Also offer weapon/passive cards from WeaponSystem.cardPool that
        // the player does not already own.
        var ws = WeaponSystem.Instance;
        if (ws != null) {
            foreach (var item in ws.cardPool) {
                bool alreadyOwned = ws.activeWeapons.Exists(w => w.itemName == item.itemName)
                                 || ws.passiveItems.Exists(p => p.itemName == item.itemName);
                if (alreadyOwned) continue;

                var captured = item; // capture for lambda
                string label = item.isWeapon ? "WPN" : "ITM";
                Color col    = item.rarity == Rarity.Legendary ? new Color(1f, 0.8f, 0f)
                             : item.rarity == Rarity.Epic      ? new Color(0.7f, 0.3f, 1f)
                             : item.rarity == Rarity.Rare      ? new Color(0.3f, 0.7f, 1f)
                             : Color.white;
                pool.Add(new UpgradeOption {
                    id          = "item_" + item.itemName,
                    title       = item.itemName,
                    description = item.description,
                    iconLabel   = label,
                    iconColor   = col,
                    onSelect    = () => {
                        if (captured.isWeapon) ws.activeWeapons.Add(captured);
                        else                   ws.passiveItems.Add(captured);
                        ws.CheckSynergies();
                    }
                });
            }
        }

        var chosen = new List<UpgradeOption>();
        count = Mathf.Min(count, pool.Count);
        while (chosen.Count < count && pool.Count > 0) {
            int i = Random.Range(0, pool.Count);
            chosen.Add(pool[i]);
            pool.RemoveAt(i);
        }
        return chosen;
    }

    Text MakeText(Transform parent, string content, int size, FontStyle style,
                  Color color, TextAnchor align, Vector2 anchorMin, Vector2 anchorMax) {
        GameObject go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        Text txt = go.AddComponent<Text>();
        txt.text = content; txt.font = font;
        txt.fontSize = size; txt.fontStyle = style;
        txt.color = color; txt.alignment = align;
        return txt;
    }
}
