using Cutting.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Cutting.Jobs
{
    [BurstCompile]
    public struct SetPartIndexesParallelJob : IJobFor
    {
        [ReadOnly] public NativeArray<Side> vertexSides;
        
        [WriteOnly] public NativeArray<int> originalIndexToPart;

        public NativeArray<int> vertexCounts;
        
        public void Execute(int index)
        {
            var counterId = (int) vertexSides[index] - 1;
            
            originalIndexToPart[index] = vertexCounts[counterId]++;
        }
    }
}
