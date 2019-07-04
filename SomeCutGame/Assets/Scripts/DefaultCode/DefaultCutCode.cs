using System.Collections.Generic;
using UnityEngine;

public class DefaultCutCode
{
    public static bool currentlyCutting;
    public static Mesh originalMesh;

    public static void Cut(GameObject originalGameObject, Vector3 contactPoint, Vector3 direction, Material cutMaterial = null, bool fill = true, bool addRigidbody = false)
    {
        if (currentlyCutting)
        {
            return;
        }

        currentlyCutting = true;

        Plane _plane = new Plane(originalGameObject.transform.InverseTransformDirection(-direction), originalGameObject.transform.InverseTransformPoint(contactPoint));
        originalMesh = originalGameObject.GetComponent<MeshFilter>().mesh;
        List<Vector3> _addedVertices = new List<Vector3>();

        GeneratedMesh _leftMesh = new GeneratedMesh();
        GeneratedMesh _rightMesh = new GeneratedMesh();

        int[] _submeshIndices;
        int triangleIndexA, triangleIndexB, triangleIndexC;

        for (int i = 0; i < originalMesh.subMeshCount; i++)
        {
            _submeshIndices = originalMesh.GetTriangles(i);

            for (int j = 0; j < _submeshIndices.Length; j += 3)
            {
                triangleIndexA = _submeshIndices[j];
                triangleIndexB = _submeshIndices[j + 1];
                triangleIndexC = _submeshIndices[j + 2];

                MeshTriangle _currentTriangle = GetTriangle(triangleIndexA, triangleIndexB, triangleIndexC, i);

                bool _triangleALeftSide = _plane.GetSide(originalMesh.vertices[triangleIndexA]);
                bool _triangleBLeftSide = _plane.GetSide(originalMesh.vertices[triangleIndexB]);
                bool _triangleCLeftSide = _plane.GetSide(originalMesh.vertices[triangleIndexC]);

                if (_triangleALeftSide && _triangleBLeftSide && _triangleCLeftSide)
                {
                    _leftMesh.AddTriangle(_currentTriangle);
                }
                else if (!_triangleALeftSide && !_triangleBLeftSide && !_triangleCLeftSide)
                {
                    _rightMesh.AddTriangle(_currentTriangle);
                }
                else
                {
                    CutTriangle(_plane, _currentTriangle, _triangleALeftSide, _triangleBLeftSide, _triangleCLeftSide, _leftMesh, _rightMesh, _addedVertices);
                    FillCut(_addedVertices, _plane, _leftMesh, _rightMesh);
                }
            }
        }

        //generate new objects
        GeneratePartObject(originalGameObject, _leftMesh, cutMaterial);
        GeneratePartObject(originalGameObject, _rightMesh, cutMaterial);
        //destroy original object
        GameObject.Destroy(originalGameObject);
    }

    public static void GeneratePartObject(GameObject originalGameObject, GeneratedMesh partMesh, Material cutMaterial)
    {
        GameObject _part = new GameObject(originalGameObject.name + "_part");
        _part.transform.SetPositionAndRotation(originalGameObject.transform.position, originalGameObject.transform.rotation);
        MeshFilter _partMeshFilter = _part.AddComponent<MeshFilter>();
        _partMeshFilter.mesh = partMesh.GetMesh();
        MeshRenderer _partRenderer = _part.AddComponent<MeshRenderer>();
        //set materials
        List<Material> _materials = new List<Material>(originalGameObject.GetComponent<MeshRenderer>().materials);
        if (cutMaterial != null)
            _materials.Add(cutMaterial);
        else
            _materials.Add(_materials[0]);
        _partRenderer.materials = _materials.ToArray();

        //
        _part.AddComponent<MeshCollider>().convex = true;
        _part.AddComponent<Rigidbody>();
    }

