using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshTriangleGraph
{
    public List<Vector3> vertices;
    public List<Vector3> normals;
    public List<Vector2> uvs;
    public List<List<int>> triangles;

    public List<VertexNode> vertexNodes;

    public MeshTriangleGraph(Vector3[] meshVertices, Vector3[] meshNormals, Vector2[] meshUVs, int[][] meshTriangles)
    {
        vertices = new List<Vector3>(meshVertices);
        normals = new List<Vector3>(meshNormals);
        uvs = new List<Vector2>(meshUVs);
        triangles = new List<List<int>>(meshTriangles.Length);
        for (int i = 0; i < meshTriangles.Length; i++)
        {
            triangles.Add(new List<int>(meshTriangles[i])) ;
        }

        vertexNodes = new List<VertexNode>(vertices.Count);

        //build graph
        //add all vertices
        for (int i = 0; i < vertices.Count; i++)
        {
            vertexNodes.Add(new VertexNode(i));
        }
        //add connected vertices to evry vertex
        for (int i = 0; i < triangles.Count; i++)
        {
            for (int j = 0; j < triangles[i].Count; j += 3)
            {
                int _vertexA = triangles[i][j];
                int _vertexB = triangles[i][j + 1];
                int _vertexC = triangles[i][j + 2];

                //add to A
                vertexNodes[_vertexA].connectedVertices.Add(_vertexB);
                vertexNodes[_vertexA].connectedVertices.Add(_vertexC);
                //add to B
                vertexNodes[_vertexB].connectedVertices.Add(_vertexC);
                vertexNodes[_vertexB].connectedVertices.Add(_vertexA);
                //add to C
                vertexNodes[_vertexC].connectedVertices.Add(_vertexA);
                vertexNodes[_vertexC].connectedVertices.Add(_vertexB);
            }
        }
        //add double vertices as connected
        for (int i = 0; i < vertices.Count; i++)
        {
            for (int j = 0; j < vertices.Count; j++)
            {
                if (i != j)
                {
                    if (vertices[i] == vertices[j])
                    {
                        vertexNodes[i].doubledVertices.Add(j);
                        vertexNodes[j].doubledVertices.Add(i);
                    }
                }
            }
        }
    }
}

public class VertexNode
{
    public int vertexID;

    public List<int> connectedVertices;
    public List<int> doubledVertices;

    public VertexNode(int vertexID)
    {
        this.vertexID = vertexID;
        connectedVertices = new List<int>();
        doubledVertices = new List<int>();
    }
}