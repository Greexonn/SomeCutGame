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

    private NativeHashMap<int, int> _edgesLeft, _edgesRight, _edgesLeftDoubles, _edgesRightDoubles;

    private int _tipIndex = 0;

    private bool _onDoubles;

    void Start()
    {
        _renderer.positionCount = 3;

        _vertices = new NativeArray<float2>(_vertexTransforms.Count, Allocator.Persistent);
        _indexesSorted = new NativeList<int>(_vertexTransforms.Count, Allocator.Persistent);
        _indexesSorted.ResizeUninitialized(_vertices.Length);
        for (int i = 0; i < _vertexTransforms.Count; i++)
        {
            _vertices[i] = new float2(_vertexTransforms[i].position.x, _vertexTransforms[i].position.y);
        }

        _edgesLeft = new NativeHashMap<int, int>(_edgeIndexes.Count / 2, Allocator.Persistent);
        _edgesRight = new NativeHashMap<int, int>(_edgeIndexes.Count / 2, Allocator.Persistent);
        _edgesLeftDoubles = new NativeHashMap<int, int>(_edgeIndexes.Count / 2, Allocator.Persistent);
        _edgesRightDoubles = new NativeHashMap<int, int>(_edgeIndexes.Count / 2, Allocator.Persistent);
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
        if (_vertices.IsCreated)
        {
            _vertices.Dispose();
            _edgesLeft.Dispose();
            _edgesRight.Dispose();
            _edgesLeftDoubles.Dispose();
            _edgesRightDoubles.Dispose();
            _indexesSorted.Dispose();
        }
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
        while ((_indexesSorted.Length - _tipIndex) > 1)
        {
            GetTriangle(_indexesSorted[_tipIndex]);
            yield return _ret;
        }

        //dispose arrays
        _vertices.Dispose();
        _edgesLeft.Dispose();
        _edgesRight.Dispose();
        _edgesLeftDoubles.Dispose();
        _edgesRightDoubles.Dispose();
        _indexesSorted.Dispose();
    }

    public void GetTriangle(int earTip)
    {
        //try get two other vertices
        _onDoubles = false;
        int _vertLeft;
        int _vertRight;
        //try get doubled edges
        if (_edgesLeftDoubles.TryGetValue(earTip, out _vertLeft) && _edgesRightDoubles.TryGetValue(earTip, out _vertRight))
        {
            _onDoubles = true;
        }
        //try get common edges
        else if (!(_edgesLeft.TryGetValue(earTip, out _vertLeft) && _edgesRight.TryGetValue(earTip, out _vertRight)))
        {
            _tipIndex++;
            return;
        }
        if (_vertLeft == _vertRight)
        {
            _tipIndex++;
            return;
        }


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
                if (_onDoubles)
                {
                    _edgesLeftDoubles.Remove(earTip);
                    _edgesLeftDoubles.TryAdd(earTip, _vertRight);
                }
                else
                {
                    _edgesLeft.Remove(earTip);
                }
                //remove edges
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
                if (_onDoubles)
                {
                    _edgesRightDoubles.Remove(earTip);
                    _edgesRightDoubles.TryAdd(earTip, _vertRight);
                }
                else
                {
                    _edgesRight.Remove(earTip);
                }
                //remove edges
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
                _vertRight = _vertInner;
                FindTriangle(ref earTip, ref _vertLeft, ref _vertRight);
            }
        }
        else
        {
            if (_onDoubles)
            {
                _edgesLeftDoubles.Remove(earTip);
                _edgesRightDoubles.Remove(earTip);
            }
            else
            {
                //remove ear tip edges
                _edgesLeft.Remove(earTip);
                _edgesRight.Remove(earTip);
                //move ear tip to next vertex
                _tipIndex++;
            }
            //remove edges to ear tip
            _edgesLeft.Remove(_vertRight);
            _edgesRight.Remove(_vertLeft);
            //add new edge
            _edgesLeft.TryAdd(_vertRight, _vertLeft);
            _edgesRight.TryAdd(_vertLeft, _vertRight);
        }

        //debug draw triangle
        _renderer.SetPosition(0, _vertexTransforms[earTip].position);
        _renderer.SetPosition(1, _vertexTransforms[_vertLeft].position);
        _renderer.SetPosition(2, _vertexTransforms[_vertRight].position);
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

    public void FindTriangle(ref int earTip, ref int vertLeft, ref int vertRight)
    {
        //find farthes vertex
        int _boundIndex = _indexesSorted.IndexOf(vertLeft);
        int _boundIndex2 = _indexesSorted.IndexOf(vertRight);
        if (_boundIndex2 > _boundIndex)
        {
            _boundIndex = _boundIndex2;
        }

        int _vertInner = FindInnerVertex(earTip, vertLeft, vertRight, _boundIndex);

        //if intersection found cut upper triangle and add extended edges
        if (_vertInner != -1)
        {
            print(_vertInner);
            //look if vertex is already connected to other vertices (no double edges)
            if (_vertInner == _edgesLeft[vertLeft])
            {
                if (_onDoubles)
                {
                    _edgesLeftDoubles.Remove(earTip);
                    _edgesLeftDoubles.TryAdd(earTip, vertRight);
                }
                else
                {
                    _edgesLeft.Remove(earTip);
                }
                //remove edges
                _edgesRight.Remove(vertLeft);
                _edgesLeft.Remove(vertLeft);
                //add edges
                if (_edgesLeft[_vertInner] == vertLeft)
                {
                    _edgesLeft.Remove(_vertInner);
                    _edgesLeft.TryAdd(_vertInner, _edgesRight[_vertInner]);
                }
                _edgesRight.Remove(_vertInner);
                _edgesRight.TryAdd(_vertInner, earTip);
                _edgesLeft.TryAdd(earTip, _vertInner);
                //
                vertRight = _vertInner;
                return;
            }
            else if (_vertInner == _edgesRight[vertRight])
            {
                if (_onDoubles)
                {
                    _edgesRightDoubles.Remove(earTip);
                    _edgesRightDoubles.TryAdd(earTip, vertRight);
                }
                else
                {
                    _edgesRight.Remove(earTip);
                }
                //remove edges
                _edgesLeft.Remove(vertRight);
                _edgesRight.Remove(vertRight);
                //add edges
                if (_edgesRight[_vertInner] == vertRight)
                {
                    _edgesRight.Remove(_vertInner);
                    _edgesRight.TryAdd(_vertInner, _edgesLeft[_vertInner]);
                }
                _edgesLeft.Remove(_vertInner);
                _edgesLeft.TryAdd(_vertInner, earTip);
                _edgesRight.TryAdd(earTip, _vertInner);
                //
                vertLeft = _vertInner;
                return;
            }
            else //if vertex is not connected to other (create double edges)
            {
                vertRight = _vertInner;
                FindTriangle(ref earTip, ref vertLeft, ref vertRight);
                return;
            }
        }
        else
        {
            //add doubles
            _edgesLeftDoubles.TryAdd(vertRight, vertLeft);
            _edgesRightDoubles.TryAdd(vertRight, _edgesRight[vertRight]);
            //remove edges
            if (_onDoubles)
            {
                _edgesLeftDoubles.Remove(earTip);
                _edgesLeftDoubles.TryAdd(earTip, vertRight);
            }
            else
            {
                _edgesLeft.Remove(earTip);
                _edgesLeft.TryAdd(earTip, vertRight);
            }
            _edgesRight.Remove(vertLeft);
            _edgesRight.Remove(vertRight);
            //add edges
            _edgesRight.TryAdd(vertRight, earTip);
            _edgesRight.TryAdd(vertLeft, vertRight);
        }
    }
}
