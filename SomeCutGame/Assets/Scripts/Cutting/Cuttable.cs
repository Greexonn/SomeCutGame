﻿using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;

public class Cuttable : MonoBehaviour
{
    public Material cutMaterial;

    private Plane _cuttingPlane;

    private MeshFilter _meshFilter;
    private Mesh _mesh;
    private int _subMeshCount;


    private GeneratedMesh _leftPart;
    private GeneratedMesh _rightPart;

    private GeneratedMesh _originalGeneratedMesh;

    //lists
    List<Mesh> _createdMeshes;
    List<GeneratedMesh> _generatedMeshes;

    //native 
    private NativeArray<int> _sideIds;
    private NativeQueue<VertexInfo> _dataLeft;
    private NativeQueue<VertexInfo> _dataRight;

    private NativeHashMap<int, int> _originalIndexToLeft;
    private NativeHashMap<int, int> _originalIndexToRight;

    private NativeArray<int>[] _triangleTypes;
    private NativeQueue<int>[] _triangleIndexesLeft;
    private NativeQueue<int>[] _triangleIndexesRight;

    private NativeQueue<int>[] _originalIntersectingTriangles;
    private NativeQueue<VertexInfo> _addedVerticesLeft;
    private NativeQueue<VertexInfo> _addedVerticesRight;
    private NativeQueue<int> _addedTrianglesLeft;
    private NativeQueue<int> _addedTrianglesRight;

    //
    private Vector3 _cuttingPlaneNormal;
    private Vector3 _cuttingPlaneCenter;

    //
    private bool _isPreSet = false;

    void Start()
    {
        //
        _meshFilter = GetComponent<MeshFilter>();
        _mesh = _meshFilter.mesh;
        _subMeshCount = _mesh.subMeshCount;

        //
        if (!_isPreSet)
        {
            _originalGeneratedMesh = new GeneratedMesh("original", _subMeshCount);
            //copy vertices, normals, uvs
            var _verts = _mesh.vertices;
            var _norms = _mesh.normals;
            var _uvs = _mesh.uv;
            
            for (int i = 0; i < _mesh.vertexCount; i++)
            {
                _originalGeneratedMesh.vertices.Add(new float3(_verts[i].x, _verts[i].y, _verts[i].z));
                _originalGeneratedMesh.normals.Add(new float3(_norms[i].x,_norms[i].y, _norms[i].z));
                _originalGeneratedMesh.uvs.Add(new float2(_uvs[i].x, _uvs[i].y));
            }
            //copy triangles
            for (int i = 0; i < _subMeshCount; i++)
            {
                int[] _subTriangles = _mesh.GetTriangles(i);
                for (int j = 0; j < _subTriangles.Length; j++)
                {
                    _originalGeneratedMesh.triangles[i].Add(_subTriangles[j]);
                }
            }
        }
    }

    public void SetGeneratedMesh(GeneratedMesh generatedMesh)
    {
        _isPreSet = true;

        _originalGeneratedMesh = generatedMesh;
    }

    void OnDestroy()
    {
        _originalGeneratedMesh.Dispose();
    }

    public void Cut(Vector3 contactPoint, Vector3 planeNormal)
    {
        //create cutting plane
        _cuttingPlane = new Plane(transform.InverseTransformDirection(planeNormal), transform.InverseTransformPoint(contactPoint));
        _cuttingPlaneNormal = _cuttingPlane.normal;
        _cuttingPlaneCenter = transform.InverseTransformPoint(contactPoint);

        //create new meshes
        _createdMeshes = new List<Mesh>();

        //create mesh containers
        _leftPart = new GeneratedMesh("left", _subMeshCount);
        _rightPart = new GeneratedMesh("right", _subMeshCount);
        _generatedMeshes = new List<GeneratedMesh>();

        //perform cut
        AllocateTemporalContainers();
        //
        SplitMesh();
        FillCut();
        FillHoles();
        //
        DisposeTemporalContainers();

        //create new parts
        CreateNewObjects();
    }

    private void AllocateTemporalContainers()
    {
        _dataLeft = new NativeQueue<VertexInfo>(Allocator.TempJob);
        _dataRight = new NativeQueue<VertexInfo>(Allocator.TempJob);
               
        _originalIndexToLeft = new NativeHashMap<int, int>(_dataLeft.Count, Allocator.TempJob);
        _originalIndexToRight = new NativeHashMap<int, int>(_dataRight.Count, Allocator.TempJob);


    }

    private void DisposeTemporalContainers()
    {
        _sideIds.Dispose();
        _dataLeft.Dispose();
        _dataRight.Dispose();

        _originalIndexToLeft.Dispose();
        _originalIndexToRight.Dispose();

        for (int i = 0; i < _subMeshCount; i++)
        {
            _triangleTypes[i].Dispose();
            _triangleIndexesLeft[i].Dispose();
            _triangleIndexesRight[i].Dispose();
        }
    }

