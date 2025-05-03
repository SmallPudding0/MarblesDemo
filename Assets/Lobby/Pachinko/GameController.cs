using UnityEngine;

public class GameController : MonoBehaviour
{
    [Header("游戏速度控制")]
    [Range(0.1f, 1f)]
    public float gameSpeed = 1f;
    [Tooltip("是否启用游戏速度控制")]
    public bool enableSpeedControl = true;

    [Header("帧率控制")]
    [Range(10, 300)]
    public int targetFrameRate = 60;
    [Tooltip("是否限制帧率")]
    public bool limitFrameRate = true;

    private void Start()
    {
        // 设置初始帧率
        if (limitFrameRate)
        {
            Application.targetFrameRate = targetFrameRate;
        }
        else
        {
            Application.targetFrameRate = -1; // 不限制帧率
        }

        // 初始化游戏速度
        Time.timeScale = gameSpeed;
    }

    private void Update()
    {
        // 更新游戏速度
        if (enableSpeedControl)
        {
            Time.timeScale = gameSpeed;
        }
        if (limitFrameRate)
        {
            Application.targetFrameRate = targetFrameRate;
        }
        else
        {
            Application.targetFrameRate = -1; // 不限制帧率
        }
    }
} 