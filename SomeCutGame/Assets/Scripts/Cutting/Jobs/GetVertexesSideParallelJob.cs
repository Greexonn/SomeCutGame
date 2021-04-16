using Cutting.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Cutting.Jobs
{
    [BurstCompile]
    public struct GetVerticesSideParallelJob : IJobParallelFor
    {
        public float3 planeCenter;
        public float3 planeNormal;
        
        [ReadOnly] public NativeArray<float3> vertices;
        // [ReadOnly] public NativeArray<float3> normals;
        // [ReadOnly] public NativeArray<float2> uvs;
        [WriteOnly] public NativeArray<Side> sideIds;
        // [WriteOnly] public NativeQueue<VertexInfo>.ParallelWriter leftSide, rightSide;
        
        public void Execute(int index)
        {
            var localPos = vertices[index] - planeCenter;
            var dotProduct = math.dot(planeNormal, localPos);

            var side = dotProduct < 0 ? Side.Right : Side.Left;
            // if (dotProduct < 0)
            // {
            //     side = Side.Right;
            //
            //     rightSide.Enqueue(new VertexInfo
            //     {
            //         originalIndex = index,
            //         // vertex = vertices[index],
            //         // normal = normals[index],
            //         // uv = uvs[index]
            //     });
            // }
            // else
            // {
            //     leftSide.Enqueue(new VertexInfo
            //     {
            //         originalIndex = index,
            //         // vertex = vertices[index],
            //         // normal = normals[index],
            //         // uv = uvs[index]
            //     });
            // }

            sideIds[index] = side;
        }
    }
}
