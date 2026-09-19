using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class PlayerController : MonoBehaviour
{
    private Rigidbody rb; 
    
    private int count;
    
    private float movementX;
    private float movementY;
    
    public float speed = 0; 
    
    public TextMeshProUGUI countText;
    
    public GameObject winTextObject;

    // --- Audio ---
    [Header("Audio")]
    [Tooltip("Minimum speed magnitude to trigger rolling sound")]
    public float rollSpeedThreshold = 0.5f;

    [Tooltip("Max speed used to normalise roll pitch/volume")]
    public float maxSpeed = 20f;

    [Tooltip("Minimum collision impulse to trigger collision sound")]
    public float collisionImpulseThreshold = 1.5f;

    private bool isMoving = false;
    
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        
        count = 0;
        
        SetCountText();
        
        winTextObject.SetActive(false);
    }
    
    void OnMove(InputValue movementValue)
    {
        Vector2 movementVector = movementValue.Get<Vector2>();
        
        movementX = movementVector.x; 
        movementY = movementVector.y; 
    }
    
    void FixedUpdate() 
    {
        Vector3 movement = new Vector3 (movementX, 0.0f, movementY);
        
        rb.AddForce(movement * speed);

        // Rolling sound: start/stop based on velocity magnitude
        float currentSpeed = rb.linearVelocity.magnitude;
        bool moving = currentSpeed > rollSpeedThreshold;

        if (moving && !isMoving)
        {
            AudioManager.Instance?.StartRolling();
            isMoving = true;
        }
        else if (!moving && isMoving)
        {
            AudioManager.Instance?.StopRolling();
            isMoving = false;
        }

        if (moving)
        {
            float normalized = Mathf.Clamp01(currentSpeed / maxSpeed);
            AudioManager.Instance?.SetRollSpeed(normalized);
        }
    }
    
    void OnTriggerEnter(Collider other) 
    {
        if (other.gameObject.CompareTag("Pickup")) 
        {
            other.gameObject.SetActive(false);
            
            count = count + 1;

            // Play pickup sound
            AudioManager.Instance?.PlayPickup();

            SetCountText();
        }
    }
    
    void SetCountText() 
    {
        countText.text = "Count: " + count.ToString();
        
        if (count >= 12)
        {
            winTextObject.SetActive(true);

            // Play win sound and stop rolling
            AudioManager.Instance?.StopRolling();
            AudioManager.Instance?.PlayWin();
            
            Destroy(GameObject.FindGameObjectWithTag("Enemy"));
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        // Only play collision sound if the impact is strong enough
        float impulse = collision.impulse.magnitude;

        if (collision.gameObject.CompareTag("Enemy"))
        {
            AudioManager.Instance?.StopRolling();
            AudioManager.Instance?.PlayEnemyHit();

            Destroy(gameObject); 

            winTextObject.gameObject.SetActive(true);
            winTextObject.GetComponent<TextMeshProUGUI>().text = "You Lose!";
        }
        else if (impulse > collisionImpulseThreshold)
        {
            // Hit wall or dynamic box
            AudioManager.Instance?.PlayCollision();
        }
    }
}
