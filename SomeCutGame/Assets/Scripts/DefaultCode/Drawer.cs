using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Drawer : MonoBehaviour
{
    public int maxCount;

    public int currentVertex;

    public int layer;
    public MeshFilter _mesh;

    private List<Vector3> _vertices = new List<Vector3>();
    private List<int> _triangles = new List<int>();
    private LineRenderer _line;

    void Start()
    {
        _line = GetComponent<LineRenderer>();
    }

    void Update()
    {
        if (_mesh != null)
        {
            if (transform.parent != _mesh.transform)
            {
                transform.parent = _mesh.transform;
                transform.localPosition = Vector3.zero;
                transform.localEulerAngles = Vector3.zero;
            }

            _mesh.mesh.GetVertices(_vertices);
            _triangles.Clear();
            _triangles.AddRange(_mesh.mesh.GetTriangles(layer));

            _line.positionCount = maxCount;

            int _counter = 0;
            foreach (var vertex in _triangles)
            {
                if(_counter >= maxCount)
                {
                    currentVertex = vertex;
                    return;
                }
                _line.SetPosition(_counter++, _vertices[vertex]);
            }
        }
    }
}
