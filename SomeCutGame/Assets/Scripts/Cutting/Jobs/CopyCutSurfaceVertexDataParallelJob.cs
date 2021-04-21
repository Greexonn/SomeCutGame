using Cutting.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Cutting.Jobs
{
    [BurstCompile]
    public struct CopyCutSurfaceVertexDataParallelJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<NewVertexInfo> edgeVertices;
        
        public float3 normal;
        public int verticesStartIndex;
        
        [WriteOnly, NativeDisableContainerSafetyRestriction] public NativeArray<float3> sideVertices, sideNormals;
        [WriteOnly, NativeDisableContainerSafetyRestriction] public NativeArray<float2> sideUVs;

        public void Execute(int index)
        {
            var writeIndex = index + verticesStartIndex;

            var vertexData = edgeVertices[index];

            sideVertices[writeIndex] = vertexData.vertex;
            sideNormals[writeIndex] = normal;
            sideUVs[writeIndex] = vertexData.uv;
        }
    }
}
