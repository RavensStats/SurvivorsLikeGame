using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour {
    public float moveSpeed = 12f;
    /// <summary>Scale applied to all input (1=normal, -1=reversed, 0.4=slowed).</summary>
    [HideInInspector] public float inputScale = 1f;
    /// <summary>The last non-zero movement direction (used by enemy AI to find "behind" the player).</summary>
    [HideInInspector] public Vector2 LastFacing = Vector2.down;

    private Rigidbody2D rb;
    private Vector2 moveInput;

    void Awake() {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        moveSpeed += PersistentUpgrades.SpeedBonus;
    }

    void Update() {
        if (Keyboard.current == null) return;
        moveInput = Vector2.zero;
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)    moveInput.y += 1;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)  moveInput.y -= 1;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)  moveInput.x -= 1;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) moveInput.x += 1;
        moveInput = moveInput.normalized;
        if (moveInput != Vector2.zero) LastFacing = moveInput;
    }

    void FixedUpdate() {
        float speed = moveSpeed * inputScale;
        if (SurvivorMasterScript.Instance != null && SurvivorMasterScript.Instance.poiHalfSpeed)
            speed *= 0.5f;
        rb.linearVelocity = moveInput * speed;
    }
}
