using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Cutting.Jobs
{
    [BurstCompile]
    public struct RemoveDoubleVerticesJob : IJobFor
    {
        [ReadOnly] public NativeArray<int> vertexDoubleIds;

        public NativeList<float2> edgeVertices;
        public NativeArray<int> edgesToLeft, edgesToRight;
        
        public void Execute(int index)
        {
            index = vertexDoubleIds.Length - index - 1;

            var originalId = vertexDoubleIds[index];
            var doubleId = index;
            
            if (originalId < 0)
                return;
            
            edgeVertices.RemoveAtSwapBack(doubleId);
            
            if (edgesToLeft[originalId] < 0) //if left free
            {
                ConnectLeft(originalId, doubleId);
            }
            if (edgesToRight[originalId] < 0) //if right free
            {
                ConnectRight(originalId, doubleId);
            }
        }
        
        private void ConnectLeft(int vertexId, int doubleId)
        {
            edgesToLeft[vertexId] = edgesToLeft[doubleId];
        }

        private void ConnectRight(int vertexId, int doubleId)
        {
            edgesToRight[vertexId] = edgesToRight[doubleId];
        }
    }
}
