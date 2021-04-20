using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Cutting.Jobs
{
    /// <summary>
    /// input array should be cleared from doubles
    /// </summary>
    [BurstCompile]
    public struct SortEdgeVerticesParallelJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float2> edgeVerticesOnPlane;
        
        [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<int> sortedEdgeVertices;
        
        private const float Difference = 0.000001f;
        
        public void Execute(int index)
        {
            if (index >= edgeVerticesOnPlane.Length)
                return;
            
            var vertex = edgeVerticesOnPlane[index];
            var place = 0;

            for (var j = 0; j < edgeVerticesOnPlane.Length; j++)
            {
                if (index == j)
                    continue;
                
                var vertexB = edgeVerticesOnPlane[j];

                var biggerX = vertex.x > vertexB.x;
                var smallerY = vertex.y < vertexB.y;

                if (biggerX)
                {
                    place++;
                }
                else if (Math.Abs(vertex.x - vertexB.x) < Difference)
                {
                    if (smallerY)
                        place++;
                }
            }
            
            sortedEdgeVertices[place] = index;
        }
    }
}