    private static void CutTriangle(Plane plane, MeshTriangle currentTriangle, bool triangleALeftSide, bool triangleBLeftSide, bool triangleCLeftSide, GeneratedMesh leftMesh, GeneratedMesh rightMesh, List<Vector3> addedVertices)
    {
        List<bool> _leftSide = new List<bool>();
        _leftSide.Add(triangleALeftSide);
        _leftSide.Add(triangleBLeftSide);
        _leftSide.Add(triangleCLeftSide);

        MeshTriangle _leftMeshTriangle = new MeshTriangle(new Vector3[2], new Vector3[2], new Vector2[2], currentTriangle.submeshIndex);
        MeshTriangle _rightMeshTriangle = new MeshTriangle(new Vector3[2], new Vector3[2], new Vector2[2], currentTriangle.submeshIndex);

        bool _left = false;
        bool _right = false;

        for (int i = 0; i < 3; i++)
        {
            if (_leftSide[i])
            {
                if (!_left)
                {
                    _left = false;

                    _leftMeshTriangle.vertices[0] = currentTriangle.vertices[i];
                    _leftMeshTriangle.vertices[1] = _leftMeshTriangle.vertices[0];

                    _leftMeshTriangle.normals[0] = currentTriangle.normals[i];
                    _leftMeshTriangle.normals[1] = _leftMeshTriangle.normals[0];

                    _leftMeshTriangle.uvs[0] = currentTriangle.uvs[i];
                    _leftMeshTriangle.uvs[1] = _leftMeshTriangle.uvs[0];
                }
                else
                {
                    _leftMeshTriangle.vertices[1] = currentTriangle.vertices[i];
                    _leftMeshTriangle.normals[1] = currentTriangle.normals[i];
                    _leftMeshTriangle.uvs[1] = currentTriangle.uvs[i];
                }
            }
            else
            {
                if (!_right)
                {
                    _right = false;

                    _rightMeshTriangle.vertices[0] = currentTriangle.vertices[i];
                    _rightMeshTriangle.vertices[1] = _rightMeshTriangle.vertices[0];

                    _rightMeshTriangle.normals[0] = currentTriangle.normals[i];
                    _rightMeshTriangle.normals[1] = _rightMeshTriangle.normals[0];

                    _rightMeshTriangle.uvs[0] = currentTriangle.uvs[i];
                    _rightMeshTriangle.uvs[1] = _rightMeshTriangle.uvs[0];
                }
                else
                {
                    _rightMeshTriangle.vertices[1] = currentTriangle.vertices[i];
                    _rightMeshTriangle.normals[1] = currentTriangle.normals[i];
                    _rightMeshTriangle.uvs[1] = currentTriangle.uvs[i];
                }
            }
        }
        
        ////////////////////////
        float _normalizedDistance;
        float _distance;

        plane.Raycast(new Ray(_leftMeshTriangle.vertices[0], (_rightMeshTriangle.vertices[0] - _leftMeshTriangle.vertices[0]).normalized), out _distance);

        _normalizedDistance = _distance / (_rightMeshTriangle.vertices[0] - _leftMeshTriangle.vertices[0]).magnitude;
        Vector3 _vertLeft = Vector3.Lerp(_leftMeshTriangle.vertices[0], _rightMeshTriangle.vertices[0], _normalizedDistance);
        addedVertices.Add(_vertLeft);

        Vector3 _normalLeft = Vector3.Lerp(_leftMeshTriangle.normals[0], _rightMeshTriangle.normals[0], _normalizedDistance);
        Vector2 _uvLeft = Vector2.Lerp(_leftMeshTriangle.uvs[0], _rightMeshTriangle.uvs[0], _normalizedDistance);

        plane.Raycast(new Ray(_leftMeshTriangle.vertices[1], (_rightMeshTriangle.vertices[1] - _leftMeshTriangle.vertices[1]).normalized), out _distance);

        _normalizedDistance = _distance / (_rightMeshTriangle.vertices[1] - _leftMeshTriangle.vertices[1]).magnitude;
        Vector3 _vertRight = Vector3.Lerp(_leftMeshTriangle.vertices[1], _rightMeshTriangle.vertices[1], _normalizedDistance);
        addedVertices.Add(_vertRight);

        Vector3 _normalRight = Vector3.Lerp(_leftMeshTriangle.normals[1], _rightMeshTriangle.normals[1], _normalizedDistance);
        Vector2 _uvRight = Vector2.Lerp(_leftMeshTriangle.uvs[1], _rightMeshTriangle.uvs[1], _normalizedDistance);

        ////////////////
        //left
        MeshTriangle _currentTriangle;
        Vector3[] _updatedVertices = new Vector3[] {_leftMeshTriangle.vertices[0], _vertLeft, _vertRight};
        Vector3[] _updatedNormals = new Vector3[] {_leftMeshTriangle.normals[0], _normalLeft, _normalRight};
        Vector2[] _updatedUVs = new Vector2[] {_leftMeshTriangle.uvs[0], _uvLeft, _uvRight};

        _currentTriangle = new MeshTriangle(_updatedVertices, _updatedNormals, _updatedUVs, currentTriangle.submeshIndex);

        if (_updatedVertices[0] != _updatedVertices[1] && _updatedVertices[0] != _updatedVertices[2])
        {
            if (Vector3.Dot(Vector3.Cross(_updatedVertices[1] - _updatedVertices[0], _updatedVertices[2] - _updatedVertices[0]), _updatedNormals[0]) < 0)
            {
                FlipTriangle(_currentTriangle);
            }
            leftMesh.AddTriangle(_currentTriangle);
        }

        _updatedVertices = new Vector3[] {_leftMeshTriangle.vertices[0], _leftMeshTriangle.vertices[1], _vertRight};
        _updatedNormals = new Vector3[] {_leftMeshTriangle.normals[0], _leftMeshTriangle.normals[1], _normalRight};
        _updatedUVs = new Vector2[] {_leftMeshTriangle.uvs[0], _leftMeshTriangle.uvs[1], _uvRight};

        _currentTriangle = new MeshTriangle(_updatedVertices, _updatedNormals, _updatedUVs, currentTriangle.submeshIndex);

        if (_updatedVertices[0] != _updatedVertices[1] && _updatedVertices[0] != _updatedVertices[2])
        {
            if (Vector3.Dot(Vector3.Cross(_updatedVertices[1] - _updatedVertices[0], _updatedVertices[2] - _updatedVertices[0]), _updatedNormals[0]) < 0)
            {
                FlipTriangle(_currentTriangle);
            }
            leftMesh.AddTriangle(_currentTriangle);
        }

        //right
        _updatedVertices = new Vector3[] {_rightMeshTriangle.vertices[0], _vertLeft, _vertRight};
        _updatedNormals = new Vector3[] {_rightMeshTriangle.normals[0], _normalLeft, _normalRight};
        _updatedUVs = new Vector2[] {_rightMeshTriangle.uvs[0], _uvLeft, _uvRight};

        _currentTriangle = new MeshTriangle(_updatedVertices, _updatedNormals, _updatedUVs, currentTriangle.submeshIndex);

        if (_updatedVertices[0] != _updatedVertices[1] && _updatedVertices[0] != _updatedVertices[2])
        {
            if (Vector3.Dot(Vector3.Cross(_updatedVertices[1] - _updatedVertices[0], _updatedVertices[2] - _updatedVertices[0]), _updatedNormals[0]) < 0)
            {
                FlipTriangle(_currentTriangle);
            }
            rightMesh.AddTriangle(_currentTriangle);
        }

        _updatedVertices = new Vector3[] {_rightMeshTriangle.vertices[0], _rightMeshTriangle.vertices[1], _vertRight};
        _updatedNormals = new Vector3[] {_rightMeshTriangle.normals[0], _rightMeshTriangle.normals[1], _normalRight};
        _updatedUVs = new Vector2[] {_rightMeshTriangle.uvs[0], _rightMeshTriangle.uvs[1], _uvRight};

        _currentTriangle = new MeshTriangle(_updatedVertices, _updatedNormals, _updatedUVs, currentTriangle.submeshIndex);

        if (_updatedVertices[0] != _updatedVertices[1] && _updatedVertices[0] != _updatedVertices[2])
        {
            if (Vector3.Dot(Vector3.Cross(_updatedVertices[1] - _updatedVertices[0], _updatedVertices[2] - _updatedVertices[0]), _updatedNormals[0]) < 0)
            {
                FlipTriangle(_currentTriangle);
            }
            rightMesh.AddTriangle(_currentTriangle);
        }
    }

