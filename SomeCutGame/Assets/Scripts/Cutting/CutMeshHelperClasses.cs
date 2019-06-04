using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshTriangleGraph
{
    public List<Vector3> vertices;
    public List<Vector3> normals;
    public List<Vector2> uvs;

    public List<VertexNode> triangles;

    public MeshTriangleGraph(Vector3[] meshVertices, Vector3[] meshNormals, Vector2[] meshUVs, int[][] meshTriangles)
    {
        vertices = new List<Vector3>(meshVertices);
        normals = new List<Vector3>(meshNormals);
        uvs = new List<Vector2>(meshUVs);

        triangles = new List<VertexNode>();
        for (int i = 0; i < meshTriangles.Length; i++)
        {
            for (int j = 0; j < meshTriangles[i].Length; j += 3)
            {
                //get triangle
                for (int t = 0; t < 3; t++)
                {
                    int _vertexID = meshTriangles[i][j + t];

                    bool _wasAdded = false;
                    for (int m = 0; m < i; m++)
                    {
                        for (int v = 0; v < meshTriangles[m].Length; v++)
                        {
                            if (vertices[meshTriangles[m][v]] == vertices[meshTriangles[i][j]])
                            {
                                triangles.Add(new VertexNode(meshTriangles[m][v], i));
                                _wasAdded = true;
                                break;
                            }
                        }
                        if (_wasAdded)
                            break;
                    }
                    if (!_wasAdded)
                    {
                        triangles.Add(new VertexNode(meshTriangles[i][j], i));
                    }
                }
            }
        }

        VertexNode[] _nodeTriangle = new VertexNode[3];
        for (int i = 0; i < triangles.Count; i++)
        {
            for (int j = 0; j < triangles.Count; j += 3)
            {
                for (int t = 0; t < 3; t++)
                {
                    _nodeTriangle[t] = triangles[j + t];
                }

                bool _vertexPresented = false;
                for (int t = 0; t < 3; t++)
                {
                    if (_nodeTriangle[t].vertexID == triangles[i].vertexID)
                    {
                        _vertexPresented = true;
                        break;
                    }
                }
                if (_vertexPresented)
                {
                    for (int t = 0; t < 3; t++)
                    {
                        if (_nodeTriangle[t].vertexID != triangles[i].vertexID)
                        {
                            if (!triangles[i].connectedVertices.Contains(_nodeTriangle[t]))
                            {
                                triangles[i].connectedVertices.Add(_nodeTriangle[t]);
                            }
                        }
                    }
                }
            }
        }
    }
}

public class VertexNode
{
    public int vertexID;
    public List<int> subMeshIndexes;

    public List<VertexNode> connectedVertices;

    public VertexNode(int vertexID, int subMeshIndex)
    {
        this.vertexID = vertexID;
        subMeshIndexes = new List<int>();
        subMeshIndexes.Add(subMeshIndex);
        connectedVertices = new List<VertexNode>();
    }
}