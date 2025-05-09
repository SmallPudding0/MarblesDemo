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
    public int maxSimulationAttempts = 100000;     // 最大尝试次数
    public float minThrustForce = 9f;         // 最小推力
    public float maxThrustForce = 20f;         // 最大推力
    public int requiredSuccessCount = 100;     // 每个目标点需要的成功次数
    [Tooltip("强制重新计算所有数据，忽略已保存的数据")]
    public bool forceRecalculate = false;      // 是否强制重新计算

    [Header("引用设置")]
    public GameObject ballPrefab;    // 添加对球预制体的引用
    public Transform[] targetPoints; // 多个可能的目标点
    public float acceptableDistance = 0.5f;   // 可接受的误差范围
    public Transform spawnArea; // 小球初始位置
    public float spawnRadius = 0.5f; // 小球初始点半径
    public BallPredictionData predictionData; // 数据持久化引用
    public GameObject[] pinsList;   // 所有柱子组合
    [SerializeField]
    private int currentPinsIndex = 0; // 当前使用的柱子组合索引
    public bool useDateBasedSelection = true; // 是否使用基于日期的选择

    [Header("调试设置")]
    public bool showDebugVisuals = true;           // 是否显示调试视觉效果
    public Color trajectoryColor = Color.yellow;    // 轨迹线颜色
    public float debugPointSize = 0.2f;            // 调试点大小

    [Header("目标点设置")]
    public Transform selectedTargetPoint; // 在Inspector中选择的目标点
    public bool useRandomTarget = false;  // 是否随机选择目标点

    // 在检视窗口中显示的进度信息
    [Header("进度信息")]
    [SerializeField]
    private string computationStatus = "未开始计算";
    [SerializeField]
    private Dictionary<Transform, string> targetStatus = new Dictionary<Transform, string>();    // 目标点状态

    private int totalSimulationAttempts = 0;    // 总尝试次数

    private Dictionary<Transform, BallPredictionData.TargetData> targetDatas = new Dictionary<Transform, BallPredictionData.TargetData>();  // 成功进入目标点的数据
    private List<BallPredictionData.TransformInfo> currentTrajectory;        // 当前轨迹
    private bool isComputing = false;  // 是否正在计算中

    void Start()
    {
        if (spawnArea == null)
        {
            Debug.LogError("请设置弹簧位置！");
            return;
        }

        // 根据日期选择当前柱子组合
        if (useDateBasedSelection)
        {
            currentPinsIndex = System.DateTime.Now.Day % pinsList.Length;
            Debug.Log($"当前日期: {System.DateTime.Now.Day}, 选择的柱子组合索引: {currentPinsIndex}");
        }

        // 初始化数据结构
        targetDatas = new Dictionary<Transform, BallPredictionData.TargetData>();
        foreach (var target in targetPoints)
        {
            targetDatas[target] = new BallPredictionData.TargetData
            {
                targetName = target.name,
                targetPosition = target.position,
                successCount = 0,
                totalAttempts = 0
            };
        }

        // 更新显示的柱子组合
        UpdatePinsConfiguration();

        // 预计算所有柱子组合的数据
        StartCoroutine(PrecomputeAllConfigurations());
    }

    // 预计算所有柱子组合的数据
    private IEnumerator PrecomputeAllConfigurations()
    {
        // 保存当前柱子组合的索引
        int originalPinsIndex = currentPinsIndex;

        if (forceRecalculate)
        {
            predictionData.ClearData();
        }

        // 遍历所有柱子组合
        for (int i = 0; i < pinsList.Length; i++)
        {
            // 设置当前柱子组合
            currentPinsIndex = i;
            float totalProgress = (float)i / pinsList.Length * 100f;
            computationStatus = $"总进度: {totalProgress:F1}% - 正在计算柱子组合 {i}/{pinsList.Length - 1}";
            Debug.Log($"开始计算柱子组合 {i} 的数据");

            // 激活当前柱子组合，隐藏其他组合
            for (int j = 0; j < pinsList.Length; j++)
            {
                pinsList[j].SetActive(j == i);
            }

            // 检查是否有已保存的数据
            string configName = $"PinsConfig_{i}";
            bool loadSuccess = !forceRecalculate && LoadData(configName);

            if (!loadSuccess)
            {
                // 如果没有数据或强制重新计算，重新计算
                yield return StartCoroutine(PrecomputeAllTargets());
            }
            else
            {
                computationStatus = $"总进度: {totalProgress:F1}% - 柱子组合 {i}/{pinsList.Length - 1} 的数据已加载";
                Debug.Log($"柱子组合 {i} 的数据已加载");
            }

            // 每计算完一个组合后等待一帧
            yield return null;
        }

        // 恢复原始柱子组合
        currentPinsIndex = originalPinsIndex;
        for (int i = 0; i < pinsList.Length; i++)
        {
            pinsList[i].SetActive(i == currentPinsIndex);
        }

        computationStatus = "所有柱子组合的数据计算完成 (100%)";
        Debug.Log("所有柱子组合的数据计算完成");
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

        // 清空所有数据
        targetDatas.Clear();
        totalSimulationAttempts = 0;

        foreach (var target in targetPoints)
        {
            targetDatas[target] = new BallPredictionData.TargetData
            {
                targetName = target.name,
                targetPosition = target.position,
                successCount = 0,
                totalAttempts = 0
            };
            targetStatus[target] = "等待计算";
        }

        // 用于检查重复值的集合
        HashSet<(float x, float force)> usedParams = new HashSet<(float x, float force)>();

        while (totalSimulationAttempts < maxSimulationAttempts)
        {
            // 检查是否所有目标点都已完成
            bool allTargetsCompleted = true;
            foreach (var target in targetPoints)
            {
                if (targetDatas[target].successCount < requiredSuccessCount)
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
                if (targetDatas[target].successCount < requiredSuccessCount)
                {
                    targetToTry = target;
                    break;
                }
            }

            if (targetToTry == null)
            {
                Debug.Log("所有目标点都已完成！");
                break;
            }

            // 生成力量值
            float currentForce = GenerateForceValue(targetToTry);
            Vector2 basePosition = spawnArea.position;
            Vector2 baseForce = Vector2.up * currentForce;

            // 在左右位置之间随机选择一个位置
            basePosition.x = Random.Range(spawnArea.position.x - spawnRadius, spawnArea.position.x + spawnRadius);

            // 检查是否重复
            var paramKey = (basePosition.x, currentForce);
            if (usedParams.Contains(paramKey))
            {
                continue;
            }
            usedParams.Add(paramKey);

            // 增加当前目标点的尝试次数
            targetDatas[targetToTry].totalAttempts++;

            var (finalPosition, hitTarget, simulationTime) = SimulateAndRecord(basePosition, baseForce);
            totalSimulationAttempts++;

            // 记录所有成功的数据
            if (hitTarget != null)
            {
                // 更新被击中目标点的统计
                targetDatas[hitTarget].successCount++;

                // 更新成功的力量值区间
                UpdateSuccessfulForceRanges(hitTarget, currentForce);

                // 更新目标点状态
                targetStatus[hitTarget] = $"成功: {targetDatas[hitTarget].successCount}/{requiredSuccessCount}";

                if (targetDatas[hitTarget].successCount < requiredSuccessCount)
                {
                    // 记录成功数据
                    var launchData = new BallPredictionData.LaunchData
                    {
                        position = basePosition,
                        force = baseForce,
                        trajectory = new List<BallPredictionData.TransformInfo>(currentTrajectory),
                        trajectoryTime = simulationTime
                    };
                    targetDatas[hitTarget].successfulLaunches.Add(launchData);
                }
            }

            // 每计算100次输出一次进度
            if (totalSimulationAttempts % 100 == 0)
            {
                float totalProgress = (float)currentPinsIndex / pinsList.Length * 100f;
                foreach (var target in targetPoints)
                {
                    Debug.Log($"目标点 {target.name}: {targetDatas[target].successCount}/{requiredSuccessCount}, 尝试次数: {targetDatas[target].totalAttempts}, 总尝试 {totalSimulationAttempts} 次");
                }

                // 计算并显示完成率
                int completedCount = targetPoints.Count(t => targetDatas[t].successCount >= requiredSuccessCount);
                float completionRate = (float)completedCount / targetPoints.Length * 100f;
                Debug.Log($"当前完成率: {completionRate:F1}% ({completedCount}/{targetPoints.Length})");
                computationStatus = $"总进度: {totalProgress:F1}% - 柱子组合 {currentPinsIndex}/{pinsList.Length - 1} - 目标点: {targetToTry?.name ?? "无"}，进度: {completionRate:F1}%";
            }

            // 每计算10次休息一帧，避免卡顿
            if (totalSimulationAttempts % 10 == 0)
                yield return null;
        }

        // 打印每个目标点的成功力量区间
        Debug.Log("\n各目标点的成功力量区间：");
        foreach (var target in targetPoints)
        {
            if (targetDatas[target].forceRanges.Count > 0)
            {
                Debug.Log($"\n目标点 {target.name} 的成功力量区间：");
                var ranges = targetDatas[target].forceRanges;
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
        foreach (var target in targetPoints)
        {
            var targetData = targetDatas[target];
            int totalAttempts = targetData.totalAttempts == 0 ? requiredSuccessCount : targetData.totalAttempts;
            float successRate = (float)targetData.successCount / totalAttempts * 100;
            targetStatus[target] = $"成功率: {successRate:F2}% ({targetData.successCount}/{totalAttempts})";
            Debug.Log($"目标点 {target.name}: 成功率 {successRate:F2}%, 成功 {targetData.successCount} 次, 总尝试 {totalAttempts} 次");
        }

        // 在计算完成后保存数据
        SaveAllData();

        isComputing = false;
        float finalProgress = (float)(currentPinsIndex + 1) / pinsList.Length * 100f;
        computationStatus = $"总进度: {finalProgress:F1}% - 柱子组合 {currentPinsIndex}/{pinsList.Length - 1} 计算完成";
        Debug.Log("所有目标点计算完成！");
    }

    // 生成力量值
    private float GenerateForceValue(Transform target)
    {
        // 检查是否大部分目标点已经达到要求
        int completedTargets = 0;
        foreach (var t in targetPoints)
        {
            if (targetDatas[t].successCount >= requiredSuccessCount)
            {
                completedTargets++;
            }
        }

        float completionRate = (float)completedTargets / targetPoints.Length;

        // 如果60%以上的目标点已完成，且当前目标点有成功的力量区间
        if (completionRate >= 0.6f && targetDatas[target].forceRanges.Count > 0)
        {
            // 从当前目标点的成功区间中随机选择一个区间
            var ranges = targetDatas[target].forceRanges;
            var selectedRange = ranges[Random.Range(0, ranges.Count)];

            // 在选中的区间内随机生成力量值
            return Random.Range(selectedRange.minForce, selectedRange.maxForce);
        }

        // 否则使用全局范围
        return Random.Range(minThrustForce, maxThrustForce);
    }

    // 更新成功的力量值区间
    private void UpdateSuccessfulForceRanges(Transform target, float successfulForce)
    {
        var ranges = targetDatas[target].forceRanges;

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
    private (Vector2, Transform, float) SimulateAndRecord(Vector2 basePosition, Vector2 baseForce)
    {
        currentTrajectory = new List<BallPredictionData.TransformInfo>
        {
            new BallPredictionData.TransformInfo(
                new Vector3(basePosition.x, basePosition.y, 0),
                Quaternion.identity.eulerAngles.z)
        };

        GameObject simulatedBall = Instantiate(ballPrefab, basePosition, Quaternion.identity, spawnArea);
        simulatedBall.SetActive(true);

        Rigidbody2D rb = simulatedBall.GetComponent<Rigidbody2D>();
        BallController ballContrl = simulatedBall.AddComponent<BallController>();

        rb.velocity = Vector2.zero;
        rb.AddForce(baseForce, ForceMode2D.Impulse);

        Vector2 finalPosition = Vector2.zero;
        float simulationTime = 0f;
        float maxSimulationTime = 20f;

        // 保存当前的物理模拟模式
        var previousSimulationMode = Physics2D.simulationMode;
        // 设置为Script模式
        Physics2D.simulationMode = SimulationMode2D.Script;

        while (simulationTime < maxSimulationTime)
        {
            Physics2D.Simulate(Time.fixedDeltaTime);
            simulationTime += Time.fixedDeltaTime;

            finalPosition = rb.position;
            // 记录轨迹点
            currentTrajectory.Add(new BallPredictionData.TransformInfo(rb.transform));

            if (ballContrl.hasCollided)
            {
                Debug.Log($"与目标点 {ballContrl.hitTarget.name} 发生碰撞！");
                Debug.Log($"模拟落点: {finalPosition}, 目标点位置: {ballContrl.hitTarget.position}");
                break;
            }

            if (rb.velocity.magnitude < 0.01f)
            {
                Debug.Log($"模拟结束 - 小球停止运动 最终落点: {finalPosition}");
                break;
            }
        }

        // 恢复原来的模拟模式
        Physics2D.simulationMode = previousSimulationMode;
        Destroy(simulatedBall);

        return (finalPosition, ballContrl.hitTarget, simulationTime);
    }

    // 当弹簧释放时调用此方法
    public void OnSpringReleased(GameObject ball, float springPercentage)
    {
        Transform targetPoint = useRandomTarget ?
            targetPoints[Random.Range(0, targetPoints.Length)] :
            selectedTargetPoint;

        // 获取当前柱子组合的数据
        string configName = $"PinsConfig_{currentPinsIndex}";
        if (!LoadData(configName))
        {
            Debug.LogError($"柱子组合 {currentPinsIndex} 的数据加载失败");
            return;
        }

        if (targetPoint != null && targetDatas.ContainsKey(targetPoint))
        {
            var targetData = targetDatas[targetPoint];
            if (targetData.successfulLaunches.Count > 0)
            {
                var currentX = ball.transform.position.x;

                // 按力量大小排序
                var suitableLaunches = targetData.successfulLaunches
                    .OrderBy(l => l.force.y)
                    .ToList();

                if (suitableLaunches.Count > 0)
                {
                    float minForce = suitableLaunches[0].force.y;
                    float maxForce = suitableLaunches[suitableLaunches.Count - 1].force.y;
                    float forceRange = maxForce - minForce;

                    // 根据弹簧位置选择不同区间的力量
                    int startIndex, endIndex;
                    if (springPercentage >= 0.7f)
                    {
                        float targetForce = minForce + forceRange * 0.7f;
                        startIndex = suitableLaunches.FindIndex(l => l.force.y >= targetForce);
                        endIndex = suitableLaunches.Count - 1;
                    }
                    else if (springPercentage >= 0.3f)
                    {
                        float minTargetForce = minForce + forceRange * 0.3f;
                        float maxTargetForce = minForce + forceRange * 0.7f;
                        startIndex = suitableLaunches.FindIndex(l => l.force.y >= minTargetForce);
                        endIndex = suitableLaunches.FindLastIndex(l => l.force.y <= maxTargetForce);
                    }
                    else
                    {
                        float targetForce = minForce + forceRange * 0.3f;
                        startIndex = 0;
                        endIndex = suitableLaunches.FindLastIndex(l => l.force.y <= targetForce);
                    }

                    if (startIndex < 0) startIndex = 0;
                    if (endIndex >= suitableLaunches.Count) endIndex = suitableLaunches.Count - 1;
                    if (startIndex > endIndex) startIndex = endIndex;

                    // 在选定的区间内筛选
                    var closeLaunches = suitableLaunches
                        .Skip(startIndex)
                        .Take(endIndex - startIndex + 1)
                        .ToList();

                    var selectedLaunch = closeLaunches[Random.Range(0, closeLaunches.Count)];

                    var ballController = ball.GetComponent<BallController>();
                    if (ballController != null)
                    {
                        ballController.SetTargetPosition(
                            selectedLaunch.trajectory,
                            selectedLaunch.trajectoryTime
                        );

                        float xDistance = Mathf.Abs(ball.transform.position.x - selectedLaunch.position.x);
                        Debug.Log($"当前柱子组合: {currentPinsIndex}, " +
                                $"弹簧位置百分比: {springPercentage:F2}, " +
                                $"选择的力量: {selectedLaunch.force.y:F2}, " +
                                $"X轴距离: {xDistance:F2}, " +
                                $"力量区间: {(springPercentage >= 0.7f ? "最大" : springPercentage >= 0.3f ? "中等" : "最小")}, " +
                                $"力量范围: {minForce:F2}-{maxForce:F2}");
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
        if (spawnArea != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(spawnArea.position, 0.3f);
            // 显示坐标
            UnityEditor.Handles.Label(spawnArea.position, $"小球初始位置: {spawnArea.position}");
        }

        // 绘制轨迹
        if (currentTrajectory != null && currentTrajectory.Count > 1)
        {
            Gizmos.color = trajectoryColor;
            for (int i = 0; i < currentTrajectory.Count - 1; i++)
            {
                Gizmos.DrawLine(currentTrajectory[i].position, currentTrajectory[i + 1].position);
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
                if (targetDatas.ContainsKey(target))
                {
                    var targetData = targetDatas[target];
                    float successRate = targetData.totalAttempts > 0 ? (float)targetData.successCount / targetData.totalAttempts * 100 : 0;
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
            Gizmos.DrawWireSphere(spawnArea.position, 0.5f);
        }
    }
#endif

    // 在最后保存所有数据
    private void SaveAllData()
    {
        if (predictionData != null)
        {
            string configName = $"PinsConfig_{currentPinsIndex}";
            predictionData.SaveData(configName, targetDatas, totalSimulationAttempts);

            // 只在编辑器中执行保存操作
#if UNITY_EDITOR
            EditorUtility.SetDirty(predictionData);
            AssetDatabase.SaveAssets();
            Debug.Log("数据已保存到磁盘");
#endif

            Debug.Log("数据已更新到 ScriptableObject");
        }
    }

    // 当currentPinsIndex在Inspector中改变时调用
    private void OnValidate()
    {
        if (pinsList == null || pinsList.Length == 0) return;

        // 确保索引在有效范围内
        currentPinsIndex = Mathf.Clamp(currentPinsIndex, 0, pinsList.Length - 1);

        // 在下一帧更新显示的柱子组合
        if (Application.isPlaying)
        {
            StartCoroutine(UpdatePinsConfigurationDelayed());
        }
    }

    // 延迟更新柱子组合
    private IEnumerator UpdatePinsConfigurationDelayed()
    {
        yield return null;
        UpdatePinsConfiguration();
    }

    // 更新当前显示的柱子组合
    private void UpdatePinsConfiguration()
    {
        if (pinsList == null || pinsList.Length == 0) return;

        // 隐藏所有柱子组合
        for (int i = 0; i < pinsList.Length; i++)
        {
            if (pinsList[i] != null)
            {
                pinsList[i].SetActive(i == currentPinsIndex);
            }
        }
    }

    // 修改 LoadData 方法
    private bool LoadData(string configName)
    {
        Dictionary<Transform, BallPredictionData.TargetData> loadedTargetDatas;
        if (predictionData.LoadData(configName, out loadedTargetDatas))
        {
            targetDatas = loadedTargetDatas;
            return true;
        }
        return false;
    }
}
