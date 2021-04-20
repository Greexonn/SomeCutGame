using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Cutting.Jobs
{
    [BurstCompile]
    public struct FindVertexOnPlaneDoublesParallelJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float2> edgeVerticesOnPlane;

        [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<int> vertexDoubleIds;
        
        private const float Difference = 0.000001f;

        public void Execute(int index)
        {
            index = edgeVerticesOnPlane.Length - index - 1;
            
            var vertex = edgeVerticesOnPlane[index];

            for (var j = 0; j < index; j++)
            {
                var vertexB = edgeVerticesOnPlane[j];

                var isDoubleX = Math.Abs(vertex.x - vertexB.x) < Difference;
                var isDoubleY = Math.Abs(vertex.y - vertexB.y) < Difference;
                
                if (!(isDoubleX && isDoubleY))
                    continue;
                
                vertexDoubleIds[index] = j;
                return;
            }
            
            vertexDoubleIds[index] = -1;
        }
    }
}
