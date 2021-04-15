using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DefaultCode
{
    public static class DefaultCutCode
    {
        public static bool currentlyCutting;
        private static Mesh _originalMesh;

        public static void Cut(GameObject originalGameObject, Vector3 contactPoint, Vector3 direction, Material cutMaterial = null, bool fill = true, bool addRigidbody = false)
        {
            if (currentlyCutting)
            {
                return;
            }

            currentlyCutting = true;

            var plane = new Plane(originalGameObject.transform.InverseTransformDirection(-direction), originalGameObject.transform.InverseTransformPoint(contactPoint));
            _originalMesh = originalGameObject.GetComponent<MeshFilter>().mesh;
            var addedVertices = new List<Vector3>();

            var leftMesh = new GeneratedMesh();
            var rightMesh = new GeneratedMesh();

            for (var i = 0; i < _originalMesh.subMeshCount; i++)
            {
                var submeshIndices = _originalMesh.GetTriangles(i);

                for (var j = 0; j < submeshIndices.Length; j += 3)
                {
                    var triangleIndexA = submeshIndices[j];
                    var triangleIndexB = submeshIndices[j + 1];
                    var triangleIndexC = submeshIndices[j + 2];

                    var currentTriangle = GetTriangle(triangleIndexA, triangleIndexB, triangleIndexC, i);

                    var triangleALeftSide = plane.GetSide(_originalMesh.vertices[triangleIndexA]);
                    var triangleBLeftSide = plane.GetSide(_originalMesh.vertices[triangleIndexB]);
                    var triangleCLeftSide = plane.GetSide(_originalMesh.vertices[triangleIndexC]);

                    switch (triangleALeftSide)
                    {
                        case true when triangleBLeftSide && triangleCLeftSide:
                            leftMesh.AddTriangle(currentTriangle);
                            break;
                        case false when !triangleBLeftSide && !triangleCLeftSide:
                            rightMesh.AddTriangle(currentTriangle);
                            break;
                        default:
                            CutTriangle(plane, currentTriangle, triangleALeftSide, triangleBLeftSide, triangleCLeftSide, leftMesh, rightMesh, addedVertices);
                            FillCut(addedVertices, plane, leftMesh, rightMesh);
                            break;
                    }
                }
            }

            //generate new objects
            GeneratePartObject(originalGameObject, leftMesh, cutMaterial);
            GeneratePartObject(originalGameObject, rightMesh, cutMaterial);
            //destroy original object
            Object.Destroy(originalGameObject);
        }

        private static void GeneratePartObject(GameObject originalGameObject, GeneratedMesh partMesh, Material cutMaterial)
        {
            var part = new GameObject(originalGameObject.name + "_part");
            part.transform.SetPositionAndRotation(originalGameObject.transform.position, originalGameObject.transform.rotation);
            var partMeshFilter = part.AddComponent<MeshFilter>();
            partMeshFilter.mesh = partMesh.GetMesh();
            var partRenderer = part.AddComponent<MeshRenderer>();
            //set materials
            var materials = new List<Material>(originalGameObject.GetComponent<MeshRenderer>().materials);
            materials.Add(cutMaterial != null ? cutMaterial : materials[0]);
            partRenderer.materials = materials.ToArray();

            //
            part.AddComponent<MeshCollider>().convex = true;
            part.AddComponent<Rigidbody>();
        }

        private static void CutTriangle(Plane plane, MeshTriangle currentTriangle, bool triangleALeftSide, bool triangleBLeftSide, bool triangleCLeftSide, GeneratedMesh leftMesh, GeneratedMesh rightMesh, List<Vector3> addedVertices)
        {
            var leftSide = new List<bool> {triangleALeftSide, triangleBLeftSide, triangleCLeftSide};

            var leftMeshTriangle = new MeshTriangle(new Vector3[2], new Vector3[2], new Vector2[2], currentTriangle.submeshIndex);
            var rightMeshTriangle = new MeshTriangle(new Vector3[2], new Vector3[2], new Vector2[2], currentTriangle.submeshIndex);

            for (var i = 0; i < 3; i++)
            {
                if (leftSide[i])
                {
                    leftMeshTriangle.vertices[0] = currentTriangle.vertices[i];
                    leftMeshTriangle.vertices[1] = leftMeshTriangle.vertices[0];

                    leftMeshTriangle.normals[0] = currentTriangle.normals[i];
                    leftMeshTriangle.normals[1] = leftMeshTriangle.normals[0];

                    leftMeshTriangle.uvs[0] = currentTriangle.uvs[i];
                    leftMeshTriangle.uvs[1] = leftMeshTriangle.uvs[0];
                }
                else
                {
                    rightMeshTriangle.vertices[0] = currentTriangle.vertices[i];
                    rightMeshTriangle.vertices[1] = rightMeshTriangle.vertices[0];

                    rightMeshTriangle.normals[0] = currentTriangle.normals[i];
                    rightMeshTriangle.normals[1] = rightMeshTriangle.normals[0];

                    rightMeshTriangle.uvs[0] = currentTriangle.uvs[i];
                    rightMeshTriangle.uvs[1] = rightMeshTriangle.uvs[0];
                }
            }
        
            ////////////////////////

            plane.Raycast(new Ray(leftMeshTriangle.vertices[0], (rightMeshTriangle.vertices[0] - leftMeshTriangle.vertices[0]).normalized), out var distance);

            var normalizedDistance = distance / (rightMeshTriangle.vertices[0] - leftMeshTriangle.vertices[0]).magnitude;
            var vertLeft = Vector3.Lerp(leftMeshTriangle.vertices[0], rightMeshTriangle.vertices[0], normalizedDistance);
            addedVertices.Add(vertLeft);

            var normalLeft = Vector3.Lerp(leftMeshTriangle.normals[0], rightMeshTriangle.normals[0], normalizedDistance);
            var uvLeft = Vector2.Lerp(leftMeshTriangle.uvs[0], rightMeshTriangle.uvs[0], normalizedDistance);

            plane.Raycast(new Ray(leftMeshTriangle.vertices[1], (rightMeshTriangle.vertices[1] - leftMeshTriangle.vertices[1]).normalized), out distance);

            normalizedDistance = distance / (rightMeshTriangle.vertices[1] - leftMeshTriangle.vertices[1]).magnitude;
            var vertRight = Vector3.Lerp(leftMeshTriangle.vertices[1], rightMeshTriangle.vertices[1], normalizedDistance);
            addedVertices.Add(vertRight);

            var normalRight = Vector3.Lerp(leftMeshTriangle.normals[1], rightMeshTriangle.normals[1], normalizedDistance);
            var uvRight = Vector2.Lerp(leftMeshTriangle.uvs[1], rightMeshTriangle.uvs[1], normalizedDistance);

            ////////////////
            //left
            var updatedVertices = new[] {leftMeshTriangle.vertices[0], vertLeft, vertRight};
            var updatedNormals = new[] {leftMeshTriangle.normals[0], normalLeft, normalRight};
            var updatedUVs = new[] {leftMeshTriangle.uvs[0], uvLeft, uvRight};

            var _currentTriangle = new MeshTriangle(updatedVertices, updatedNormals, updatedUVs, currentTriangle.submeshIndex);

            if (updatedVertices[0] != updatedVertices[1] && updatedVertices[0] != updatedVertices[2])
            {
                if (Vector3.Dot(Vector3.Cross(updatedVertices[1] - updatedVertices[0], updatedVertices[2] - updatedVertices[0]), updatedNormals[0]) < 0)
                {
                    FlipTriangle(_currentTriangle);
                }
                leftMesh.AddTriangle(_currentTriangle);
            }

            updatedVertices = new[] {leftMeshTriangle.vertices[0], leftMeshTriangle.vertices[1], vertRight};
            updatedNormals = new[] {leftMeshTriangle.normals[0], leftMeshTriangle.normals[1], normalRight};
            updatedUVs = new[] {leftMeshTriangle.uvs[0], leftMeshTriangle.uvs[1], uvRight};

            _currentTriangle = new MeshTriangle(updatedVertices, updatedNormals, updatedUVs, currentTriangle.submeshIndex);

            if (updatedVertices[0] != updatedVertices[1] && updatedVertices[0] != updatedVertices[2])
            {
                if (Vector3.Dot(Vector3.Cross(updatedVertices[1] - updatedVertices[0], updatedVertices[2] - updatedVertices[0]), updatedNormals[0]) < 0)
                {
                    FlipTriangle(_currentTriangle);
                }
                leftMesh.AddTriangle(_currentTriangle);
            }

            //right
            updatedVertices = new[] {rightMeshTriangle.vertices[0], vertLeft, vertRight};
            updatedNormals = new[] {rightMeshTriangle.normals[0], normalLeft, normalRight};
            updatedUVs = new[] {rightMeshTriangle.uvs[0], uvLeft, uvRight};

            _currentTriangle = new MeshTriangle(updatedVertices, updatedNormals, updatedUVs, currentTriangle.submeshIndex);

            if (updatedVertices[0] != updatedVertices[1] && updatedVertices[0] != updatedVertices[2])
            {
                if (Vector3.Dot(Vector3.Cross(updatedVertices[1] - updatedVertices[0], updatedVertices[2] - updatedVertices[0]), updatedNormals[0]) < 0)
                {
                    FlipTriangle(_currentTriangle);
                }
                rightMesh.AddTriangle(_currentTriangle);
            }

            updatedVertices = new[] {rightMeshTriangle.vertices[0], rightMeshTriangle.vertices[1], vertRight};
            updatedNormals = new[] {rightMeshTriangle.normals[0], rightMeshTriangle.normals[1], normalRight};
            updatedUVs = new[] {rightMeshTriangle.uvs[0], rightMeshTriangle.uvs[1], uvRight};

            _currentTriangle = new MeshTriangle(updatedVertices, updatedNormals, updatedUVs, currentTriangle.submeshIndex);

            if (updatedVertices[0] == updatedVertices[1] || updatedVertices[0] == updatedVertices[2]) 
                return;
            
            if (Vector3.Dot(Vector3.Cross(updatedVertices[1] - updatedVertices[0], updatedVertices[2] - updatedVertices[0]), updatedNormals[0]) < 0)
            {
                FlipTriangle(_currentTriangle);
            }
            
            rightMesh.AddTriangle(_currentTriangle);
        }

        private static void FlipTriangle(MeshTriangle currentTriangle)
        {
            currentTriangle.vertices.Reverse();
        }

        private static void FillCut(List<Vector3> addedVertices, Plane plane, GeneratedMesh leftMesh, GeneratedMesh rightMesh)
        {
            var vertices = new List<Vector3>();
            var polygone = new List<Vector3>();

            for (var i = 0; i < addedVertices.Count; i++)
            {
                if (vertices.Contains(addedVertices[i])) 
                    continue;
                
                polygone.Clear();
                polygone.Add(addedVertices[i]);
                polygone.Add(addedVertices[i + 1]);

                vertices.Add(addedVertices[i]);
                vertices.Add(addedVertices[i + 1]);

                EvaluatePairs(addedVertices, vertices, polygone);
                Fill(polygone, plane, leftMesh, rightMesh);
            }
        }

        private static void EvaluatePairs(IReadOnlyList<Vector3> addedVertices, ICollection<Vector3> vertices, IList<Vector3> polygone)
        {
            var isDone = false;
            while (!isDone)
            {
                isDone = true;
                for (var i = 0; i < addedVertices.Count; i += 2)
                {
                    if (addedVertices[i] == polygone[polygone.Count - 1] && !vertices.Contains(addedVertices[i + 1]))
                    {
                        isDone = false;
                        polygone.Add(addedVertices[i + 1]);
                        vertices.Add(addedVertices[i + 1]);
                    }
                    else if (addedVertices[i + 1] == polygone[polygone.Count - 1] && !vertices.Contains(addedVertices[i]))
                    {
                        isDone = false;
                        polygone.Add(addedVertices[i]);
                        vertices.Add(addedVertices[i]);
                    }
                }
            }
        }

        private static void Fill(IReadOnlyList<Vector3> vertices, Plane plane, GeneratedMesh leftMesh, GeneratedMesh rightMesh)
        {
            var centerPosition = vertices.Aggregate(Vector3.zero, (current, t) => current + t);
            centerPosition /= vertices.Count;

            var up = plane.normal;
       

            var left = Vector3.Cross(plane.normal, plane.normal);

            var uv2 = Vector2.zero;

            for (var i = 0; i < vertices.Count; i++)
            {
                var displacement = vertices[i] - centerPosition;
                var uv1 = new Vector2
                {
                    x = 0.5f + Vector3.Dot(displacement, left),
                    y = 0.5f + Vector3.Dot(displacement, up)
                };

                var _vertices = new[] {vertices[i], vertices[(i + 1) % vertices.Count], centerPosition};
                var _normals = new[] {-plane.normal, -plane.normal, -plane.normal};
                var _uvs = new[] {uv1, uv2, new Vector2(0.5f, 0.5f)};

                var currentTriangle = new MeshTriangle(_vertices, _normals, _uvs, _originalMesh.subMeshCount);

                if (Vector3.Dot(Vector3.Cross(_vertices[1] - _vertices[0], _vertices[2] - _vertices[0]), _normals[0]) < 0)
                {
                    FlipTriangle(currentTriangle);
                }
                leftMesh.AddTriangle(currentTriangle);

                _normals = new[] {plane.normal, plane.normal, plane.normal};
                currentTriangle = new MeshTriangle(_vertices, _normals, _uvs, _originalMesh.subMeshCount);

                if (Vector3.Dot(Vector3.Cross(_vertices[1] - _vertices[0], _vertices[2] - _vertices[0]), _normals[0]) < 0)
                {
                    FlipTriangle(currentTriangle);
                }
                rightMesh.AddTriangle(currentTriangle);
            }
        }

        private static MeshTriangle GetTriangle(int triangleIndexA, int triangleIndexB, int triangleIndexC, int i)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
        
            vertices.Add(_originalMesh.vertices[triangleIndexA]);
            vertices.Add(_originalMesh.vertices[triangleIndexB]);
            vertices.Add(_originalMesh.vertices[triangleIndexC]);

            normals.Add(_originalMesh.normals[triangleIndexA]);
            normals.Add(_originalMesh.normals[triangleIndexB]);
            normals.Add(_originalMesh.normals[triangleIndexC]);

            uvs.Add(_originalMesh.uv[triangleIndexA]);
            uvs.Add(_originalMesh.uv[triangleIndexB]);
            uvs.Add(_originalMesh.uv[triangleIndexC]);

            return new MeshTriangle(vertices.ToArray(), normals.ToArray(), uvs.ToArray(), i);
        }
    }

    public class GeneratedMesh
    {
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<List<int>> _triangles = new List<List<int>>();
        
        private readonly bool[] _verticesPresents = new bool[3];
        private readonly int[] _presentedIDs = new int[3];

        public void AddTriangle(MeshTriangle triangle)
        {
            if (_triangles.Count < (triangle.submeshIndex + 1))
            {
                for (var i = _triangles.Count; i < triangle.submeshIndex + 1; i++)
                {
                    _triangles.Add(new List<int>());
                }
            }

            for (var i = 0; i < 3; i++)
                _verticesPresents[i] = false;
            for (var i = 0; i < _vertices.Count; i++)
            {
                for (var j = 0; j < 3; j++)
                {
                    if (_vertices[i] != triangle.vertices[j]) 
                        continue;
                    
                    _verticesPresents[j] = true;
                    _presentedIDs[j] = i;
                }
            }
            for (var i = 0; i < 3; i++)
            {
                if (_verticesPresents[i] && _triangles[triangle.submeshIndex].Contains(_presentedIDs[i]))
                {
                    _triangles[triangle.submeshIndex].Add(_presentedIDs[i]);
                }
                else
                {
                    _vertices.Add(triangle.vertices[i]);
                    _normals.Add(triangle.normals[i]);
                    _uvs.Add(triangle.uvs[i]);
                    _triangles[triangle.submeshIndex].Add(_vertices.Count - 1);
                }
            }
        }

        public Mesh GetMesh()
        {
            var mesh = new Mesh();

            mesh.SetVertices(_vertices);
            mesh.subMeshCount = _triangles.Count;
            for (var i = 0; i < _triangles.Count; i++)
            {
                mesh.SetTriangles(_triangles[i], i);
            }
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uvs);

            return mesh;
        }
    }

    public class MeshTriangle
    {
        public readonly List<Vector3> vertices = new List<Vector3>();
        public readonly List<Vector3> normals = new List<Vector3>();
        public readonly List<Vector2> uvs = new List<Vector2>();
        public int submeshIndex;

        public MeshTriangle(IEnumerable<Vector3> vertices, IEnumerable<Vector3> normals, IEnumerable<Vector2> uvs, int submeshIndex)
        {
            Clear();

            this.vertices.AddRange(vertices);
            this.normals.AddRange(normals);
            this.uvs.AddRange(uvs);

            this.submeshIndex = submeshIndex;
        }

        private void Clear()
        {
            vertices.Clear();
            normals.Clear();
            uvs.Clear();

            submeshIndex = 0;
        }
    }
}