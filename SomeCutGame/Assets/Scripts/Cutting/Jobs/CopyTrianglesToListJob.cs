using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Cutting.Jobs
{
    [BurstCompile]
    public struct CopyTrianglesToListJob : IJob
    {
        public NativeQueue<int> triangleIndexes;
        [WriteOnly] public NativeList<int> listTriangles;

        public void Execute()
        {
            while (triangleIndexes.Count > 0)
            {
                listTriangles.AddNoResize(triangleIndexes.Dequeue());
            }
        }
    }
}
