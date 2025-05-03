using UnityEngine;

public class FlipperController : MonoBehaviour
{
    [Header("发射设置")]
    public float pullDownDistance = 1f; // Distance to pull down the platform
    public float thrustForce = 10f; // Force applied to the ball
    public float baseMinForce = 2f; // 基础最小力量
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
            // 计算弹簧位置相对于总行程的百分比
            float percentage = Vector3.Distance(transform.position, originalPosition) / pullDownDistance;

            // Apply upward thrust to nearby balls
            Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, 3.5f);
            foreach (var collider in colliders)
            {
                if (collider.CompareTag("Ball"))
                {
                    BallController ballController = collider.GetComponent<BallController>();
                    if (ballController != null && predictor != null)
                    {
                        predictor.OnSpringReleased(collider.gameObject, percentage);
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
