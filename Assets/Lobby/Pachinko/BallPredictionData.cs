using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "BallPredictionData", menuName = "Pachinko/BallPredictionData")]
public class BallPredictionData : ScriptableObject
{
    [System.Serializable]
    public class TargetData
    {
        public string targetName;
        public Vector3 targetPosition;
        public List<Vector2> successfulPositions = new List<Vector2>();
        public List<Vector2> successfulForces = new List<Vector2>();
        public int successCount;
        public int totalAttempts;
        public List<(float minForce, float maxForce)> forceRanges = new List<(float, float)>();
    }

    [System.Serializable]
    public class PinsConfigurationData
    {
        public string configurationName;  // 柱子组合的名称
        public List<TargetData> targetDatas = new List<TargetData>();
        public int totalSimulationAttempts;
        public bool isDataValid;
    }

    public List<PinsConfigurationData> pinsConfigurations = new List<PinsConfigurationData>();
    public string currentConfigurationName;  // 当前使用的柱子组合名称

    public void ClearData()
    {
        pinsConfigurations.Clear();
        currentConfigurationName = "";
    }

    public void SaveData(string configurationName,
                        Dictionary<Transform, List<(Vector2 position, Vector2 force)>> launchParamsCache,
                        Dictionary<Transform, (int successCount, int totalAttempts)> successRates,
                        Dictionary<Transform, List<(float minForce, float maxForce)>> targetForceRanges,
                        int totalAttempts)
    {
        // 查找或创建配置
        var config = pinsConfigurations.Find(c => c.configurationName == configurationName);
        if (config == null)
        {
            config = new PinsConfigurationData { configurationName = configurationName };
            pinsConfigurations.Add(config);
        }

        // 清除旧数据
        config.targetDatas.Clear();
        config.totalSimulationAttempts = totalAttempts;

        foreach (var kvp in launchParamsCache)
        {
            var target = kvp.Key;
            var targetData = new TargetData
            {
                targetName = target.name,
                targetPosition = target.position
            };

            // 保存成功的位置和力量
            for (int i = 0; i < kvp.Value.Count; i++)
            {
                targetData.successfulPositions.Add(kvp.Value[i].position);
                targetData.successfulForces.Add(kvp.Value[i].force);
            }

            // 保存成功率数据
            if (successRates.ContainsKey(target))
            {
                targetData.successCount = successRates[target].successCount;
                targetData.totalAttempts = successRates[target].totalAttempts;
            }

            // 保存力量区间
            if (targetForceRanges.ContainsKey(target))
            {
                targetData.forceRanges = targetForceRanges[target];
            }

            config.targetDatas.Add(targetData);
        }

        config.isDataValid = true;
        currentConfigurationName = configurationName;
    }

    public bool LoadData(string configurationName,
                        out Dictionary<Transform, List<(Vector2 position, Vector2 force)>> launchParamsCache,
                        out Dictionary<Transform, (int successCount, int totalAttempts)> successRates,
                        out Dictionary<Transform, List<(float minForce, float maxForce)>> targetForceRanges)
    {
        launchParamsCache = new Dictionary<Transform, List<(Vector2 position, Vector2 force)>>();
        successRates = new Dictionary<Transform, (int successCount, int totalAttempts)>();
        targetForceRanges = new Dictionary<Transform, List<(float minForce, float maxForce)>>();

        var config = pinsConfigurations.Find(c => c.configurationName == configurationName);
        if (config == null || !config.isDataValid)
        {
            return false;
        }

        foreach (var targetData in config.targetDatas)
        {
            var positions = new List<(Vector2 position, Vector2 force)>();
            for (int i = 0; i < targetData.successfulPositions.Count; i++)
            {
                positions.Add((targetData.successfulPositions[i], targetData.successfulForces[i]));
            }

            // 找到对应的Transform
            var target = GameObject.Find(targetData.targetName)?.transform;
            if (target != null)
            {
                launchParamsCache[target] = positions;
                successRates[target] = (targetData.successCount, targetData.totalAttempts);
                targetForceRanges[target] = targetData.forceRanges;
            }
        }

        currentConfigurationName = configurationName;
        return true;
    }
} 