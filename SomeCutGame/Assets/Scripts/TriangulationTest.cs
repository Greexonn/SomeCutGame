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
        _triangleRenderer.positionCount = 4;

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
        while (_tipIndex < _indexesSorted.Length)
        {
            TryGetTriangle(_indexesSorted[_tipIndex]);
            yield return _ret;
        }

        //debug highlight vertices
        // for (int i = 0; i < _indexesSorted.Length; i++)
        // {
        //     _renderer.SetPosition(i, _vertexTransforms[_indexesSorted[i]].position);

        //     yield return _ret;
        // }

        yield return _ret;
        //dispose arrays
        _vertices.Dispose();
        _edgesLeft.Dispose();
        _edgesRight.Dispose();
        _indexesSorted.Dispose();
    }

    public void TryGetTriangle(int earTip)
    {
        int _vertLeft = _edgesLeft[earTip];
        int _vertRight = _edgesRight[earTip];

        //debug draw triangle
        _triangleRenderer.SetPosition(0, _vertexTransforms[earTip].position);
        _triangleRenderer.SetPosition(1, _vertexTransforms[_vertLeft].position);
        _triangleRenderer.SetPosition(2, _vertexTransforms[_vertRight].position);
        _triangleRenderer.SetPosition(3, _vertexTransforms[earTip].position);

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
}
