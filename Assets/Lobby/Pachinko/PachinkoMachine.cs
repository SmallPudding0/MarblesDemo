using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PachinkoMachine : MonoBehaviour
{
    public GameObject self;
    public BallController ballPrefab; // Prefab for the ball
    public Transform spawnArea; // Area where balls will spawn
    public List<BallController> ballList; // List to keep track of spawned balls

    public float spawnRadius = 0.5f; // Radius to check for collisions
    public int rows = 5; // Number of rows for the grid
    public int columns = 2; // Number of columns for the grid

    public void SpawnOneBall(){
        ballList.Clear();
        Vector3 center = spawnArea.position;
        float x = center.x;
        float y = center.y;
        Vector3 spawnPosition = new Vector3(x, y, 0);

        BallController ball = Instantiate(ballPrefab, spawnPosition, Quaternion.identity, spawnArea);
        ballList.Add(ball);
    }
    // Method to spawn a specified amount of balls in a rectangular grid
    public void SpawnTenBalls()
    {
        ballList.Clear();
        // Calculate the bounds based on the spawnArea's position and scale
        Vector3 center = spawnArea.position;
        Vector3 size = spawnArea.localScale;

        // Calculate the width and height of each ball based on spawn radius
        float ballDiameter = spawnRadius * 2;
        float totalWidth = size.x * ballDiameter; // Total width of the spawn area
        float totalHeight = size.y * ballDiameter; // Total height of the spawn area

        // Calculate spacing between balls
        float spacingX = totalWidth / columns;
        float spacingY = totalHeight / rows;

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                // Calculate the spawn position for each ball
                float x = center.x+column*0.01f;
                float y = center.y+row*0.01f;
                Vector3 spawnPosition = new Vector3(x, y, 0);

                BallController ball = Instantiate(ballPrefab, spawnPosition, Quaternion.identity, spawnArea);
                ballList.Add(ball);
            }
        }
    }

    public bool IsClear()
    {
        // Check if there are no children in the spawn area
        return spawnArea.childCount == 0;
    }

    public void ShowMachine() {
        if (!self.activeSelf) {
            self.SetActive(true);
        }
    }

    public void HideMachine() {
        if (self.activeSelf) {
            self.SetActive(false);
        }
    }
}
