using UnityEngine;

public class FlipperController : MonoBehaviour
{
    [Header("发射设置")]
    public float pullDownDistance = 1f; // Distance to pull down the platform
    public float thrustForce = 10f; // Force applied to the ball
    public float minForce = 2f;
    public float downSpeed = 5f; // Speed of the platform moving down
    public float upSpeed = 15f; // Speed of the platform moving up

    private Vector3 originalPosition;
    private Vector3 targetPosition;
    private bool isPulledDown = false;

    private Collider2D flipperCollider;
    private bool autoTrigger;
    private BallPredictionController predictor;

    void Start()
    {
        originalPosition = transform.position; // Store the original position
        targetPosition = originalPosition; // Initialize target position
        flipperCollider = GetComponent<Collider2D>(); // Get the flipper's collider
        predictor = FindObjectOfType<BallPredictionController>();
    }

    void OnMouseDown()
    {
        if (!isPulledDown && !autoTrigger)
        {
            targetPosition = originalPosition + Vector3.down * pullDownDistance;
            isPulledDown = true;
        }
    }

    void OnMouseUp()
    {
        if (isPulledDown && !autoTrigger)
        {
            // Apply upward thrust to nearby balls
            Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, 2.5f);
            foreach (var collider in colliders)
            {
                if (collider.CompareTag("Ball"))
                {
                    Rigidbody2D rb = collider.GetComponent<Rigidbody2D>();
                    if (rb != null && predictor != null)
                    {
                        predictor.OnSpringReleased(collider.gameObject);
                    }
                }
            }

            // Reset position
            targetPosition = originalPosition;
            isPulledDown = false;
        }
    }

    void FixedUpdate()
    {
        // Move the platform towards the target position
        float speed = isPulledDown ? downSpeed : upSpeed;
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.fixedDeltaTime);
    }

    public void OnToggleChanged(bool isOn)
    {
        // Update autoTrigger whenever the toggle state changes
        autoTrigger = isOn;
    }
}
