using System.Collections.Generic;
using UnityEngine;

// Placed on the ground after the arc animation completes.
// Damages the first enemy that steps on it, then destroys itself.
// Destroys itself when it leaves the player's screen.
public class CaltropLogic : MonoBehaviour {
    // Shared registry so WeaponSystem can count on-screen caltrops without FindObjectsOfType.
    public static readonly List<CaltropLogic> Active = new List<CaltropLogic>();

    private float _dmg;
    private bool _dead;

    public static void Spawn(Vector3 pos, float dmg, Sprite spr) {
        var go = new GameObject("Caltrop");
        go.transform.position = pos;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 3;
        if (spr != null) sr.sprite = spr;
        go.transform.localScale = Vector3.one * 3f;

        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;

        var logic = go.AddComponent<CaltropLogic>();
        logic._dmg = dmg;
        Active.Add(logic);
    }

    void OnDestroy() => Active.Remove(this);

    void Update() {
        if (!SurvivorMasterScript.IsOnScreen(transform.position))
            Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other) {
        if (_dead || !other.CompareTag("Enemy")) return;
        var e = other.GetComponent<EnemyEntity>();
        if (e == null || e.isDead) return;
        e.TakeDamage(_dmg);
        _dead = true;
        Destroy(gameObject);
    }
}
