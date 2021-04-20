using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Cutting.Jobs
{
    [BurstCompile]
    public struct TriangulateFrameJob : IJob
    {
        public NativeArray<int> edgesToLeft, edgesToRight;

        [ReadOnly] public NativeArray<int> sortedEdgeVertices;
        [ReadOnly] public NativeArray<float2> edgeVerticesOnPlane;

        [WriteOnly] public NativeList<int> cutSurfaceTriangles;

        private int _tipIndex;
        private int _maxIndex;

        public void Execute()
        {
            _tipIndex = 0;
            _maxIndex = edgeVerticesOnPlane.Length - 1;
            while (_tipIndex <= _maxIndex)
            {
                FindTriangle();
            }
        }

        private void FindTriangle()
        {
            NativeArray<int> localEdgesToLeft, localEdgesToRight;

            var tip = sortedEdgeVertices[_tipIndex];
            
            var left = edgesToLeft[tip];
            var right = edgesToRight[tip];
            if (left < 0 || right < 0 || left > _maxIndex || right > _maxIndex)
            {
                _tipIndex++;
                return;
            }

            //check sides
            if (edgeVerticesOnPlane[left].y < edgeVerticesOnPlane[right].y)
            {
                //swap
                var buff = left;
                left = right;
                right = buff;
                //swap
                localEdgesToLeft = edgesToRight;
                localEdgesToRight = edgesToLeft;
            }
            else
            {
                localEdgesToLeft = edgesToLeft;
                localEdgesToRight = edgesToRight;
            }

            //store triangle
            cutSurfaceTriangles.Add(tip);
            cutSurfaceTriangles.Add(left);
            cutSurfaceTriangles.Add(right);
            //delete old edges
            localEdgesToLeft[tip] = -1;
            localEdgesToRight[tip] = -1;
            localEdgesToRight[left] = -1;
            localEdgesToLeft[right] = -1;
            //add new edges
            localEdgesToLeft[right] = left;
            localEdgesToRight[left] = right;
            //increase tip index
            _tipIndex++;
        }

        private int FindInnerVertex(int aId, int bId, int cId, int boundIndex)
        {
            var a = edgeVerticesOnPlane[aId];
            var b = edgeVerticesOnPlane[bId];
            var c = edgeVerticesOnPlane[cId];

            for (var i = _tipIndex + 1; i < boundIndex; i++)
            {
                if (sortedEdgeVertices[i] == bId || sortedEdgeVertices[i] == cId) 
                    continue;
        
                var vert = edgeVerticesOnPlane[sortedEdgeVertices[i]];
                var edgeAb = (a.x - vert.x) * (b.y - a.y) - (b.x - a.x) * (a.y - vert.y);
                var edgeBC = (b.x - vert.x) * (c.y - b.y) - (c.x - b.x) * (b.y - vert.y);
                var edgeCa = (c.x - vert.x) * (a.y - c.y) - (a.x - c.x) * (c.y - vert.y);

                if (edgeAb <= 0 && edgeBC <= 0 && edgeCa <= 0)
                {
                    return sortedEdgeVertices[i];
                }

                if (edgeAb >= 0 && edgeBC >= 0 && edgeCa >= 0)
                {
                    return sortedEdgeVertices[i];
                }
            }

            return -1;
        }
    }
}
