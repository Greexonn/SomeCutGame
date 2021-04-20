using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Cutting.Jobs
{
    [BurstCompile]
    public struct SortEdgeVerticesJob : IJobFor
    {
        public NativeList<float2> edgeVerticesOnPlane;
        public NativeList<int> sortedEdgeVertices;

        public NativeHashMap<int, int> edgesToLeft, edgesToRight;

        private const float Difference = 0.000001f;

        public void Execute(int index)
        {
            var vertex = edgeVerticesOnPlane[index];
            var place = 0;
            var doubles = 1;
            for (var j = 0; j < edgeVerticesOnPlane.Length; j++)
            {
                if (index == j)
                    continue;
        
                var vertexB = edgeVerticesOnPlane[j];

                if (vertex.x > vertexB.x)
                {
                    place++;
                }
                else if (Math.Abs(vertex.x - vertexB.x) < Difference)
                {
                    if (vertex.y < vertexB.y)
                    {
                        place++;
                    }
                    else
                    {
                        if (Math.Abs(vertex.y - vertexB.y) > Difference) 
                            continue;
                        
                        //if we found doubled vertex we connect original vertex to doubled connected vertex
                        //check free side ou current vertex
                        if (!edgesToLeft.ContainsKey(index)) //if left free
                        {
                            ConnectVertex(index, j, edgesToLeft);
                        }

                        if (!edgesToRight.ContainsKey(index)) //if right free
                        {
                            ConnectVertex(index, j, edgesToRight);
                        }

                        //double found
                        doubles++;
                    }
                }
            }
            //store ids in place
            for (var p = 0; p < doubles; p++)
            {
                sortedEdgeVertices[place + p] = index;
            }
        }

        private void ConnectVertex(int vertexID, int doubleID, NativeHashMap<int, int> side)
        {
            if (edgesToLeft.TryGetValue(doubleID, out var connected))
            {
                side.TryAdd(vertexID, connected);
                //remove double connection
                edgesToLeft.Remove(doubleID);
            }
            else if (edgesToRight.TryGetValue(doubleID, out connected))
            {
                side.TryAdd(vertexID, connected);
                //remove double connection
                edgesToRight.Remove(doubleID);
            }
        }
    }
}
