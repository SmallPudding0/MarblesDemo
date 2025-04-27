using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
// using static DataModel;
using TMPro;
using System;
using System.Linq;

public class PachinkoPage : MonoBehaviour
{
    public BasePanel self;
    public GameObject backButton;
    public List<TMP_Text> currencyIcons;
    public List<TMP_Text> currencyTexts;
    public List<TMP_Text> rewardGroup;
    public GameObject questionButton;
    public GameObject pool_1;
    public GameObject pool_2;
    public GameObject pool_3;
    public GameObject exchangeShopButton;
    public GameObject gacha1;
    public GameObject gacha10;
    public GameObject autoButton;
    public static int curPool;

    public PachinkoMachine machine;
    public static Dictionary<int, List<int>> currencyList = new Dictionary<int, List<int>>()
    {
        { 0, new List<int> { 1010014, 1010017 } },
        { 1, new List<int> { 1010015, 1010018 } },
        { 2, new List<int> { 1010016, 1010019 } },
    };
    public void ShowPanel(){
        self.ShowPanel();
    }
    public void ClosePanel()
    {
        if (!machine.IsClear()) return;
        // Call the base class's ClosePanel method
        self.ClosePanel();
        
        // Destroy all children of the spawnArea
        DestroyAllChildren(machine.spawnArea);
    }

    private void DestroyAllChildren(Transform parent)
    {
        // Loop through all child transforms and destroy them
        foreach (Transform child in parent)
        {
            Destroy(child.gameObject);
        }
    }

    public void SpawnOneBall(){
        if (!machine.IsClear()) return;
        machine.SpawnOneBall();
    }
    public void SpawnTenBalls(){
        if (!machine.IsClear()) return;
        machine.SpawnTenBalls();
    }

    #region Helper Funtion
    public static (int, int) SplitStringToInt(string input)
    {
        // Split the string by the asterisk
        string[] parts = input.Split('*');

        // Parse the parts into integers
        int first = int.Parse(parts[0]);
        int second = int.Parse(parts[1]);
        // Debug.Log($"Parsed Values - First: {first}, Second: {second}");
        return (first, second);
    }
    #endregion

    #region Testing(TODO)
    //您必须确保生成的球将沿着设计的路线和目的地移动
    //我将提供一些测试集，以模拟服务器结果的伪随机

    public List<List<int>> testingList = new List<List<int>>()
    {
        // 4 lists with 1 integer each
        new List<int>() { 1 },
        new List<int>() { 5 },
        new List<int>() { 8 },
        new List<int>() { 10 },

        // 4 lists with 10 integers each (randomly chosen from 1 to 10)
        new List<int>() { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 },
        new List<int>() { 10,10,2,3,1,1,2,5,2,3},
        new List<int>() { 2,2,3,1,6,6,1,7,8,2},
        new List<int>() { 5,3,6,7,1,2,3,1,2,3 }
    };
    
    #endregion 


}