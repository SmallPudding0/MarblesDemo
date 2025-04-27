using UnityEngine;

public class BallController : MonoBehaviour
{
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Check if the collided object is another ball
        if (collision.gameObject.CompareTag("Ball"))
        {
            // Ignore the collision
            Physics2D.IgnoreCollision(GetComponent<Collider2D>(), collision.collider);
        }
        else if (collision.gameObject.CompareTag("Edge")){
            Destroy(this.gameObject);
        }
    }
}
