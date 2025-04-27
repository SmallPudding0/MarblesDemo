using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.UI;
using System.Linq;

public class BallPredictionController : MonoBehaviour
{
    [Header("模拟参数")]
    public int maxAttempts = 1000;             // 最大尝试次数
    public float minThrustForce = 9f;         // 最小推力
    public float maxThrustForce = 20f;         // 最大推力
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
        launchParamsCache = new Dictionary<Transform, List<(Vector2, Vector2)>>();
        successRates = new Dictionary<Transform, (int, int)>();
        StartCoroutine(PrecomputeAllTargets());
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

        

        isComputing = false;
        computationStatus = "计算完成";
        Debug.Log("所有目标点计算完成！");
    }

    // 生成力量值
    private float GenerateForceValue(Transform target)
    {
        // 这里想优化一下，按区间生成力量值，但是会导致最后小球轨迹计算结果过于接近
        // // 如果有该目标点的成功力量值区间，优先在这些区间内生成
        // if (targetForceRanges.ContainsKey(target) && targetForceRanges[target].Count > 0)
        // {
        //     // 检查是否在成功区间内尝试了太多次
        //     var (successCount, totalAttempts) = successRates[target];
        //     if (totalAttempts > 50 && successCount < totalAttempts * 0.2f) // 如果尝试超过50次且成功率低于20%
        //     {
        //         Debug.Log($"目标点 {target.name} 在成功区间内尝试 {totalAttempts} 次，成功率过低 ({successCount}/{totalAttempts})，切换到全局范围");
        //         return Random.Range(minThrustForce, maxThrustForce);
        //     }

        //     // 随机选择一个成功区间
        //     var ranges = targetForceRanges[target];
        //     var range = ranges[Random.Range(0, ranges.Count)];
        //     // 在区间内生成一个随机值
        //     return Random.Range(range.minForce, range.maxForce);
        // }

        // 如果没有成功区间，使用全局范围
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
            // 将缓存中的参数按力量大小排序
            var sortedParams = launchParamsCache[target].OrderBy(p => p.force.magnitude).ToList();
            
            // 将排序后的参数分成三组：小、中、大
            int groupSize = Mathf.Max(1, sortedParams.Count / 3);
            var smallGroup = sortedParams.Take(groupSize).ToList();
            var mediumGroup = sortedParams.Skip(groupSize).Take(groupSize).ToList();
            var largeGroup = sortedParams.Skip(groupSize * 2).ToList();

            // 随机选择一个组（权重：小30%，中40%，大30%）
            float random = Random.value;
            List<(Vector2 position, Vector2 force)> selectedGroup;
            if (random < 0.3f)
                selectedGroup = smallGroup;
            else if (random < 0.7f)
                selectedGroup = mediumGroup;
            else
                selectedGroup = largeGroup;

            // 从选中的组中随机选择一个参数
            int randomIndex = Random.Range(0, selectedGroup.Count);
            var launchParams = selectedGroup[randomIndex];

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
}
