using Cutting.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Cutting.Jobs
{
    [BurstCompile]
    public struct TranslateCoordinatesToPlaneParallelJob : IJobParallelFor
    {
        [ReadOnly] public float3 planeXAxis;
        [ReadOnly] public float3 planeYAxis;

        [ReadOnly] public NativeArray<VertexInfo> edgeVertices;
        [WriteOnly] public NativeArray<float2> edgeVerticesOnPlane;

        public void Execute(int index)
        {
            var x = math.dot(planeXAxis, edgeVertices[index].vertex);
            var y = math.dot(planeYAxis, edgeVertices[index].vertex);

            edgeVerticesOnPlane[index] = new float2(x, y);
        }
    }
}
