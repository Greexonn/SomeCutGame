using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Cutting.Jobs
{
    [BurstCompile]
    public struct CopyCutSurfaceTrianglesParallelJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> cutSurfaceTriangles;

        public int vertexStartIndex;
        public int trianglesStartIndex;
        public int trianglesReadOffset;
        
        [WriteOnly, NativeDisableContainerSafetyRestriction] public NativeArray<int> sideTriangles;
        
        public void Execute(int index)
        {
            var readIndex = Math.Abs(trianglesReadOffset - index);
            var writeIndex = index + trianglesStartIndex;

            sideTriangles[writeIndex] = cutSurfaceTriangles[readIndex] + vertexStartIndex;
        }
    }
}
