using UnityEngine;

// Persistent shield for the Psammomancer's Sand Shield weapon.
// Parents itself to the player, fades in to 50% alpha, then fades out before
// despawning. While alive it sets WeaponSystem.SandShieldActive = true so that
// EnemyAttack and EnemyBullet know to block incoming damage and counterattack.
public class SandShieldLogic : MonoBehaviour {
    private float          _duration;
    private float          _elapsed;
    private SpriteRenderer _sr;

    private const float FadeTime = 0.2f;
    private const float MaxAlpha = 0.5f;
    private static readonly Color ShieldTint = new Color(0.95f, 0.78f, 0.25f, 0f);

    public static void Spawn(Transform player, float duration, float counterDmg, Sprite spr) {
        var go = new GameObject("SandShield");
        // Parent to player so the shield moves with them automatically.
        go.transform.SetParent(player);
        go.transform.localPosition = Vector3.zero;
        // Scale slightly larger than the player (1.3× local) to fully cover them.
        go.transform.localScale = Vector3.one * 1.3f;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.color        = ShieldTint; // starts fully transparent
        sr.sortingOrder = 6;          // just above the player sprite layer
        if (spr != null) sr.sprite = spr;

        WeaponSystem.SandShieldActive    = true;
        WeaponSystem.SandShieldCounterDmg = counterDmg;

        var logic      = go.AddComponent<SandShieldLogic>();
        logic._duration = duration;
        logic._sr       = sr;
    }

    void Update() {
        _elapsed += Time.deltaTime;

        // Alpha envelope: fade in → hold at MaxAlpha → fade out.
        float fadeOutStart = _duration - FadeTime;
        float alpha;
        if      (_elapsed < FadeTime)       alpha = (_elapsed / FadeTime) * MaxAlpha;
        else if (_elapsed > fadeOutStart)   alpha = Mathf.Max(0f, 1f - (_elapsed - fadeOutStart) / FadeTime) * MaxAlpha;
        else                                alpha = MaxAlpha;

        if (_sr != null)
            _sr.color = new Color(ShieldTint.r, ShieldTint.g, ShieldTint.b, alpha);

        if (_elapsed >= _duration) Destroy(gameObject);
    }

    void OnDestroy() {
        WeaponSystem.SandShieldActive = false;
    }
}
