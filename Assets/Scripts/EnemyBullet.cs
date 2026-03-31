using UnityEngine;

public class EnemyBullet : MonoBehaviour {
    public float damage;
    void OnTriggerEnter2D(Collider2D other) {
        if (other.CompareTag("Player")) {
            SurvivorMasterScript.Instance.playerHP -= damage;
            Destroy(gameObject);
        }
    }
}