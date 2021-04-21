using Cutting.Data;
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

        public NativeList<float2> edgeVerticesOnPlane;
        public NativeList<NewVertexInfo> edgeVertices;
        public NativeArray<int> edgesToLeft, edgesToRight;
        
        public void Execute(int index)
        {
            index = vertexDoubleIds.Length - index - 1;

            var originalId = vertexDoubleIds[index];
            var doubleId = index;
            
            if (originalId < 0)
                return;
            
            edgeVerticesOnPlane.RemoveAtSwapBack(doubleId);
            edgeVertices.RemoveAtSwapBack(doubleId);
            var swapId = edgeVerticesOnPlane.Length;
            
            if (edgesToLeft[originalId] < 0) //if left free
            {
                ConnectLeft(originalId, doubleId);
            }
            if (edgesToRight[originalId] < 0) //if right free
            {
                ConnectRight(originalId, doubleId);
            }
            
            PatchSwapped(doubleId, swapId);
        }
        
        private void ConnectLeft(int vertexId, int doubleId)
        {
            var connected = edgesToLeft[doubleId];
            edgesToLeft[vertexId] = connected;
            edgesToRight[connected] = vertexId;
        }

        private void ConnectRight(int vertexId, int doubleId)
        {
            var connected = edgesToRight[doubleId];
            edgesToRight[vertexId] = connected;
            edgesToLeft[connected] = vertexId;
        }

        private void PatchSwapped(int doubleId, int swapId)
        {
            if (doubleId == swapId)
                return;
            
            var left = edgesToLeft[swapId];
            if (left > 0)
            {
                edgesToRight[left] = doubleId;
                edgesToLeft[doubleId] = left;
            }

            var right = edgesToRight[swapId];
            if (right > 0)
            {
                edgesToLeft[right] = doubleId;
                edgesToRight[doubleId] = right;
            }
        }
    }
}
