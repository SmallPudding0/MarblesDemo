using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.UI;

public class BallPredictionController1 : MonoBehaviour
{
    [Header("模拟参数")]
    public int maxAttempts = 10000;             // 最大尝试次数
    public float minThrustForce = 10f;         // 最小推力
    public float maxThrustForce = 15f;         // 最大推力
    public float ballSpawnOffset = 0.5f;      // 小球生成位置的Y轴偏移
    public int requiredSuccessCount = 100;     // 每个目标点需要的成功次数

    [Header("引用设置")]
    public GameObject ballPrefab;    // 添加对球预制体的引用
    public Transform[] targetPoints; // 多个可能的目标点
    public float acceptableDistance = 0.5f;   // 可接受的误差范围
    public Transform springTransform;  // 弹簧的位置
    public Transform spawnArea; // Area where balls will spawn

    [Header("调试设置")]
    public bool showDebugVisuals = true;           // 是否显示调试视觉效果
    public Color trajectoryColor = Color.yellow;    // 轨迹线颜色
    public float debugPointSize = 0.2f;            // 调试点大小

    [Header("UI设置")]
    public Text progressText;  // 进度显示文本
    public Slider progressSlider;  // 进度条

    [Header("目标点设置")]
    public Transform selectedTargetPoint; // 在Inspector中选择的目标点
    public bool useRandomTarget = false;  // 是否随机选择目标点

    // 存储每个目标点对应的发射参数和成功率
    private Dictionary<Transform, List<(Vector2 position, Vector2 force)>> launchParamsCache;
    private Dictionary<Transform, (int successCount, int totalAttempts)> successRates;
    private List<Vector2> currentTrajectory;        // 当前轨迹
    private List<Vector2> bestTrajectory;          // 最佳轨迹
    private Vector2 bestEndPoint;                  // 最佳终点位置
    private bool isComputing = false;  // 是否正在计算中

    void Start()
    {
        if (springTransform == null)
        {
            Debug.LogError("请设置弹簧位置！");
            return;
        }
        launchParamsCache = new Dictionary<Transform, List<(Vector2, Vector2)>>();
        successRates = new Dictionary<Transform, (int, int)>();
        StartCoroutine(PrecomputeAllTargets());
    }

    // 异步预计算所有目标点
    private IEnumerator PrecomputeAllTargets()
    {
        if (targetPoints == null || targetPoints.Length == 0) yield break;

        isComputing = true;
        int totalTargets = targetPoints.Length;
        int completedTargets = 0;

        // 初始化成功率统计
        foreach (var target in targetPoints)
        {
            successRates[target] = (0, 0);
        }

        foreach (var target in targetPoints)
        {
            yield return StartCoroutine(PrecomputeSingleTarget(target));
            completedTargets++;

            // 更新进度
            if (progressText != null)
                progressText.text = $"计算进度: {completedTargets}/{totalTargets}";
            if (progressSlider != null)
                progressSlider.value = (float)completedTargets / totalTargets;
        }

        // 打印每个目标点的成功率
        foreach (var kvp in successRates)
        {
            float successRate = (float)kvp.Value.successCount / kvp.Value.totalAttempts * 100;
            Debug.Log($"目标点 {kvp.Key.name} 成功率: {successRate:F2}% ({kvp.Value.successCount}/{kvp.Value.totalAttempts})");
        }

        isComputing = false;
        if (progressText != null)
            progressText.text = "计算完成";
    }

