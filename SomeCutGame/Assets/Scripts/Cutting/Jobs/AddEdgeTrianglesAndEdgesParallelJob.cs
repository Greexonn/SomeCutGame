using Cutting.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Cutting.Jobs
{
    [BurstCompile]
    public struct AddEdgeTrianglesAndEdgesParallelJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> originalIndexToPart;
        [ReadOnly] public NativeHashMap<Edge, int> edgeToSideVertex;
        [ReadOnly, DeallocateOnJobCompletion] public NativeArray<HalfNewTriangle> halfNewTriangles;
        
        public int previousVertexCount;
        public int startTrianglesIndex;
        
        [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<int> sideTriangles;
        [WriteOnly] public NativeHashMap<int, int>.ParallelWriter edgesToLeft, edgesToRight;

        private int _a, _b;
        
        public void Execute(int index)
        {
            var hnTriangle = halfNewTriangles[index];

            var triangleId = startTrianglesIndex + index * 3;

            sideTriangles[triangleId++] = originalIndexToPart[hnTriangle.a];
            if (hnTriangle.b != -1)
            {
                sideTriangles[triangleId++] = originalIndexToPart[hnTriangle.b];
            }
            _a = edgeToSideVertex[hnTriangle.c];
            sideTriangles[triangleId++] = edgeToSideVertex[hnTriangle.c];
            if (hnTriangle.d.Empty()) 
                return;
                
            _b = edgeToSideVertex[hnTriangle.d];
            sideTriangles[triangleId] = _b;

            // if we have 2 new vertices we add them to edges hash-maps
            _a -= previousVertexCount;
            _b -= previousVertexCount;
            edgesToLeft.TryAdd(_a, _b);
            edgesToRight.TryAdd(_b, _a);
        }
    }
}
