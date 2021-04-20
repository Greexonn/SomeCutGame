using Cutting.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Cutting.Jobs
{
    [BurstCompile]
    public struct AddEdgeVerticesParallelJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Edge> edges;
        [ReadOnly] public NativeHashMap<Edge, NewVertexInfo> edgesToVertices;

        public int startVertexCount;
        
        [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<float3> sideVertices, sideNormals;
        [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<float2> sideUVs;

        [WriteOnly] public NativeHashMap<Edge, int>.ParallelWriter edgeToSideVertex;
        
        public void Execute(int index)
        {
            var id = startVertexCount + index;
            var edge = edges[index];
            
            //add to hash-map
            edgeToSideVertex.TryAdd(edge, id);

            //add vertex info
            var vertex = edgesToVertices[edge];
            sideVertices[id] = vertex.vertex;
            sideNormals[id] = vertex.normal;
            sideUVs[id] = vertex.uv;
        }
    }
}
