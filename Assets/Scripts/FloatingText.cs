using System.Collections;
using UnityEngine;

/// <summary>
/// Spawns a floating grey damage number above an enemy when hit.
/// Rises at a randomised speed for 0.75 seconds then destroys itself.
/// Controlled by PlayerPrefs key "showDamageNumbers" (default: 1 = on).
/// </summary>
public class FloatingText : MonoBehaviour {

    public static void Spawn(Vector3 worldPos, float damage) {
        if (PlayerPrefs.GetInt("showDamageNumbers", 1) != 1) return;
        var go = new GameObject("FloatDmg");
        go.AddComponent<FloatingText>().Init(worldPos, damage);
    }

    void Init(Vector3 worldPos, float damage) {
        transform.position = worldPos + Vector3.up * 1.0f;

        var mesh           = gameObject.AddComponent<TextMesh>();
        mesh.text          = Mathf.RoundToInt(damage).ToString();
        mesh.fontSize      = 60;
        mesh.characterSize = 0.05f;   // world height ≈ fontSize × characterSize / 10 = 0.3 units
        mesh.fontStyle     = FontStyle.Bold;
        mesh.color         = new Color(0.78f, 0.78f, 0.78f, 1f);
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
}
