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
        [ReadOnly] public NativeArray<int> triangleTypes;

        [WriteOnly] public NativeQueue<int>.ParallelWriter leftTriangleIndexes, rightTriangleIndexes, intersectingTriangleIndexes;

        public void Execute(int index)
        {
            var currentStatrtIndex = index * 3;

            var vertexA = triangleIndexes[currentStatrtIndex];
            var vertexB = triangleIndexes[currentStatrtIndex + 1]; 
            var vertexC = triangleIndexes[currentStatrtIndex + 2];

            switch (triangleTypes[index])
            {
                case 3:
                {
                    //if left
                    leftTriangleIndexes.Enqueue(originalIndexesToLeft[vertexA]);
                    leftTriangleIndexes.Enqueue(originalIndexesToLeft[vertexB]);
                    leftTriangleIndexes.Enqueue(originalIndexesToLeft[vertexC]);
                    break;
                }
                case -3:
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
