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
        public float3 planeXAxis;
        public float3 planeYAxis;

        [ReadOnly] public NativeArray<NewVertexInfo> edgeVertices;
        
        [WriteOnly] public NativeArray<float2> edgeVerticesOnPlane;

        public void Execute(int index)
        {
            var vertexPos = edgeVertices[index].vertex;
            
            var x = math.dot(planeXAxis, vertexPos);
            var y = math.dot(planeYAxis, vertexPos);

            edgeVerticesOnPlane[index] = new float2(x, y);
        }
    }
}
