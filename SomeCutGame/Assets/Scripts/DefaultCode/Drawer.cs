using System.Collections.Generic;
using Cutting;
using UnityEngine;

namespace DefaultCode
{
    public class Drawer : MonoBehaviour
    {
        public CuttableMesh _cuttableMeshObject;

        [SerializeField] private List<Vector3> _vertices = new List<Vector3>();
        private LineRenderer _line;

        private void Start()
        {
            _line = GetComponent<LineRenderer>();
        }

        private void Update()
        {
            if (_cuttableMeshObject == null) 
                return;
            
            var localTransform = transform;
            if (localTransform.parent != _cuttableMeshObject.transform)
            {
                localTransform.parent = _cuttableMeshObject.transform;
                localTransform.localPosition = Vector3.zero;
                localTransform.localEulerAngles = Vector3.zero;
            }

            _line.positionCount = _vertices.Count;

            var counter = 0;
            foreach (var vertex in _vertices)
            {
                _line.SetPosition(counter++, vertex);
            }
        }
    }
}