    // 预计算单个目标点
    private IEnumerator PrecomputeSingleTarget(Transform target)
    {
        int successCount = 0;
        int totalAttempts = 0;
        int currentTargetAttempts = 0;  // 当前目标点的尝试次数

        // 创建力量值列表，确保每个成功数据都有不同的力量值
        List<float> usedForces = new List<float>();
        float forceStep = (maxThrustForce - minThrustForce) / requiredSuccessCount; // 将力量区间分成requiredSuccessCount份

        while (successCount < requiredSuccessCount && totalAttempts < maxAttempts)
        {
            // 生成不重复的力量值
            float currentForce;
            do
            {
                currentForce = Random.Range(minThrustForce, maxThrustForce);
                // 如果尝试次数过多，使用固定步长
                if (currentTargetAttempts > maxAttempts / 2)
                {
                    currentForce = minThrustForce + (successCount * forceStep);
                }
            } while (usedForces.Contains(currentForce) && currentTargetAttempts <= maxAttempts / 2);

            Vector2 basePosition = springTransform.position + Vector3.up * ballSpawnOffset;
            Vector2 baseForce = Vector2.up * currentForce;

            var (finalPosition, hitTarget) = SimulateAndRecord(basePosition, baseForce);

            // 只记录真正击中当前目标点的数据
            if (hitTarget == target)
            {
                if (!launchParamsCache.ContainsKey(target))
                {
                    launchParamsCache[target] = new List<(Vector2, Vector2)>();
                }
                launchParamsCache[target].Add((basePosition, baseForce));
                usedForces.Add(currentForce);  // 记录已使用的力量值
                successCount++;
                Debug.Log($"目标点 {target.name} 成功次数: {successCount}/{requiredSuccessCount}, 当前力量: {currentForce}");
            }

            totalAttempts++;
            currentTargetAttempts++;

            // 每计算10次休息一帧，避免卡顿
            if (totalAttempts % 10 == 0)
                yield return null;

            // 如果当前目标点尝试次数过多但成功次数太少，可以跳过这个目标点
            if (currentTargetAttempts > maxAttempts / targetPoints.Length && successCount < requiredSuccessCount / 2)
            {
                Debug.LogWarning($"目标点 {target.name} 尝试次数过多但成功率太低，跳过此目标点");
                break;
            }
        }

        // 更新成功率统计
        successRates[target] = (successCount, currentTargetAttempts);
        Debug.Log($"目标点 {target.name} 计算完成: 成功 {successCount} 次, 总尝试 {currentTargetAttempts} 次");
    }

    // 模拟并记录结果
    private (Vector2, Transform) SimulateAndRecord(Vector2 basePosition, Vector2 baseForce)
    {
        currentTrajectory = new List<Vector2>();
        currentTrajectory.Add(basePosition);

        GameObject simulatedBall = Instantiate(ballPrefab, basePosition, Quaternion.identity, spawnArea);
        simulatedBall.SetActive(true);

        Rigidbody2D rb = simulatedBall.GetComponent<Rigidbody2D>();

        // 添加碰撞检测脚本
        var collisionDetector = simulatedBall.AddComponent<CollisionDetector>();
        collisionDetector.targetPoints = targetPoints;
        collisionDetector.acceptableDistance = acceptableDistance;
        rb.velocity = Vector2.zero;
        rb.AddForce(baseForce, ForceMode2D.Impulse);

        Vector2 finalPosition = Vector2.zero;
        float simulationTime = 0f;
        float maxSimulationTime = 5f;

        // 保存当前的物理模拟模式
        var previousSimulationMode = Physics2D.simulationMode;
        // 设置为Script模式
        Physics2D.simulationMode = SimulationMode2D.Script;

        while (simulationTime < maxSimulationTime)
        {
            Physics2D.Simulate(Time.fixedDeltaTime);
            simulationTime += Time.fixedDeltaTime;

            finalPosition = rb.position;
            currentTrajectory.Add(finalPosition);

            // 检查是否与任何目标点发生碰撞
            if (collisionDetector.hasCollided)
            {
                Debug.Log($"与目标点 {collisionDetector.hitTarget.name} 发生碰撞！");
                Debug.Log($"模拟落点: {finalPosition}, 目标点位置: {collisionDetector.hitTarget.position}");
                break;
            }

            // 如果小球停止运动也结束模拟
            if (rb.velocity.magnitude < 0.1f)
            {
                Debug.Log($"模拟结束 - 小球停止运动");
                Debug.Log($"最终落点: {finalPosition}");
                break;
            }
        }

        // 恢复原来的模拟模式
        Physics2D.simulationMode = previousSimulationMode;

        // 清理临时对象
        Destroy(simulatedBall);

        return (finalPosition, collisionDetector.hitTarget);
    }

    // 碰撞检测脚本
    private class CollisionDetector : MonoBehaviour
    {
        public Transform[] targetPoints;
        public float acceptableDistance;
        public bool hasCollided = false;
        public Transform hitTarget;

