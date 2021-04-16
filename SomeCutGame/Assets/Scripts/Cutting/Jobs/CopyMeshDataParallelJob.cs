using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Cutting.Jobs
{
    [BurstCompile]
    public struct CopyMeshDataParallelJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> vertices;
        [ReadOnly] public NativeArray<float3> normals;
        [ReadOnly] public NativeArray<float2> uvs;

        [ReadOnly] public NativeHashMap<int, int> originalIndexToRight, originalIndexToLeft;

        [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<float3> rightVertices, leftVertices;
        [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<float3> rightNormals, leftNormals;
        [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<float2> rightUVs, leftUVs;
        
        public void Execute(int index)
        {
            if (originalIndexToRight.ContainsKey(index))
            {
                var id = originalIndexToRight[index];

                rightVertices[id] = vertices[index];
                rightNormals[id] = normals[index];
                rightUVs[id] = uvs[index];
            }
            else
            {
                var id = originalIndexToLeft[index];

                leftVertices[id] = vertices[index];
                leftNormals[id] = normals[index];
                leftUVs[id] = uvs[index];
            }
        }
    }
}
