using Cutting.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Cutting.Jobs
{
    [BurstCompile]
    public struct SetMeshDataAndHashMapsParallelJob : IJobFor
    {
        [ReadOnly] public NativeArray<Side> vertexSides;
        
        [WriteOnly] public NativeHashMap<int, int> originalIndexToRight, originalIndexToLeft;

        private int _leftCounter;
        private int _rightCounter;
        
        public void Execute(int index)
        {
            switch (vertexSides[index])
            {
                case Side.Left:
                    originalIndexToLeft.TryAdd(index, _leftCounter++);
                    break;
                case Side.Right:
                    originalIndexToRight.TryAdd(index, _rightCounter++);
                    break;
            }
        }
    }
}
