using Cutting.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Cutting.Jobs
{
    [BurstCompile]
    public struct CutTrianglesParallelJob : IJobParallelFor
    {
        [ReadOnly] public float3 planeCenter, planeNormal;
        [ReadOnly] public NativeArray<float3> vertices, normals;
        [ReadOnly] public NativeArray<float2> uvs;
        [ReadOnly] public NativeList<int> triangles;
        [ReadOnly] public NativeArray<int> sideIDs;

        [WriteOnly] public NativeHashMap<Edge, VertexInfo>.ParallelWriter edgesToVertices;
        [WriteOnly] public NativeQueue<HalfNewTriangle>.ParallelWriter leftHalfTriangles, rightHalfTriangles;

        public void Execute(int index)
        {
            var currentStartIndex = index * 3;
            var vertexA = triangles[currentStartIndex];
            var vertexB = triangles[currentStartIndex + 1];
            var vertexC = triangles[currentStartIndex + 2];

            //create edges
            var ab = new Edge{a = vertexA, b = vertexB};
            var bc = new Edge{a = vertexB, b = vertexC};
            var ca = new Edge{a = vertexC, b = vertexA};

            //check every edge to find one that is not intersected
            //check a-b
            if (sideIDs[ab.a] == sideIDs[ab.b])
            {
                CutTriangle(ab, bc, ca);
            }
            //check b-c
            else if (sideIDs[bc.a] == sideIDs[bc.b])
            {
                CutTriangle(bc, ca, ab);
            }
            //check c-a
            else if (sideIDs[ca.a] == sideIDs[ca.b])
            {
                CutTriangle(ca, ab, bc);
            }
        }

        private void CutTriangle(Edge solidEdge, Edge firstIntersected, Edge secondIntersected)
        {
            var newVertexOne = GetNewVertex(firstIntersected);
            var newVertexTwo = GetNewVertex(secondIntersected);

            //add vertices
            edgesToVertices.TryAdd(firstIntersected, newVertexOne);
            edgesToVertices.TryAdd(secondIntersected, newVertexTwo);

            //create half-new triangles
            switch (sideIDs[solidEdge.a])
            {
                case -1:
                {
                    //base on right side
                    rightHalfTriangles.Enqueue(new HalfNewTriangle
                    {
                        a = solidEdge.a,
                        b = solidEdge.b,
                        c = firstIntersected,
                        d = new Edge{a = -1, b = -1}
                    });
                    rightHalfTriangles.Enqueue(new HalfNewTriangle
                    {
                        a = solidEdge.a,
                        b = -1,
                        c = firstIntersected,
                        d = secondIntersected
                    });
                    leftHalfTriangles.Enqueue(new HalfNewTriangle
                    {
                        a = firstIntersected.b,
                        b = -1,
                        c = secondIntersected,
                        d = firstIntersected
                    });
                    break;
                }
                default:
                {
                    //base on left side
                    leftHalfTriangles.Enqueue(new HalfNewTriangle
                    {
                        a = solidEdge.a,
                        b = solidEdge.b,
                        c = firstIntersected,
                        d = new Edge{a = -1, b = -1}
                    });
                    leftHalfTriangles.Enqueue(new HalfNewTriangle
                    {
                        a = solidEdge.a,
                        b = -1,
                        c = firstIntersected,
                        d = secondIntersected
                    });
                    rightHalfTriangles.Enqueue(new HalfNewTriangle
                    {
                        a = firstIntersected.b,
                        b = -1,
                        c = secondIntersected,
                        d = firstIntersected
                    });
                    break;
                }
            }
        }

        private VertexInfo GetNewVertex(Edge edge)
        {
            var newVertex = new VertexInfo();

            var relation = FindIntersectionPosOnSegment(vertices[edge.a], vertices[edge.b]);

            newVertex.vertex = math.lerp(vertices[edge.a], vertices[edge.b], relation);
            newVertex.normal = math.lerp(normals[edge.a], normals[edge.b], relation);
            newVertex.uv = math.lerp(uvs[edge.a], uvs[edge.b], relation);

            return newVertex;
        }

        private float FindIntersectionPosOnSegment(float3 vertexA, float3 vertexB)
        {
            var vectorAc = planeCenter - vertexA;
            var vectorAb = vertexB - vertexA;

            var productAc = math.dot(planeNormal, vectorAc);
            var productAb = math.dot(planeNormal, vectorAb);

            var relation = productAc / productAb;

            return math.abs(relation);
        }
    }
}

