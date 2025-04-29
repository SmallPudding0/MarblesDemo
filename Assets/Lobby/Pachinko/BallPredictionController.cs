using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.UI;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class BallPredictionController : MonoBehaviour
{
    [Header("模拟参数")]
    public int maxAttempts = 1000;             // 最大尝试次数
    public float minThrustForce = 9f;         // 最小推力
    public float maxThrustForce = 20f;         // 最大推力
    public float ballSpawnOffset = 0.5f;      // 小球生成位置的Y轴偏移
    public int requiredSuccessCount = 100;     // 每个目标点需要的成功次数
    public Vector3 leftBallPosition; // 最左侧小球位置
    public Vector3 rightBallPosition; // 最右侧小球位置

    [Header("引用设置")]
    public GameObject ballPrefab;    // 添加对球预制体的引用
    public Transform[] targetPoints; // 多个可能的目标点
    public float acceptableDistance = 0.5f;   // 可接受的误差范围
    public Transform springTransform;  // 弹簧的位置
    public Transform spawnArea; // Area where balls will spawn
    public BallPredictionData predictionData; // 数据持久化引用

    [Header("调试设置")]
    public bool showDebugVisuals = true;           // 是否显示调试视觉效果
    public Color trajectoryColor = Color.yellow;    // 轨迹线颜色
    public float debugPointSize = 0.2f;            // 调试点大小

    [Header("目标点设置")]
    public Transform selectedTargetPoint; // 在Inspector中选择的目标点
    public bool useRandomTarget = false;  // 是否随机选择目标点

    // 存储每个目标点对应的发射参数和成功率
    private Dictionary<Transform, List<(Vector2 position, Vector2 force)>> launchParamsCache;
    private Dictionary<Transform, (int successCount, int totalAttempts)> successRates;
    private Dictionary<Transform, int> targetAttempts; // 记录每个目标点作为目标时的尝试次数
    private List<Vector2> currentTrajectory;        // 当前轨迹
    private bool isComputing = false;  // 是否正在计算中

    // 在检视窗口中显示的进度信息
    [Header("进度信息")]
    [SerializeField]
    private string computationStatus = "未开始计算";
    [SerializeField]
    private Dictionary<Transform, string> targetStatus = new Dictionary<Transform, string>();

    // 存储成功的力量值区间
    private Dictionary<Transform, List<(float minForce, float maxForce)>> targetForceRanges = new Dictionary<Transform, List<(float, float)>>();
    private int totalSimulationAttempts = 0;

    void Start()
    {
        if (springTransform == null)
        {
            Debug.LogError("请设置弹簧位置！");
            return;
        }

        // 初始化数据结构
        launchParamsCache = new Dictionary<Transform, List<(Vector2, Vector2)>>();
        successRates = new Dictionary<Transform, (int, int)>();
        targetForceRanges = new Dictionary<Transform, List<(float, float)>>();
        targetAttempts = new Dictionary<Transform, int>();

        // 检查是否有有效的持久化数据
        if (predictionData != null && predictionData.isDataValid)
        {
            Debug.Log("正在加载持久化数据...");
            predictionData.LoadData(out launchParamsCache, out successRates, out targetForceRanges);
            totalSimulationAttempts = predictionData.totalSimulationAttempts;

            // 初始化目标点状态
            foreach (var target in targetPoints)
            {
                if (successRates.ContainsKey(target))
                {
                    var (successCount, totalAttempts) = successRates[target];
                    targetStatus[target] = $"成功: {successCount}/{requiredSuccessCount}";
                    targetAttempts[target] = totalAttempts;
                }
                else
                {
                    targetStatus[target] = "等待计算";
                    targetAttempts[target] = 0;
                }
            }

            Debug.Log("持久化数据加载完成");
        }
        else
        {
            Debug.Log("没有有效的持久化数据，开始新的模拟...");
            StartCoroutine(PrecomputeAllTargets());
        }
    }

    // 异步预计算所有目标点
    private IEnumerator PrecomputeAllTargets()
    {
        if (targetPoints == null || targetPoints.Length == 0)
        {
            Debug.LogError("没有设置目标点！");
            yield break;
        }

        Debug.Log($"开始预计算所有目标点，共 {targetPoints.Length} 个目标点");
        isComputing = true;
        targetForceRanges.Clear();
        totalSimulationAttempts = 0;
        targetAttempts = new Dictionary<Transform, int>();


        // 初始化成功率统计和力量值区间
        foreach (var target in targetPoints)
        {
            successRates[target] = (0, 0);
            targetStatus[target] = "等待计算";
            targetForceRanges[target] = new List<(float, float)>();
            targetAttempts[target] = 0;
            Debug.Log($"初始化目标点 {target.name}");
        }

        while (totalSimulationAttempts < maxAttempts)
        {
            // 检查是否所有目标点都已完成
            bool allTargetsCompleted = true;
            foreach (var target in targetPoints)
            {
                var (successCount, _) = successRates[target];
                if (successCount < requiredSuccessCount)
                {
                    allTargetsCompleted = false;
                    break;
                }
            }

            if (allTargetsCompleted)
            {
                Debug.Log("所有目标点都已完成！");
                break;
            }

            // 选择未完成的目标点
            Transform targetToTry = null;
            foreach (var target in targetPoints)
            {
                var (successCount, _) = successRates[target];
                // 如果尝试次数超过最大尝试次数，跳过这个目标点
                if (targetAttempts[target] > maxAttempts / targetPoints.Length)
                {
                    Debug.LogWarning($"目标点 {target.name} 尝试次数过多 ({targetAttempts[target]}次)，跳过此目标点");
                    continue;
                }

                if (successCount < requiredSuccessCount)
                {
                    Debug.Log($"当前目标点 {target.name} 成功 ({successCount}次)");
                    targetToTry = target;
                    break;
                }
                else
                {
                    continue;
                }
            }

            // 如果没有可尝试的目标点，结束计算
            if (targetToTry == null)
            {
                Debug.Log("所有目标点都已完成或跳过！");
                break;
            }

            // 增加当前目标点的尝试次数并更新successRates
            targetAttempts[targetToTry]++;
            var (currentSuccessCount, _) = successRates[targetToTry];
            successRates[targetToTry] = (currentSuccessCount, targetAttempts[targetToTry]);

            // 生成力量值
            float currentForce = GenerateForceValue(targetToTry);
            Vector2 basePosition = springTransform.position + Vector3.up * ballSpawnOffset;
            Vector2 baseForce = Vector2.up * currentForce;

            // 在左右位置之间随机选择一个位置
            basePosition.x = Random.Range(leftBallPosition.x, rightBallPosition.x);

            var (finalPosition, hitTarget) = SimulateAndRecord(basePosition, baseForce);
            totalSimulationAttempts++;

            // 记录所有成功的数据
            if (hitTarget != null)
            {
                // 更新被击中目标点的统计
                var (hitSuccessCount, hitTotalAttempts) = successRates[hitTarget];
                successRates[hitTarget] = (hitSuccessCount + 1, targetAttempts[hitTarget]);

                // 记录成功数据
                if (!launchParamsCache.ContainsKey(hitTarget))
                {
                    launchParamsCache[hitTarget] = new List<(Vector2, Vector2)>();
                }
                launchParamsCache[hitTarget].Add((basePosition, baseForce));

                // 更新成功的力量值区间
                UpdateSuccessfulForceRanges(hitTarget, currentForce);

                // 更新目标点状态
                targetStatus[hitTarget] = $"成功: {hitSuccessCount + 1}/{requiredSuccessCount}";
                Debug.Log($"目标点 {hitTarget.name} 成功次数: {hitSuccessCount + 1}/{requiredSuccessCount}, 当前力量: {currentForce}");
            }

            // 每计算100次输出一次进度
            if (totalSimulationAttempts % 100 == 0)
            {
                float progress = (float)System.Array.IndexOf(targetPoints, targetToTry) / targetPoints.Length * 100f;
                computationStatus = $"正在计算目标点: {targetToTry?.name ?? "无"}，进度: {progress:F1}%";
                Debug.Log($"当前进度: 总尝试 {totalSimulationAttempts} 次");
                foreach (var target in targetPoints)
                {
                    var (successCount, _) = successRates[target];
                    Debug.Log($"目标点 {target.name}: {successCount}/{requiredSuccessCount}, 尝试次数: {targetAttempts[target]}");
                }
            }

            // 每计算10次休息一帧，避免卡顿
            if (totalSimulationAttempts % 10 == 0)
                yield return null;
        }

        // 打印每个目标点的成功力量区间
        Debug.Log("\n各目标点的成功力量区间：");
        foreach (var target in targetPoints)
        {
            if (targetForceRanges.ContainsKey(target) && targetForceRanges[target].Count > 0)
            {
                Debug.Log($"\n目标点 {target.name} 的成功力量区间：");
                var ranges = targetForceRanges[target];
                for (int i = 0; i < ranges.Count; i++)
                {
                    var range = ranges[i];
                    Debug.Log($"区间 {i + 1}: {range.minForce:F2} - {range.maxForce:F2}");
                }
            }
            else
            {
                Debug.Log($"\n目标点 {target.name} 没有成功的力量区间");
            }
        }

        // 更新所有目标点的状态
        Debug.Log("计算完成，统计各目标点成功率：");
        foreach (var kvp in successRates)
        {
            float successRate = (float)kvp.Value.successCount / targetAttempts[kvp.Key] * 100;
            targetStatus[kvp.Key] = $"成功率: {successRate:F2}% ({kvp.Value.successCount}/{targetAttempts[kvp.Key]})";
            Debug.Log($"目标点 {kvp.Key.name}: 成功率 {successRate:F2}%, 成功 {kvp.Value.successCount} 次, 总尝试 {targetAttempts[kvp.Key]} 次");
        }

        // 在计算完成后保存数据
        SavePredictionData();

        isComputing = false;
        computationStatus = "计算完成 (100%)";
        Debug.Log("所有目标点计算完成！");
    }

    // 生成力量值
    private float GenerateForceValue(Transform target)
    {
        // 使用全局范围
        return Random.Range(minThrustForce, maxThrustForce);
    }

    // 更新成功的力量值区间
    private void UpdateSuccessfulForceRanges(Transform target, float successfulForce)
    {
        if (!targetForceRanges.ContainsKey(target))
        {
            targetForceRanges[target] = new List<(float, float)>();
        }

        var ranges = targetForceRanges[target];

        // 检查是否可以与现有区间合并
        for (int i = 0; i < ranges.Count; i++)
        {
            var range = ranges[i];
            if (successfulForce >= range.minForce - 0.5f && successfulForce <= range.maxForce + 0.5f)
            {
                // 扩展区间
                ranges[i] = (
                    Mathf.Min(range.minForce, successfulForce),
                    Mathf.Max(range.maxForce, successfulForce)
                );
                return;
            }
        }

        // 如果没有可合并的区间，添加新的区间
        ranges.Add((successfulForce - 0.5f, successfulForce + 0.5f));
    }

    // 模拟并记录结果
    private (Vector2, Transform) SimulateAndRecord(Vector2 basePosition, Vector2 baseForce)
    {
        currentTrajectory = new List<Vector2>();
        currentTrajectory.Add(basePosition);

        GameObject simulatedBall = Instantiate(ballPrefab, basePosition, Quaternion.identity, spawnArea);
        simulatedBall.SetActive(true);

        Rigidbody2D rb = simulatedBall.GetComponent<Rigidbody2D>();
        BallController ballContrl = simulatedBall.AddComponent<BallController>();

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
            if (ballContrl.hasCollided)
            {
                Debug.Log($"与目标点 {ballContrl.hitTarget.name} 发生碰撞！");
                Debug.Log($"模拟落点: {finalPosition}, 目标点位置: {ballContrl.hitTarget.position}");
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

        return (finalPosition, ballContrl.hitTarget);
    }

    // 当弹簧释放时调用此方法
    public void OnSpringReleased(GameObject ball)
    {
        // 获取目标点
        Transform targetPoint = useRandomTarget ?
            targetPoints[Random.Range(0, targetPoints.Length)] :
            selectedTargetPoint;

        if (targetPoint != null)
        {
            // 获取该目标点的所有成功发射参数
            if (launchParamsCache.ContainsKey(targetPoint) && launchParamsCache[targetPoint].Count > 0)
            {
                // 找到最接近当前小球X轴位置的成功位置
                var currentX = ball.transform.position.x;
                var closestParams = launchParamsCache[targetPoint]
                    .OrderBy(p => Mathf.Abs(p.position.x - currentX))
                    .FirstOrDefault();

                if (closestParams != default)
                {
                    var ballController = ball.GetComponent<BallController>();
                    if (ballController != null)
                    {
                        ballController.SetTargetPosition(closestParams.position, closestParams.force);
                    }
                }
            }
        }
    }
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebugVisuals) return;

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
#endif

    private void SavePredictionData()
    {
        if (predictionData != null)
        {
            predictionData.SaveData(launchParamsCache, successRates, targetForceRanges, totalSimulationAttempts);

            // 只在编辑器中执行保存操作
#if UNITY_EDITOR

            EditorUtility.SetDirty(predictionData);
            AssetDatabase.SaveAssets();
            Debug.Log("数据已保存到磁盘");

#endif

            Debug.Log("数据已更新到 ScriptableObject");
        }
    }
}
