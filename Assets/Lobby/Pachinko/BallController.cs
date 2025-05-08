using UnityEngine;
using System.Collections.Generic;

public class BallController : MonoBehaviour
{
    public bool hasCollided = false;    // 是否碰撞
    public Transform hitTarget; // 碰撞目标
    private List<BallPredictionData.TransformInfo> trajectory; // 轨迹
    private float trajectoryTime; // 轨迹总时间
    private float currentTime = 0f; // 当前时间
    private bool isPlayingTrajectory = false; // 是否正在播放轨迹

    public void SetTargetPosition(List<BallPredictionData.TransformInfo> newTrajectory = null, float newTrajectoryTime = 0f)
    {
        trajectory = newTrajectory;
        trajectoryTime = newTrajectoryTime;
        isPlayingTrajectory = newTrajectory != null && newTrajectory.Count > 0;
        currentTime = 0f;
    }

    void Update()
    {
        if (isPlayingTrajectory && trajectory != null)
        {
            // 计算当前应该显示的位置
            currentTime += Time.deltaTime;
            if (currentTime >= trajectoryTime)
            {
                isPlayingTrajectory = false;
                return;
            }

            float normalizedTime = currentTime / trajectoryTime;
            float exactIndex = normalizedTime * (trajectory.Count - 1);
            int currentIndex = Mathf.FloorToInt(exactIndex);
            int nextIndex = Mathf.Min(currentIndex + 1, trajectory.Count - 1);
            float t = exactIndex - currentIndex;

            // 直接进行插值计算
            transform.position = Vector3.Lerp(
                trajectory[currentIndex].GetPosition(),
                trajectory[nextIndex].GetPosition(),
                t
            );

            transform.rotation = Quaternion.Lerp(
                trajectory[currentIndex].GetRotation(),
                trajectory[nextIndex].GetRotation(),
                t
            );
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
