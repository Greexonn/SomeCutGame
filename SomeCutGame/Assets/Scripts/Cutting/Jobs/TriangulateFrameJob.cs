using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Cutting.Jobs
{
    [BurstCompile]
    public struct TriangulateFrameJob : IJob
    {
        public NativeHashMap<int, int> edgesToLeft, edgesToRight;

        [ReadOnly] public NativeArray<int> sortedEdgeVertices;
        [ReadOnly] public NativeArray<float2> edgeVerticesOnPlane;

        [WriteOnly] public NativeList<int> cutSurfaceTriangles;

        private int _tipIndex;

        public void Execute()
        {
            _tipIndex = 0;
            while (_tipIndex < (sortedEdgeVertices.Length - 1))
            {
                FindTriangle();
            }
        }

        private void FindTriangle()
        {
            //
            NativeHashMap<int, int> _edgesToLeft, _edgesToRight;

            var tip = sortedEdgeVertices[_tipIndex];

            if (!edgesToLeft.TryGetValue(tip, out var left) || !edgesToRight.TryGetValue(tip, out var right))
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
                _edgesToLeft = edgesToRight;
                _edgesToRight = edgesToLeft;
            }
            else
            {
                _edgesToLeft = edgesToLeft;
                _edgesToRight = edgesToRight;
            }

            //store triangle
            cutSurfaceTriangles.Add(tip);
            cutSurfaceTriangles.Add(left);
            cutSurfaceTriangles.Add(right);
            //delete old edges
            _edgesToLeft.Remove(tip);
            _edgesToRight.Remove(tip);
            _edgesToRight.Remove(left);
            _edgesToLeft.Remove(right);
            //add new edges
            _edgesToLeft.TryAdd(right, left);
            _edgesToRight.TryAdd(left, right);
            //increase tip index
            _tipIndex++;
        }

        private int FindInnerVertex(int aId, int bId, int cId, int boundIndex)
        {
            var a = edgeVerticesOnPlane[aId];
            var b = edgeVerticesOnPlane[bId];
            var c = edgeVerticesOnPlane[cId];

            for (var i = (_tipIndex + 1); i < boundIndex; i++)
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
