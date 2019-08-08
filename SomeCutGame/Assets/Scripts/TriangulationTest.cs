using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;

public class TriangulationTest : MonoBehaviour
{
    [SerializeField] private LineRenderer _renderer;
    [SerializeField] private LineRenderer _triangleRenderer;

    [SerializeField] private List<Transform> _vertexTransforms;
    [SerializeField] private List<int> _edgeIndexes;

    private NativeArray<float2> _vertices;
    private NativeList<int> _indexesSorted;

    private NativeHashMap<int, int> _edgesLeft, _edgesRight;

    private int _tipIndex = 0;

    void Start()
    {
        _renderer.positionCount = _vertexTransforms.Count + 1;

        _vertices = new NativeArray<float2>(_vertexTransforms.Count, Allocator.Persistent);
        _indexesSorted = new NativeList<int>(_vertexTransforms.Count, Allocator.Persistent);
        _indexesSorted.ResizeUninitialized(_vertices.Length);
        for (int i = 0; i < _vertexTransforms.Count; i++)
        {
            _vertices[i] = new float2(_vertexTransforms[i].position.x, _vertexTransforms[i].position.y);
        }

        _edgesLeft = new NativeHashMap<int, int>(_edgeIndexes.Count / 2, Allocator.Persistent);
        _edgesRight = new NativeHashMap<int, int>(_edgeIndexes.Count / 2, Allocator.Persistent);
        for (int i = 0; i < (_edgeIndexes.Count); i += 2)
        {
            _edgesLeft.TryAdd(_edgeIndexes[i], _edgeIndexes[i + 1]);
            _edgesRight.TryAdd(_edgeIndexes[i + 1], _edgeIndexes[i]);
        }

        StartCoroutine(Triangulate());
    }

    void OnDestroy()
    {
        //dispose arrays
        _vertices.Dispose();
        _edgesLeft.Dispose();
        _edgesRight.Dispose();
        _indexesSorted.Dispose();
    }

    private IEnumerator Triangulate()
    {
        var _ret = new WaitForSeconds(1f);

        //sort array
        for (int i = 0; i < _vertices.Length; i++)
        {
            int _place = 0;

            for (int j = 0; j < _vertices.Length; j++)
            {
                if (_vertices[i].x > _vertices[j].x)
                {
                    _place++;
                }
                else if (_vertices[i].x == _vertices[j].x)
                {
                    if (_vertices[i].y < _vertices[j].y)
                    {
                        _place++;
                    }
                }
            }

            _indexesSorted[_place] = i;
        }

        //triangulate
        while ((_indexesSorted.Length - _tipIndex) > 2)
        {
            GetTriangle(_indexesSorted[_tipIndex]);
            yield return _ret;
        }

        //dispose arrays
        _vertices.Dispose();
        _edgesLeft.Dispose();
        _edgesRight.Dispose();
        _indexesSorted.Dispose();
    }

    public void GetTriangle(int earTip)
    {
        //get two other vertices
        int _vertLeft = _edgesLeft[earTip];
        int _vertRight = _edgesRight[earTip];

        //find farthes vertex
        int _boundIndex = _indexesSorted.IndexOf(_vertLeft);
        int _boundIndex2 = _indexesSorted.IndexOf(_vertRight);
        if (_boundIndex2 > _boundIndex)
        {
            _boundIndex = _boundIndex2;
        }

        int _vertInner = FindInnerVertex(earTip, _vertLeft, _vertRight, _boundIndex);

        //if intersection found cut upper triangle and add extended edges
        if (_vertInner != -1)
        {
            print(_vertInner);
            //look if vertex is already connected to other vertices (no double edges)
            if (_vertInner == _edgesLeft[_vertLeft])
            {
                //remove edges
                _edgesLeft.Remove(earTip);
                _edgesRight.Remove(_vertLeft);
                _edgesLeft.Remove(_vertLeft);
                //add edges
                if (_edgesLeft[_vertInner] == _vertLeft)
                {
                    _edgesLeft.Remove(_vertInner);
                    _edgesLeft.TryAdd(_vertInner, _edgesRight[_vertInner]);
                }
                _edgesRight.Remove(_vertInner);
                _edgesRight.TryAdd(_vertInner, earTip);
                _edgesLeft.TryAdd(earTip, _vertInner);
                //
                _vertRight = _vertInner;
            }
            else if (_vertInner == _edgesRight[_vertRight])
            {
                //remove edges
                _edgesRight.Remove(earTip);
                _edgesLeft.Remove(_vertRight);
                _edgesRight.Remove(_vertRight);
                //add edges
                if (_edgesRight[_vertInner] == _vertRight)
                {
                    _edgesRight.Remove(_vertInner);
                    _edgesRight.TryAdd(_vertInner, _edgesLeft[_vertInner]);
                }
                _edgesLeft.Remove(_vertInner);
                _edgesLeft.TryAdd(_vertInner, earTip);
                _edgesRight.TryAdd(earTip, _vertInner);
                //
                _vertLeft = _vertInner;
            }
            else //if vertex is not connected to other (create double edges)
            {
                
                return;
            }
        }
        else
        {
            //remove ear tip edges
            _edgesLeft.Remove(earTip);
            _edgesRight.Remove(earTip);
            //remove edges to ear tip
            _edgesLeft.Remove(_vertRight);
            _edgesRight.Remove(_vertLeft);
            //add new edge
            _edgesLeft.TryAdd(_vertRight, _vertLeft);
            _edgesRight.TryAdd(_vertLeft, _vertRight);
            //move ear tip to next vertex
            _tipIndex++;
        }

        //debug draw triangle
        _renderer.SetPosition(0, _vertexTransforms[earTip].position);
        _renderer.SetPosition(1, _vertexTransforms[_vertLeft].position);
        _renderer.SetPosition(2, _vertexTransforms[_vertRight].position);
        _renderer.SetPosition(3, _vertexTransforms[earTip].position);
    }

    public int FindInnerVertex(int a, int b, int c, int boundIndex)
    {
        float2 _a = _vertices[a];
        float2 _b = _vertices[b];
        float2 _c = _vertices[c];

        for (int i = (_tipIndex + 1); i < boundIndex; i++)
        {
            if ((_indexesSorted[i] != b) && (_indexesSorted[i] != c))
            {
                float2 _vert = _vertices[_indexesSorted[i]];
                float _edgeAB = (_a.x - _vert.x) * (_b.y - _a.y) - (_b.x - _a.x) * (_a.y - _vert.y);
                float _edgeBC = (_b.x - _vert.x) * (_c.y - _b.y) - (_c.x - _b.x) * (_b.y - _vert.y);
                float _edgeCA = (_c.x - _vert.x) * (_a.y - _c.y) - (_a.x - _c.x) * (_c.y - _vert.y);

                if ((_edgeAB <= 0) && (_edgeBC <= 0) && (_edgeCA <= 0))
                {
                    return _indexesSorted[i];
                }
                else if ((_edgeAB >= 0) && (_edgeBC >= 0) && (_edgeCA >= 0))
                {
                    return _indexesSorted[i];
                }
            }
        }

        return -1;
    }

    public void FindTriangle()
    {

    }
}
