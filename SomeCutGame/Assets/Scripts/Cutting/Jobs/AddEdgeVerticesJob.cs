using Cutting.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Cutting.Jobs
{
    [BurstCompile]
    public struct AddEdgeVerticesJob : IJob
    {
        [ReadOnly] public NativeArray<Edge> edges;
        [ReadOnly] public NativeHashMap<Edge, VertexInfo> edgesToVertices;

        [ReadOnly] public int startVertexCount;

        [WriteOnly] public NativeList<float3> sideVertices, sideNormals;
        [WriteOnly] public NativeList<float2> sideUVs;

        [WriteOnly] public NativeHashMap<Edge, int> edgeVerticesToSide;

        public void Execute()
        {
            for (var i = 0; i < edges.Length; i++)
            {
                //add to hash-map
                edgeVerticesToSide.TryAdd(edges[i], startVertexCount + i);

                //add vertex info
                var vertex = edgesToVertices[edges[i]];
                sideVertices.Add(vertex.vertex);
                sideNormals.Add(vertex.normal);
                sideUVs.Add(vertex.uv);
            }
        }
    }
}
