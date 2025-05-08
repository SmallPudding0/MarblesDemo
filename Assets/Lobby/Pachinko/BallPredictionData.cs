using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "BallPredictionData", menuName = "Pachinko/BallPredictionData")]
public class BallPredictionData : ScriptableObject
{
    [System.Serializable]
    public struct TransformInfo
    {
        public Vector2 position;
        public float rotation; // 使用角度表示旋转

        public TransformInfo(Vector2 pos, float rot)
        {
            position = pos;
            rotation = rot;
        }

        public TransformInfo(Transform transform)
        {
            position = transform.position;
            rotation = transform.rotation.eulerAngles.z;
        }

        // 转换为 Transform 数据
        public Vector3 GetPosition()
        {
            return new Vector3(position.x, position.y, 0);
        }

        public Quaternion GetRotation()
        {
            return Quaternion.Euler(0, 0, rotation);
        }
    }

    [System.Serializable]
    public struct LaunchData
    {
        public Vector2 position;
        public Vector2 force;
        public List<TransformInfo> trajectory;
        public float trajectoryTime;
    }

    [System.Serializable]
    public class TargetData
    {
        public string targetName;
        public Vector3 targetPosition;

        // 存储成功发射的参数
        public List<LaunchData> successfulLaunches = new List<LaunchData>();

        // 成功率统计
        public int successCount;
        public int totalAttempts;

        // 力量区间
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

    public void SaveData(string configurationName, Dictionary<Transform, TargetData> targetDatas, int totalAttempts)
    {
        // 查找或创建配置
        var config = pinsConfigurations.Find(c => c.configurationName == configurationName);
        if (config == null)
        {
            config = new PinsConfigurationData { configurationName = configurationName };
            pinsConfigurations.Add(config);
        }

        // 清空现有数据
        config.targetDatas.Clear();
        config.totalSimulationAttempts = totalAttempts;
        config.isDataValid = true;

        // 保存每个目标点的数据
        foreach (var kvp in targetDatas)
        {
            var targetData = new TargetData
            {
                targetName = kvp.Key.name,
                targetPosition = kvp.Key.position,
                successfulLaunches = new List<LaunchData>(kvp.Value.successfulLaunches),
                successCount = kvp.Value.successCount,
                totalAttempts = kvp.Value.totalAttempts,
                forceRanges = new List<(float, float)>(kvp.Value.forceRanges)
            };
            config.targetDatas.Add(targetData);
        }

        currentConfigurationName = configurationName;
    }

    public bool LoadData(string configurationName, out Dictionary<Transform, TargetData> targetDatas)
    {
        targetDatas = new Dictionary<Transform, TargetData>();

        var config = pinsConfigurations.Find(c => c.configurationName == configurationName);
        if (config == null || !config.isDataValid)
        {
            return false;
        }

        foreach (var targetData in config.targetDatas)
        {
            // 找到对应的Transform
            var target = GameObject.Find(targetData.targetName)?.transform;
            if (target != null)
            {
                targetDatas[target] = targetData;
            }
        }

        currentConfigurationName = configurationName;
        return true;
    }
}