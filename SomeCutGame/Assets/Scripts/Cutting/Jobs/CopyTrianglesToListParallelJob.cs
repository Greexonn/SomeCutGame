using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Cutting.Jobs
{
    [BurstCompile]
    public struct CopyTrianglesToListParallelJob : IJobParallelFor
    {
        [ReadOnly, DeallocateOnJobCompletion] public NativeArray<int> triangleIndexes;
        
        [WriteOnly] public NativeArray<int> targetBuffer;
        
        public void Execute(int index)
        {
            targetBuffer[index] = triangleIndexes[index];
        }
    }
}
