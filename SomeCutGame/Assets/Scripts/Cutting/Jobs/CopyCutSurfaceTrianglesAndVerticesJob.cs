using Cutting.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Cutting.Jobs
{
    [BurstCompile]
    public struct CopyCutSurfaceTrianglesAndVerticesJob : IJob
    {
        [ReadOnly] public NativeArray<NewVertexInfo> edgeVertices;
        [ReadOnly] public NativeArray<int> cutSurfaceTriangles;

        [WriteOnly] public NativeList<float3> sideVertices, sideNormals;
        [WriteOnly] public NativeList<float2> sideUVs;
        [WriteOnly] public NativeList<int> sideTriangles;

        [ReadOnly] public float3 normal;
        [ReadOnly] public bool inverseOrder;

        [ReadOnly] public int verticesStartCount;

        public void Execute()
        {
            //copy vertices
            for (var i = 0; i < edgeVertices.Length; i++)
            {
                sideVertices.Add(edgeVertices[i].vertex);
                sideNormals.Add(normal);
                sideUVs.Add(edgeVertices[i].uv);
            }
            //copy triangles
            if (inverseOrder)
            {
                for (var i = cutSurfaceTriangles.Length - 1; i >= 0; i--)
                {
                    sideTriangles.Add(cutSurfaceTriangles[i] + verticesStartCount);
                }
            }
            else
            {
                foreach (var triangle in cutSurfaceTriangles)
                {
                    sideTriangles.Add(triangle + verticesStartCount);
                }
            }
        }
    }
}