    private static void FlipTriangle(MeshTriangle currentTriangle)
    {
        currentTriangle.vertices.Reverse();
    }

    public static void FillCut(List<Vector3> addedVertices, Plane plane, GeneratedMesh leftMesh, GeneratedMesh rightMesh)
    {
        List<Vector3> _vertices = new List<Vector3>();
        List<Vector3> _polygone = new List<Vector3>();

        for (int i = 0; i < addedVertices.Count; i++)
        {
            if (!_vertices.Contains(addedVertices[i]))
            {
                _polygone.Clear();
                _polygone.Add(addedVertices[i]);
                _polygone.Add(addedVertices[i + 1]);

                _vertices.Add(addedVertices[i]);
                _vertices.Add(addedVertices[i + 1]);

                EvaluatePairs(addedVertices, _vertices, _polygone);
                Fill(_polygone, plane, leftMesh, rightMesh);
            }
        }
    }

    private static void EvaluatePairs(List<Vector3> addedVertices, List<Vector3> vertices, List<Vector3> polygone)
    {
        bool _isDone = false;
        while (!_isDone)
        {
            _isDone = true;
            for (int i = 0; i < addedVertices.Count; i += 2)
            {
                if (addedVertices[i] == polygone[polygone.Count - 1] && !vertices.Contains(addedVertices[i + 1]))
                {
                    _isDone = false;
                    polygone.Add(addedVertices[i + 1]);
                    vertices.Add(addedVertices[i + 1]);
                }
                else if (addedVertices[i + 1] == polygone[polygone.Count - 1] && !vertices.Contains(addedVertices[i]))
                {
                    _isDone = false;
                    polygone.Add(addedVertices[i]);
                    vertices.Add(addedVertices[i]);
                }
            }
        }
    }

