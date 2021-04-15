using Cutting.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Cutting.Jobs
{
    [BurstCompile]
    public struct SetMehsDataAndHashMapsJob : IJob
    {
        public NativeQueue<VertexInfo> sideData;
        [WriteOnly] public NativeHashMap<int, int> originalIndexesToSide;
        [WriteOnly] public NativeList<float3> verticesSide, normalsSide;
        [WriteOnly] public NativeList<float2> uvsSide;

        public void Execute()
        {
            var counter = 0;
            while (sideData.Count > 0)
            {
                var data = sideData.Dequeue();
                originalIndexesToSide.TryAdd(data.originalIndex, counter++);
                verticesSide.Add(data.vertex);
                normalsSide.Add(data.normal);
                uvsSide.Add(data.uv);
            }
        }
    }
}
