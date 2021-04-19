using Cutting.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Cutting.Jobs
{
    [BurstCompile]
    public struct AddEdgeTrianglesJob : IJob
    {
        [WriteOnly] public NativeList<int> sideTriangles;

        [ReadOnly] public NativeArray<int> originalIndexToPart;
        [ReadOnly] public NativeHashMap<Edge, int> edgeToSideVertex;

        public NativeQueue<HalfNewTriangle> halfNewTriangles;

        private int _a, _b;

        public void Execute()
        {
            while (halfNewTriangles.Count > 0)
            {
                _a = _b = -1;

                var hnTriangle = halfNewTriangles.Dequeue();

                sideTriangles.Add(originalIndexToPart[hnTriangle.a]);
                if (hnTriangle.b != -1)
                {
                    sideTriangles.Add(originalIndexToPart[hnTriangle.b]);
                }
                _a = edgeToSideVertex[hnTriangle.c];
                sideTriangles.Add(_a);
                if (hnTriangle.d.Empty()) 
                    continue;
        
                _b = edgeToSideVertex[hnTriangle.d];
                sideTriangles.Add(_b);
            }
        }
    }
}