    private static void Fill(List<Vector3> vertices, Plane plane, GeneratedMesh leftMesh, GeneratedMesh rightMesh)
    {
        Vector3 _centerPosition = Vector3.zero;
        for (int i = 0; i < vertices.Count; i++)
        {
            _centerPosition += vertices[i];
        }
        _centerPosition = _centerPosition / vertices.Count;

        Vector3 _up = plane.normal;
       

        Vector3 _left = Vector3.Cross(plane.normal, plane.normal);

        Vector3 _displacement = Vector3.zero;
        Vector2 _uv1 = Vector2.zero;
        Vector2 _uv2 = Vector2.zero;

        for (int i = 0; i < vertices.Count; i++)
        {
            _displacement = vertices[i] - _centerPosition;
            _uv1 = new Vector2()
            {
                x = 0.5f + Vector3.Dot(_displacement, _left),
                y = 0.5f + Vector3.Dot(_displacement, _up)
            };

            Vector3[] _vertices = new Vector3[] {vertices[i], vertices[(i + 1) % vertices.Count], _centerPosition};
            Vector3[] _normals = new Vector3[] {-plane.normal, -plane.normal, -plane.normal};
            Vector2[] _uvs = new Vector2[] {_uv1, _uv2, new Vector2(0.5f, 0.5f)};

            MeshTriangle _currentTriangle = new MeshTriangle(_vertices, _normals, _uvs, originalMesh.subMeshCount);

            if (Vector3.Dot(Vector3.Cross(_vertices[1] - _vertices[0], _vertices[2] - _vertices[0]), _normals[0]) < 0)
            {
                FlipTriangle(_currentTriangle);
            }
            leftMesh.AddTriangle(_currentTriangle);

            _normals = new Vector3[] {plane.normal, plane.normal, plane.normal};
            _currentTriangle = new MeshTriangle(_vertices, _normals, _uvs, originalMesh.subMeshCount);

            if (Vector3.Dot(Vector3.Cross(_vertices[1] - _vertices[0], _vertices[2] - _vertices[0]), _normals[0]) < 0)
            {
                FlipTriangle(_currentTriangle);
            }
            rightMesh.AddTriangle(_currentTriangle);
        }
    }

