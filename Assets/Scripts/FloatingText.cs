using System.Collections;
using UnityEngine;

/// <summary>
/// Spawns floating combat text (damage/heal) in world-space.
/// Rises at a randomised speed for 0.75 seconds then destroys itself.
/// Controlled by PlayerPrefs keys:
///   showDamageNumbers        (enemy damage)
///   showPlayerDamageNumbers  (player damage)
///   showHealingNumbers       (healing on player + enemies)
/// </summary>
public class FloatingText : MonoBehaviour {

    // Cached once at startup (or when settings change) to avoid a PlayerPrefs
    // lookup on every single damage event.
    public static bool ShowDamageNumbers = true;
    public static bool ShowPlayerDamageNumbers = true;
    public static bool ShowHealingNumbers = true;

    public static void RefreshSettings() {
        ShowDamageNumbers = PlayerPrefs.GetInt("showDamageNumbers", 1) == 1;
        ShowPlayerDamageNumbers = PlayerPrefs.GetInt("showPlayerDamageNumbers", 1) == 1;
        ShowHealingNumbers = PlayerPrefs.GetInt("showHealingNumbers", 1) == 1;
    }

    public static void SpawnEnemyDamage(Vector3 worldPos, float damage) {
        if (damage <= 0f) return;
        if (!ShowDamageNumbers) return;
        SpawnText(worldPos, Mathf.RoundToInt(damage).ToString(), new Color(0.78f, 0.78f, 0.78f, 1f));
    }

    public static void SpawnPlayerDamage(Vector3 worldPos, float damage) {
        if (damage <= 0f) return;
        if (!ShowPlayerDamageNumbers) return;
        SpawnText(worldPos, Mathf.RoundToInt(damage).ToString(), new Color(1f, 0.35f, 0.3f, 1f));
    }

    public static void SpawnHeal(Vector3 worldPos, float amount) {
        if (amount <= 0f) return;
        if (!ShowHealingNumbers) return;
        SpawnText(worldPos, $"+{Mathf.RoundToInt(amount)}", new Color(0.35f, 1f, 0.45f, 1f));
    }

    static void SpawnText(Vector3 worldPos, string text, Color color) {
        var go = new GameObject("FloatText");
        go.AddComponent<FloatingText>().Init(worldPos, text, color);
    }

    void Init(Vector3 worldPos, string text, Color color) {
        transform.position = worldPos + Vector3.up * 1.0f;

        var mesh           = gameObject.AddComponent<TextMesh>();
        mesh.text          = text;
        mesh.fontSize      = 60;
        mesh.characterSize = 0.14f;   // world height ≈ fontSize × characterSize / 10 = 0.84 units
        mesh.fontStyle     = FontStyle.Bold;
        mesh.color         = color;
        mesh.anchor        = TextAnchor.MiddleCenter;
        mesh.alignment     = TextAlignment.Center;

        // Render above enemies (sortingOrder 5) and orbs (6-9)
        GetComponent<MeshRenderer>().sortingOrder = 15;

        _riseSpeed = Random.Range(1.5f, 3.5f);
        StartCoroutine(Float());
    }

    float _riseSpeed;

    IEnumerator Float() {
        const float Duration = 0.75f;
        float elapsed = 0f;
        while (elapsed < Duration) {
            elapsed += Time.deltaTime;
            transform.position += Vector3.up * _riseSpeed * Time.deltaTime;
            yield return null;
        }
        Destroy(gameObject);
    }

    // Spawns a large centered banner above the player for 2.5 seconds, fading out over the last second.
    public static void SpawnBanner(string text, Color color) {
        var go = new GameObject("EffectBanner");
        go.AddComponent<FloatingText>().InitBanner(text, color);
    }

    void InitBanner(string text, Color color) {
        var sms = SurvivorMasterScript.Instance;
        transform.position = sms?.player != null
            ? sms.player.position + Vector3.up * 7f
            : Vector3.up * 7f;

        var mesh           = gameObject.AddComponent<TextMesh>();
        mesh.text          = text;
        mesh.fontSize      = 90;
        mesh.characterSize = 0.13f;
        mesh.fontStyle     = FontStyle.Bold;
        mesh.color         = color;
        mesh.anchor        = TextAnchor.MiddleCenter;
        mesh.alignment     = TextAlignment.Center;
        GetComponent<MeshRenderer>().sortingOrder = 20;
        StartCoroutine(BannerFade(mesh, color));
    }

    IEnumerator BannerFade(TextMesh mesh, Color baseColor) {
        const float Hold    = 1.5f;
        const float FadeOut = 1.0f;
        yield return new WaitForSeconds(Hold);
        float elapsed = 0f;
        while (elapsed < FadeOut) {
            if (mesh != null)
                mesh.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f - elapsed / FadeOut);
            elapsed += Time.deltaTime;
            yield return null;
        }
        Destroy(gameObject);
    }
}
