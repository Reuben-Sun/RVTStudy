using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QuadtreeManager : MonoBehaviour
{
    private QuadtreeNode root;

    private void onLoadNodeData(QuadtreeNode node)
    {
        Debug.Log("Node Data Loaded!");
    }

    private void Start()
    {
        root = QuadtreeNode.CreateRoot(1024, 100, onLoadNodeData);
    }

    private void Update()
    {
        QuadtreeNode.UpdateAllLeavesState(new Vector2(Camera.main.transform.position.x, Camera.main.transform.position.z));
    }

    private void OnDrawGizmos()
    {
        if(root == null) return;
        Gizmos.color = Color.green;

        int leafCount = 0;
        foreach (var leaf in QuadtreeNode.allLeaves)
        {
            Gizmos.DrawWireCube(new Vector3(leaf.x + leaf.size / 2.0f, 0, leaf.z + leaf.size / 2.0f), new Vector3(1, 0, 1) * leaf.size);
            //UnityEditor.Handles.Label(new Vector3(leaf.x + leaf.size / 2.0f, 0, leaf.z + leaf.size / 2.0f), leaf.textureArrayIndex + "");
            leafCount++;
        }
    }
}
