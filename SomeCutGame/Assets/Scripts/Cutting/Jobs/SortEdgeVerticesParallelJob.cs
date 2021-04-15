using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Cutting.Jobs
{
    [BurstCompile]
    public struct SortEdgeVerticesParallelJob : IJob
    {
        public NativeList<float2> edgeVerticesOnPlane;
        public NativeList<int> sortedEdgeVertices;

        public NativeHashMap<int, int> edgesToLeft, edgesToRight;

        private const float Difference = 0.000001f;

        public void Execute()
        {
            for (var i = 0; i < edgeVerticesOnPlane.Length; i++)
            {
                var vertex = edgeVerticesOnPlane[i];
                var place = 0;
                var doubles = 1;
                for (var j = 0; j < edgeVerticesOnPlane.Length; j++)
                {
                    if (i == j) 
                        continue;
            
                    var vertexB = edgeVerticesOnPlane[j];

                    if (vertex.x > vertexB.x)
                    {
                        place++;
                    }
                    else if (Math.Abs(vertex.x - vertexB.x) < Difference)
                    {
                        switch (vertex.y < vertexB.y)
                        {
                            case true:
                                place++;
                                break;
                            default:
                            {
                                if (Math.Abs(vertex.y - vertexB.y) < Difference)
                                {
                                    //if we found doubled vertex we connect original vertex to doubled connected vertex
                                    //check free side ou current vertex
                                    if (!edgesToLeft.ContainsKey(i)) //if left free
                                    {
                                        ConnectVertex(i, j, edgesToLeft);
                                    }
                                    if (!edgesToRight.ContainsKey(i)) //if right free
                                    {
                                        ConnectVertex(i, j, edgesToRight);
                                    }
                                    //double found
                                    doubles++;
                                }
                                break;
                            }
                        }
                    }
                }
                //store ids in place
                for (var p = 0; p < doubles; p++)
                {
                    sortedEdgeVertices[place + p] = i;
                }
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
