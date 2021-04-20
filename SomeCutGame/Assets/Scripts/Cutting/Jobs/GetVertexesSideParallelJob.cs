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
        [WriteOnly] public NativeArray<Side> sideIds;
        
        public void Execute(int index)
        {
            var localPos = vertices[index] - planeCenter;
            var dotProduct = math.dot(planeNormal, localPos);

            var side = dotProduct < 0 ? Side.Right : Side.Left;
            sideIds[index] = side;
        }
    }
}
