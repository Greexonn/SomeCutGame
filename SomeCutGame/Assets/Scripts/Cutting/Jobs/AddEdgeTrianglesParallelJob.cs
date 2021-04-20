using Cutting.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Cutting.Jobs
{
    [BurstCompile]
    public struct AddEdgeTrianglesParallelJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> originalIndexToPart;
        [ReadOnly] public NativeHashMap<Edge, int> edgeToSideVertex;
        [ReadOnly, DeallocateOnJobCompletion] public NativeArray<HalfNewTriangle> halfNewTriangles;
        
        public int startTrianglesIndex;
        
        [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<int> sideTriangles;
        
        public void Execute(int index)
        {
            var hnTriangle = halfNewTriangles[index];
            
            var triangleId = startTrianglesIndex + index * 3;

            sideTriangles[triangleId++] = originalIndexToPart[hnTriangle.a];
            if (hnTriangle.b != -1)
            {
                sideTriangles[triangleId++] = originalIndexToPart[hnTriangle.b];
            }
            sideTriangles[triangleId++] = edgeToSideVertex[hnTriangle.c];
            if (hnTriangle.d.Empty()) 
                return;
        
            sideTriangles[triangleId] = edgeToSideVertex[hnTriangle.d];
        }
    }
}
