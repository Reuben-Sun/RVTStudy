using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QuadtreeNode
{
    #region Constant

    private const int MAX_SPLIT_COUNT = 8;  //每帧最大细分次数
    private const int CHILDREN_NUM = 4;     //四叉树自然有四个子节点

    private PosOffset[] PosOffsets = new[]
    {
        new PosOffset() {offsetX = 0, offsetZ = 0},
        new PosOffset() {offsetX = 1, offsetZ = 0},
        new PosOffset() {offsetX = 0, offsetZ = 1},
        new PosOffset() {offsetX = 1, offsetZ = 1}
    };
    
    #endregion
    
    #region Public Attribute

    public int x;
    public int z;
    public int size;
    public QuadtreeNode parentNode;
    public QuadtreeNode[] childrenNodes;
    
    public bool parentMerged;    //当前帧parent是否被合并过

    public int textureArrayIndex = -1;    //当前节点在 Texture2DArray的索引

    #endregion

    #region Static


    public static Queue<QuadtreeNode> allLeaves;    //当前帧所有叶子节点

    private static Queue<QuadtreeNode> nextAllLeaves;    //下一帧所有叶子节点

    public static Queue<int> emptyTextureArrayIndexQueue;    //可用的贴图地址队列

    private static Action<QuadtreeNode> onLoadData;    //某节点需要加载贴图资源时的回调

    private static int splitCount;    //当前帧细分次数

    
    //出队，得到一个空闲贴图地址
    private static int GetTextureArrayIndex()
    {
        return emptyTextureArrayIndexQueue.Dequeue();
    }
    //入队，释放一个节点的贴图地址
    private static void ResetTextureArrayIndex(QuadtreeNode node)
    {
        if (node.textureArrayIndex > -1)
        {
            emptyTextureArrayIndexQueue.Enqueue(node.textureArrayIndex);
        }
    }
    #endregion

    //是否为叶子节点
    public bool IsLeaf { get { return childrenNodes == null; } }
    
    //创建根节点
    public static QuadtreeNode CreateRoot(int rootSize, int indexCount, Action<QuadtreeNode> action)
    {
        QuadtreeNode.onLoadData = action;
        allLeaves = new Queue<QuadtreeNode>();
        nextAllLeaves = new Queue<QuadtreeNode>();
        emptyTextureArrayIndexQueue = new Queue<int>();
        for (int i = 0; i < indexCount; i++)
        {
            emptyTextureArrayIndexQueue.Enqueue(i);
        }

        var root = new QuadtreeNode();
        root.textureArrayIndex = GetTextureArrayIndex();
        root.size = rootSize;
        allLeaves.Enqueue(root);
        return root;
    }
    
    //刷新叶节点状态，主循环
    public static void UpdateAllLeavesState(Vector2 cameraPos)
    {
        splitCount = 0;
        nextAllLeaves.Clear();
        while (allLeaves.Count > 0)
        {
            var node = allLeaves.Dequeue();
            node.UpdateState(cameraPos);
        }
        (allLeaves, nextAllLeaves) = (nextAllLeaves, allLeaves);    //交换
    }

    private void UpdateState(Vector2 cameraPos)
    {
        //先判断父节点是否需要合并
        //父节点已经合并过，自然不需要合并
        if (parentMerged)
        {
            return;
        }
        if (parentNode != null)
        {
            int parentLODSize = parentNode.CalculateLODSize(cameraPos);
            bool allBrotherAreLeaf = true;
            for (int i = 0; i < CHILDREN_NUM; i++)
            {
                if (!parentNode.childrenNodes[i].IsLeaf)
                {
                    allBrotherAreLeaf = false;
                }
            }

            if (parentNode.size <= parentLODSize && allBrotherAreLeaf)
            {
                parentNode.Merge();
                return;
            }
        }
        
        //判断自己的状态
        int LODSize = CalculateLODSize(cameraPos);
        
        if (size == LODSize)    //若自己大小正合适，那么不变
        {
            nextAllLeaves.Enqueue(this);
        }
        else if (size > LODSize)    //若自己太大，能裂则裂，不能裂
        {
            splitCount++;
            if (splitCount < MAX_SPLIT_COUNT && emptyTextureArrayIndexQueue.Count >= CHILDREN_NUM - 1)
            {
                Split();
            }
            else
            {
                nextAllLeaves.Enqueue(this);
            }
        }
        else
        {
            nextAllLeaves.Enqueue(this);
        }
        
    }
    
    //合并
    private void Merge()
    {
        //将自己放入
        nextAllLeaves.Enqueue(this);    
        //标记四个子对象，并回收资源
        for (int i = 0; i < CHILDREN_NUM; i++)
        {
            ResetTextureArrayIndex(childrenNodes[i]);
            childrenNodes[i].parentNode = null;
            childrenNodes[i].parentMerged = true;
        }

        textureArrayIndex = GetTextureArrayIndex();
        onLoadData(this);
        childrenNodes = null;
    }

    //细分
    private void Split()
    {
        //将自己释放
        ResetTextureArrayIndex(this);
        //添加子物体
        childrenNodes = new QuadtreeNode[CHILDREN_NUM];
        for (int i = 0; i < CHILDREN_NUM; i++)
        {
            childrenNodes[i] = new QuadtreeNode();
            childrenNodes[i].x = x + size * PosOffsets[i].offsetX / 2;
            childrenNodes[i].z = z + size * PosOffsets[i].offsetZ / 2;
            childrenNodes[i].parentNode = this;
            childrenNodes[i].size = size / 2;
            childrenNodes[i].textureArrayIndex = GetTextureArrayIndex();
            nextAllLeaves.Enqueue(childrenNodes[i]);
            onLoadData(childrenNodes[i]);
        }
    }
    private int CalculateLODSize(Vector2 cameraPos)
    {
        var dis = CalculateClosestPoint(cameraPos, new Vector2(x + size / 2, z + size / 2), new Vector2(size / 2, size / 2));
        dis = Mathf.Max(1, Mathf.Sqrt(dis));
        // int lod = Mathf.Max(0, (int)(Mathf.Log(dis, 2) + 0.5));
        int lod = Mathf.Max(0, (int)(Mathf.Log(dis, 2) + 0.5));
        
        return 1 << lod;
    }

    private float CalculateClosestPoint(Vector2 pos, Vector2 centerPos, Vector2 aabbExt)
    {
        // compute coordinates of point in box coordinate system
        Vector2 closestPos = pos - centerPos;

        // project test point onto box
        float fSqrDistance = 0;
        float fDelta = 0;

        for (int i = 0; i < 2; i++)
        {
            if (closestPos[i] < -aabbExt[i])
            {
                fDelta = closestPos[i] + aabbExt[i];
                fSqrDistance += fDelta * fDelta;
                closestPos[i] = -aabbExt[i];
            }
            else if (closestPos[i] > aabbExt[i])
            {
                fDelta = closestPos[i] - aabbExt[i];
                fSqrDistance += fDelta * fDelta;
                closestPos[i] = aabbExt[i];
            }
        }

        return fSqrDistance;
    }
    #region UtilsStruct

    private struct PosOffset
    {
        public int offsetX;
        public int offsetZ;
    }

    #endregion
}
