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

    public List<TargetData> targetDatas = new List<TargetData>();
    public int totalSimulationAttempts;
    public bool isDataValid;

    public void ClearData()
    {
        targetDatas.Clear();
        totalSimulationAttempts = 0;
        isDataValid = false;
    }

    public void SaveData(Dictionary<Transform, List<(Vector2 position, Vector2 force)>> launchParamsCache,
                        Dictionary<Transform, (int successCount, int totalAttempts)> successRates,
                        Dictionary<Transform, List<(float minForce, float maxForce)>> targetForceRanges,
                        int totalAttempts)
    {
        ClearData();
        totalSimulationAttempts = totalAttempts;

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

            targetDatas.Add(targetData);
        }

        isDataValid = true;
    }

    public void LoadData(out Dictionary<Transform, List<(Vector2 position, Vector2 force)>> launchParamsCache,
                        out Dictionary<Transform, (int successCount, int totalAttempts)> successRates,
                        out Dictionary<Transform, List<(float minForce, float maxForce)>> targetForceRanges)
    {
        launchParamsCache = new Dictionary<Transform, List<(Vector2 position, Vector2 force)>>();
        successRates = new Dictionary<Transform, (int successCount, int totalAttempts)>();
        targetForceRanges = new Dictionary<Transform, List<(float minForce, float maxForce)>>();

        foreach (var targetData in targetDatas)
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
    }
} 