        private void OnCollisionEnter2D(Collision2D collision)
        {
            // 检查是否与任何目标点发生碰撞
            if (collision.gameObject.CompareTag("Edge"))
            {
                hasCollided = true;
                hitTarget = collision.transform;
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (!showDebugVisuals) return;

        // 绘制坐标网格
        DrawCoordinateGrid();

        // 绘制弹簧位置
        if (springTransform != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(springTransform.position, 0.3f);
            // 显示坐标
            UnityEditor.Handles.Label(springTransform.position + Vector3.up * ballSpawnOffset,
                $"小球初始位置: {springTransform.position + Vector3.up * ballSpawnOffset}");
        }

        // 绘制轨迹
        if (currentTrajectory != null && currentTrajectory.Count > 1)
        {
            Gizmos.color = trajectoryColor;
            for (int i = 0; i < currentTrajectory.Count - 1; i++)
            {
                Gizmos.DrawLine(currentTrajectory[i], currentTrajectory[i + 1]);
            }
        }

        // 绘制目标点
        if (targetPoints != null)
        {
            Gizmos.color = Color.blue;
            foreach (var target in targetPoints)
            {
                Gizmos.DrawWireSphere(target.position, debugPointSize);
                // 显示坐标和成功率
                if (successRates != null && successRates.ContainsKey(target))
                {
                    var (successCount, totalAttempts) = successRates[target];
                    float successRate = totalAttempts > 0 ? (float)successCount / totalAttempts * 100 : 0;
                    UnityEditor.Handles.Label(target.position + Vector3.up * 0.5f,
                        $"目标点: {target.position}\n" +
                        $"成功率: {successRate:F2}%");
                }
                // 绘制可接受范围
                Gizmos.color = new Color(0, 0, 1, 0.2f);
                Gizmos.DrawWireSphere(target.position, acceptableDistance);
            }
        }

        // 显示计算状态
        if (isComputing)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(springTransform.position, 0.5f);
        }
    }

    // 绘制坐标网格
    private void DrawCoordinateGrid()
    {
        // 设置网格颜色
        Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.2f);

        // 绘制X轴网格线
        for (float x = -10; x <= 10; x += 1)
        {
            Gizmos.DrawLine(new Vector3(x, -10, 0), new Vector3(x, 10, 0));
        }

        // 绘制Y轴网格线
        for (float y = -10; y <= 10; y += 1)
        {
            Gizmos.DrawLine(new Vector3(-10, y, 0), new Vector3(10, y, 0));
        }

        // 绘制坐标轴
        Gizmos.color = Color.red;
        Gizmos.DrawLine(Vector3.zero, Vector3.right * 10); // X轴
        Gizmos.color = Color.green;
        Gizmos.DrawLine(Vector3.zero, Vector3.up * 10);    // Y轴
    }

    public void ClearDebugVisuals()
    {
        currentTrajectory = null;
        bestTrajectory = null;
        bestEndPoint = Vector2.zero;
    }

    public (Vector2 position, Vector2 force) GetRandomLaunchParams()
    {
        // 获取目标点
        Transform targetPoint = useRandomTarget ?
            targetPoints[Random.Range(0, targetPoints.Length)] :
            selectedTargetPoint;

        if (targetPoint != null)
        {
            var (bestPos, bestForce) = GetRandomLaunchParams(targetPoint);
            Debug.Log($"发射到目标点: {targetPoint.name}, 力度: {bestForce}");
            return (bestPos, bestForce);
        }
        else
        {
            Debug.LogWarning("未选择目标点！");
            return (springTransform.position, Vector2.up * minThrustForce);
        }
    }

    // 获取指定目标点的随机发射参数
    public (Vector2 position, Vector2 force) GetRandomLaunchParams(Transform target)
    {
        if (launchParamsCache.ContainsKey(target) && launchParamsCache[target].Count > 0)
        {
            int randomIndex = Random.Range(0, launchParamsCache[target].Count);
            var launchParams = launchParamsCache[target][randomIndex];

            // 重新模拟一次以验证落点
            var (simulatedPosition, hitTarget) = SimulateAndRecord(launchParams.position, launchParams.force);
            Debug.Log($"从缓存中获取的发射参数:");
            Debug.Log($"- 初始位置: {launchParams.position}");
            Debug.Log($"- 发射力: {launchParams.force}");
            Debug.Log($"- 模拟落点: {simulatedPosition}");
            Debug.Log($"- 目标点位置: {target.position}");
            Debug.Log($"- 距离误差: {Vector2.Distance(simulatedPosition, target.position)}");

            return launchParams;
        }
        Debug.LogWarning($"未找到目标点 {target.name} 的有效发射参数");
        return (Vector2.zero, Vector2.zero);
    }

    // 获取所有目标点的成功率
    public Dictionary<Transform, float> GetSuccessRates()
    {
        Dictionary<Transform, float> rates = new Dictionary<Transform, float>();
        foreach (var kvp in successRates)
        {
            float successRate = (float)kvp.Value.successCount / kvp.Value.totalAttempts * 100;
            rates[kvp.Key] = successRate;
        }
        return rates;
    }
}
