using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Cutting.Jobs
{
    [BurstCompile]
    public struct CheckTrianglesParallelJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> sideIDs;
        [ReadOnly] public NativeArray<int> triangleIndexes;
        [WriteOnly] public NativeArray<int> triangleTypes;

        public void Execute(int index)
        {
            var currentStartIndex = index * 3;

            var vertexA = triangleIndexes[currentStartIndex];
            var vertexB = triangleIndexes[currentStartIndex + 1];
            var vertexC = triangleIndexes[currentStartIndex + 2];

            triangleTypes[index] = GetTriangleType(vertexA, vertexB, vertexC);
        }

        private int GetTriangleType(int vertexA, int vertexB, int vertexC)
        {
            return sideIDs[vertexA] + sideIDs[vertexB] + sideIDs[vertexC];
        }
    }
}