    private void SplitMesh()
    {
        int _verticesCount = _originalGeneratedMesh.vertices.Length;
        _sideIds = new NativeArray<int>(_verticesCount, Allocator.TempJob);

        //get all vertices sides
        GetVertexesSideJob _getVertexesSideJob = new GetVertexesSideJob
        {
            planeCenter = new float3(_cuttingPlaneCenter.x, _cuttingPlaneCenter.y, _cuttingPlaneCenter.z),
            planeNormal = new float3(_cuttingPlaneNormal.x, _cuttingPlaneNormal.y, _cuttingPlaneNormal.z),
            vertices = _originalGeneratedMesh.vertices.AsArray(),
            normals = _originalGeneratedMesh.normals.AsArray(),
            uvs = _originalGeneratedMesh.uvs.AsArray(),
            sideIds = _sideIds,
            leftSide = _dataLeft.ToConcurrent(),
            rightSide = _dataRight.ToConcurrent()
        };

        _getVertexesSideJob.Schedule(_originalGeneratedMesh.vertices.Length, (_verticesCount / 10 + 1)).Complete();

        //make hash-maps for triangle indexes and mesh data
        SetMehsDataAndHashMapsJob _setMeshAndHashMaps = new SetMehsDataAndHashMapsJob
        {
            leftSideData = _dataLeft,
            rightSideData = _dataRight,
            verticesLeftSide = _leftPart.vertices,
            verticesRightSide = _rightPart.vertices,
            normalsLeftSide = _leftPart.normals,
            normalsRightSide = _rightPart.normals,
            uvsLeftSide = _leftPart.uvs,
            uvsRightSide = _rightPart.uvs,
            originalIndexesToLeft = _originalIndexToLeft,
            originalIndexesToRight = _originalIndexToRight
        };

        _setMeshAndHashMaps.Schedule().Complete();

        //check triangles
        _triangleTypes = new NativeArray<int>[_subMeshCount];
        for (int i = 0; i < _subMeshCount; i++)
        {
            _triangleTypes[i] = new NativeArray<int>(_originalGeneratedMesh.triangles[i].Length / 3, Allocator.TempJob);

            CheckTrianglesParallelJob _checkTrianglesParallelJob = new CheckTrianglesParallelJob
            {
                sideIDs = _sideIds,
                triangleIndexes = _originalGeneratedMesh.triangles[i].AsArray(),
                triangleTypes = _triangleTypes[i]
            };

            _checkTrianglesParallelJob.Schedule(_triangleTypes[i].Length, _triangleTypes[i].Length / 10 + 1).Complete();
        }

        //reassign triangles
        //allocate indexes queue
        _triangleIndexesLeft = new NativeQueue<int>[_subMeshCount];
        _triangleIndexesRight = new NativeQueue<int>[_subMeshCount];

        for (int i = 0; i < _subMeshCount; i++)
        {
            _triangleIndexesLeft[i] = new NativeQueue<int>(Allocator.TempJob);
            _triangleIndexesRight[i] = new NativeQueue<int>(Allocator.TempJob);

            ReassignTrianglesJob _reassignTrianglesJob = new ReassignTrianglesJob
            {
                triangleIndexes = _originalGeneratedMesh.triangles[i].AsArray(),
                triangleTypes = _triangleTypes[i],
                leftTriangleIndexes = _triangleIndexesLeft[i].ToConcurrent(),
                rightTriangleIndexes = _triangleIndexesRight[i].ToConcurrent(),
                originalIndexesToLeft = _originalIndexToLeft,
                originalIndexesToRight = _originalIndexToRight
            };
            
            //schedule job
            _reassignTrianglesJob.Schedule(_triangleTypes[i].Length, (_triangleTypes[i].Length / 10 + 1)).Complete();
        }

        //assign triangles to mesh
        for (int i = 0; i < _subMeshCount; i++)
        {
            AssignDataToMeshJob _assignDataToMeshJob = new AssignDataToMeshJob
            {
                triangleIndexesLeft = _triangleIndexesLeft[i],
                triangleIndexesRight = _triangleIndexesRight[i],
                meshTrianglesLeft = _leftPart.triangles[i],
                meshTrianglesRight = _rightPart.triangles[i]
            };

            _assignDataToMeshJob.Schedule().Complete();
        }
    }

    private void FillCut()
    {

    }

    private void FillHoles()
    {

    }

