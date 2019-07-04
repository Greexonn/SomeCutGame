using System.Collections.Generic;
using UnityEngine;

public class Drawer : MonoBehaviour
{
    public Cuttable cuttableObject;

    private List<Vector3> _vertices = new List<Vector3>();
    private List<int> _triangles = new List<int>();
    private LineRenderer _line;

    void Start()
    {
        _line = GetComponent<LineRenderer>();
    }

    void Update()
    {
        if (cuttableObject != null)
        {
            if (transform.parent != cuttableObject.transform)
            {
                transform.parent = cuttableObject.transform;
                transform.localPosition = Vector3.zero;
                transform.localEulerAngles = Vector3.zero;
            }

            _vertices = cuttableObject.edgeVertices;

            _line.positionCount = _vertices.Count;

            int _counter = 0;
            foreach (var vertex in _vertices)
            {
                _line.SetPosition(_counter++, vertex);
            }
        }
    }
}
