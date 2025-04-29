using UnityEngine;

public class BallController : MonoBehaviour
{
    public bool hasCollided = false;    // 是否碰撞
    public Transform hitTarget; // 碰撞目标
    private Vector2? targetPosition; // 目标位置
    private Vector2? targetForce; // 目标力量
    private bool isMovingToPosition = false;
    private float moveSpeed = 5f; // 移动速度

    public void SetTargetPosition(Vector2 position, Vector2 force)
    {
        targetPosition = position;
        targetForce = force;
        isMovingToPosition = true;
    }

    private void FixedUpdate()
    {
        if (isMovingToPosition && targetPosition.HasValue)
        {
            // 计算目标位置（保持当前Y轴位置，只移动X轴）
            Vector2 currentTarget = new Vector2(
                targetPosition.Value.x,
                transform.position.y
            );

            // 平滑移动到目标位置
            transform.position = Vector2.MoveTowards(
                transform.position,
                currentTarget,
                moveSpeed * Time.fixedDeltaTime
            );

            // 如果到达目标位置
            if (Vector2.Distance(transform.position, currentTarget) < 0.01f)
            {
                transform.position = (Vector3)targetPosition;
                isMovingToPosition = false;
                
                // 初始化小球速度并施加目标力量
                var rb = GetComponent<Rigidbody2D>();
                if (rb != null && targetForce.HasValue)
                {
                    rb.velocity = Vector2.zero;
                    rb.AddForce(targetForce.Value, ForceMode2D.Impulse);
                }
                
                // 重置状态
                targetPosition = null;
                targetForce = null;
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Check if the collided object is another ball
        if (collision.gameObject.CompareTag("Ball"))
        {
            // Ignore the collision
            Physics2D.IgnoreCollision(GetComponent<Collider2D>(), collision.collider);
        }
        else if (collision.gameObject.CompareTag("Edge"))
        {
            hasCollided = true;
            hitTarget = collision.transform;
            Destroy(this.gameObject);
        }
    }
}