    private void CreateNewObjects()
    {
        //for development
        _generatedMeshes.Add(_leftPart);
        _generatedMeshes.Add(_rightPart);
        foreach (var genMesh in _generatedMeshes)
        {
            _createdMeshes.Add(genMesh.GetMesh());
        }
        
        //change mesh on current object
        _meshFilter.mesh = _createdMeshes[0];
        
        //reset current original mesh
        _originalGeneratedMesh.Dispose();
        _originalGeneratedMesh = _leftPart;
        //set materials
        List<Material> _materials = new List<Material>(gameObject.GetComponent<MeshRenderer>().materials);
        if (cutMaterial != null)
            _materials.Add(cutMaterial);
        else
            _materials.Add(_materials[0]);
        gameObject.GetComponent<MeshRenderer>().materials = _materials.ToArray();
        Destroy(GetComponent<Collider>());
        gameObject.AddComponent<MeshCollider>().convex = true;
        gameObject.AddComponent<Rigidbody>();
        _createdMeshes.Remove(_createdMeshes[0]);
        _generatedMeshes.Remove(_generatedMeshes[0]);

        //create new objects
        for (int i = 0; i < _createdMeshes.Count; i++)
        {
            GameObject _part = new GameObject(gameObject.name + "_part");

            _part.transform.SetPositionAndRotation(transform.position, transform.rotation);
            MeshFilter _partMeshFilter = _part.AddComponent<MeshFilter>();
            _partMeshFilter.mesh = _createdMeshes[i];
            MeshRenderer _partRenderer = _part.AddComponent<MeshRenderer>();
            //set materials
            _partRenderer.materials = _materials.ToArray();

            _part.AddComponent<MeshCollider>().convex = true;
            Rigidbody _partRigidbody = _part.AddComponent<Rigidbody>();
            
            //
            Cuttable _partCuttableComponent = _part.AddComponent<Cuttable>();
            _partCuttableComponent.SetGeneratedMesh(_generatedMeshes[i]);
            _partCuttableComponent.cutMaterial = _materials[_materials.Count - 1];
        }
    }

    public struct GeneratedMesh
    {
        public NativeList<float3> vertices;
        public NativeList<float3> normals;
        public NativeList<float2> uvs;
        public NativeList<int>[] triangles;

        //
        public NativeList<float3> edgeVertices;

        public GeneratedMesh(string meshName, int subMeshCount)
        {
            vertices = new NativeList<float3>(Allocator.Persistent);
            normals = new NativeList<float3>(Allocator.Persistent);
            uvs = new NativeList<float2>(Allocator.Persistent);
            triangles = new NativeList<int>[subMeshCount];
            for (int i = 0; i < subMeshCount; i++)
            {
                triangles[i] = new NativeList<int>(Allocator.Persistent);
            }
            edgeVertices = new NativeList<float3>(Allocator.Persistent);
        }

        public void Dispose()
        {
            vertices.Dispose();
            normals.Dispose();
            uvs.Dispose();
            for (int i = 0; i < triangles.Length; i++)
            {
                triangles[i].Dispose();
            }
            edgeVertices.Dispose();
        }

        public Mesh GetMesh()
        {
            Mesh _mesh = new Mesh();

            Vector3[] _vertices = new Vector3[vertices.Length];
            Vector3[] _normals = new Vector3[normals.Length];
            Vector2[] _uvs = new Vector2[uvs.Length];
            int[] _triangles = new int[triangles.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                _vertices[i] = new Vector3(vertices[i].x, vertices[i].y, vertices[i].z);
                _normals[i] = new Vector3(normals[i].x, normals[i].y, normals[i].z);
                _uvs[i] = new Vector2(uvs[i].x, uvs[i].y);
            }

            _mesh.vertices = _vertices;
            _mesh.normals = _normals;
            _mesh.uv = _uvs;
            _mesh.subMeshCount = triangles.Length;
            
            //set triangles
            for (int i = 0; i < triangles.Length; i++)
            {
                _mesh.SetTriangles(triangles[i].ToArray(), i);
            }

            return _mesh;
        }
    }

    public struct VertexInfo
    {
        public int originalIndex;
        public float3 vertex;
        public float3 normal;
        public float2 uv;
    }

    [BurstCompile]
    public struct GetVertexesSideJob : IJobParallelFor
    {
        [ReadOnly] public float3 planeCenter;
        [ReadOnly] public float3 planeNormal;
        [ReadOnly] public NativeArray<float3> vertices;
        [ReadOnly] public NativeArray<float3> normals;
        [ReadOnly] public NativeArray<float2> uvs; 
        [WriteOnly] public NativeArray<int> sideIds;
        [WriteOnly] public NativeQueue<VertexInfo>.Concurrent leftSide, rightSide;
        public void Execute(int index)
        {
            float3 _localPos = vertices[index] - planeCenter;
            float _dotProduct = math.dot(planeNormal, _localPos);

            int _side = 1;
            if (_dotProduct < 0)
            {
                _side = -1;

                rightSide.Enqueue(new VertexInfo
                {
                    originalIndex = index,
                    vertex = vertices[index],
                    normal = normals[index],
                    uv = uvs[index]
                });
            }
            else
            {
                leftSide.Enqueue(new VertexInfo
                {
                    originalIndex = index,
                    vertex = vertices[index],
                    normal = normals[index],
                    uv = uvs[index]
                });
            }

            sideIds[index] = _side;
        }
    }

