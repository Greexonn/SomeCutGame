using Cutting.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Cutting.Jobs
{
    [BurstCompile]
    public struct ReassignTrianglesJob : IJobParallelFor
    {
        [ReadOnly] public NativeHashMap<int, int> originalIndexesToLeft, originalIndexesToRight;
        [ReadOnly] public NativeArray<int> triangleIndexes;
        [ReadOnly] public NativeArray<Side> triangleTypes;

        [WriteOnly] public NativeQueue<int>.ParallelWriter 
            leftTriangleIndexes,
            rightTriangleIndexes,
            intersectingTriangleIndexes;

        public void Execute(int index)
        {
            var currentStartIndex = index * 3;

            var vertexA = triangleIndexes[currentStartIndex];
            var vertexB = triangleIndexes[currentStartIndex + 1]; 
            var vertexC = triangleIndexes[currentStartIndex + 2];

            switch (triangleTypes[index])
            {
                case Side.Left:
                {
                    //if left
                    leftTriangleIndexes.Enqueue(originalIndexesToLeft[vertexA]);
                    leftTriangleIndexes.Enqueue(originalIndexesToLeft[vertexB]);
                    leftTriangleIndexes.Enqueue(originalIndexesToLeft[vertexC]);
                    break;
                }
                case Side.Right:
                {
                    //if right
                    rightTriangleIndexes.Enqueue(originalIndexesToRight[vertexA]);
                    rightTriangleIndexes.Enqueue(originalIndexesToRight[vertexB]);
                    rightTriangleIndexes.Enqueue(originalIndexesToRight[vertexC]);
                    break;
                }
                default:
                {
                    //if intersecting
                    intersectingTriangleIndexes.Enqueue(vertexA);
                    intersectingTriangleIndexes.Enqueue(vertexB);
                    intersectingTriangleIndexes.Enqueue(vertexC);
                    break;
                }
            }
        }
    }
}