    private static MeshTriangle GetTriangle(int triangleIndexA, int triangleIndexB, int triangleIndexC, int i)
    {
        List<Vector3> _vertices = new List<Vector3>();
        List<Vector3> _normals = new List<Vector3>();
        List<Vector2> _uvs = new List<Vector2>();
        
        _vertices.Add(originalMesh.vertices[triangleIndexA]);
        _vertices.Add(originalMesh.vertices[triangleIndexB]);
        _vertices.Add(originalMesh.vertices[triangleIndexC]);

        _normals.Add(originalMesh.normals[triangleIndexA]);
        _normals.Add(originalMesh.normals[triangleIndexB]);
        _normals.Add(originalMesh.normals[triangleIndexC]);

        _uvs.Add(originalMesh.uv[triangleIndexA]);
        _uvs.Add(originalMesh.uv[triangleIndexB]);
        _uvs.Add(originalMesh.uv[triangleIndexC]);

        return new MeshTriangle(_vertices.ToArray(), _normals.ToArray(), _uvs.ToArray(), i);
    }
}

public class GeneratedMesh
{
    public List<Vector3> vertices = new List<Vector3>();
    public List<Vector3> normals = new List<Vector3>();
    public List<Vector2> uvs = new List<Vector2>();
    public List<List<int>> triangles = new List<List<int>>();

    //
    public List<Vector3> edgeVertices = new List<Vector3>();

    private bool[] _verticesPresents = new bool[3];
    private int[] _presentedIDs = new int[3];

    public void AddTriangle(MeshTriangle triangle)
    {
        if (triangles.Count < (triangle.submeshIndex + 1))
        {
            for (int i = triangles.Count; i < triangle.submeshIndex + 1; i++)
            {
                triangles.Add(new List<int>());
            }
        }

        for (int i = 0; i < 3; i++)
            _verticesPresents[i] = false;
        for (int i = 0; i < vertices.Count; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                if (vertices[i] == triangle.vertices[j])
                {
                    _verticesPresents[j] = true;
                    _presentedIDs[j] = i;
                }
            }
        }
        for (int i = 0; i < 3; i++)
        {
            if (_verticesPresents[i] && triangles[triangle.submeshIndex].Contains(_presentedIDs[i]))
            {
                triangles[triangle.submeshIndex].Add(_presentedIDs[i]);
            }
            else
            {
                vertices.Add(triangle.vertices[i]);
                normals.Add(triangle.normals[i]);
                uvs.Add(triangle.uvs[i]);
                triangles[triangle.submeshIndex].Add(vertices.Count - 1);
            }
        }
    }

    public Mesh GetMesh()
    {
        Mesh _mesh = new Mesh();

        _mesh.SetVertices(vertices);
        _mesh.subMeshCount = triangles.Count;
        for (int i = 0; i < triangles.Count; i++)
        {
            _mesh.SetTriangles(triangles[i], i);
        }
        _mesh.SetNormals(normals);
        _mesh.SetUVs(0, uvs);

        return _mesh;
    }
}

public class MeshTriangle
{
    public List<Vector3> vertices = new List<Vector3>();
    public List<Vector3> normals = new List<Vector3>();
    public List<Vector2> uvs = new List<Vector2>();
    public int submeshIndex;

    public MeshTriangle(Vector3[] vertices, Vector3[] normals, Vector2[] uvs, int submeshIndex)
    {
        Clear();

        this.vertices.AddRange(vertices);
        this.normals.AddRange(normals);
        this.uvs.AddRange(uvs);

        this.submeshIndex = submeshIndex;
    }

    public void Clear()
    {
        vertices.Clear();
        normals.Clear();
        uvs.Clear();

        submeshIndex = 0;
    }
}