    [BurstCompile]
    public struct SetMehsDataAndHashMapsJob : IJob
    {
        public NativeQueue<VertexInfo> leftSideData, rightSideData;
        [WriteOnly] public NativeHashMap<int, int> originalIndexesToLeft, originalIndexesToRight;
        [WriteOnly] public NativeList<float3> verticesLeftSide, verticesRightSide, normalsLeftSide, normalsRightSide;
        [WriteOnly] public NativeList<float2> uvsLeftSide, uvsRightSide;

        public void Execute()
        {
            int _counter = 0;
            while (leftSideData.Count > 0)
            {
                var _data = leftSideData.Dequeue();
                originalIndexesToLeft.TryAdd(_data.originalIndex, _counter++);
                verticesLeftSide.Add(_data.vertex);
                normalsLeftSide.Add(_data.normal);
                uvsLeftSide.Add(_data.uv);
            }
            _counter = 0;
            while (rightSideData.Count > 0)
            {
                var _data = rightSideData.Dequeue();
                originalIndexesToRight.TryAdd(_data.originalIndex, _counter++);
                verticesRightSide.Add(_data.vertex);
                normalsRightSide.Add(_data.normal);
                uvsRightSide.Add(_data.uv);
            }
        }
    }

    [BurstCompile]
    public struct CheckTrianglesParallelJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> sideIDs;
        [ReadOnly] public NativeArray<int> triangleIndexes;
        [WriteOnly] public NativeArray<int> triangleTypes;

        public void Execute(int index)
        {
            int _vertexA, _vertexB, _vertexC;

            int _currentStartIndex = index * 3;

            _vertexA = triangleIndexes[_currentStartIndex];
            _vertexB = triangleIndexes[_currentStartIndex + 1];
            _vertexC = triangleIndexes[_currentStartIndex + 2];

            triangleTypes[index] = GetTriangleType(_vertexA, _vertexB, _vertexC);
        }

        private int GetTriangleType(int vertexA, int vertexB, int vertexC)
        {
            return sideIDs[vertexA] + sideIDs[vertexB] + sideIDs[vertexC];
        }
    }

    [BurstCompile]
    public struct ReassignTrianglesJob : IJobParallelFor
    {
        [ReadOnly] public NativeHashMap<int, int> originalIndexesToLeft, originalIndexesToRight;
        [ReadOnly] public NativeArray<int> triangleIndexes;
        [ReadOnly] public NativeArray<int> triangleTypes;

        [WriteOnly] public NativeQueue<int>.Concurrent leftTriangleIndexes, rightTriangleIndexes;

        public void Execute(int index)
        {
            int _currentStatrtIndex = index * 3;

            int _vertexA = triangleIndexes[_currentStatrtIndex];
            int _vertexB = triangleIndexes[_currentStatrtIndex + 1]; 
            int _vertexC = triangleIndexes[_currentStatrtIndex + 2];

            switch (triangleTypes[index])
            {
                case 3:
                {
                    //if left
                    leftTriangleIndexes.Enqueue(originalIndexesToLeft[_vertexA]);
                    leftTriangleIndexes.Enqueue(originalIndexesToLeft[_vertexB]);
                    leftTriangleIndexes.Enqueue(originalIndexesToLeft[_vertexC]);
                    break;
                }
                case -3:
                {
                    //if right
                    rightTriangleIndexes.Enqueue(originalIndexesToRight[_vertexA]);
                    rightTriangleIndexes.Enqueue(originalIndexesToRight[_vertexB]);
                    rightTriangleIndexes.Enqueue(originalIndexesToRight[_vertexC]);
                    break;
                }
                default:
                {
                    // to-do
                    break;
                }
            }
        }
    }

    [BurstCompile]
    public struct AssignDataToMeshJob : IJob
    {
        public NativeQueue<int> triangleIndexesLeft, triangleIndexesRight;
        [WriteOnly] public NativeList<int> meshTrianglesLeft, meshTrianglesRight;

        public void Execute()
        {
            while (triangleIndexesLeft.Count > 0)
            {
                meshTrianglesLeft.Add(triangleIndexesLeft.Dequeue());
            }
            while (triangleIndexesRight.Count > 0)
            {
                meshTrianglesRight.Add(triangleIndexesRight.Dequeue());
            }
        }
    }
}
