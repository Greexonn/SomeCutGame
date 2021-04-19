using Cutting.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Cutting.Jobs
{
    [BurstCompile]
    public struct SetMeshDataAndPartIndexesParallelJob : IJobFor
    {
        [ReadOnly] public NativeArray<Side> vertexSides;
        
        [WriteOnly] public NativeArray<int> originalIndexToPart;

        public NativeArray<int> vertexCounts;
        
        public void Execute(int index)
        {
            switch (vertexSides[index])
            {
                case Side.Left:
                    originalIndexToPart[index] = vertexCounts[0]++;
                    break;
                case Side.Right:
                    originalIndexToPart[index] = vertexCounts[1]++;
                    break;
            }
        }
    }
}
