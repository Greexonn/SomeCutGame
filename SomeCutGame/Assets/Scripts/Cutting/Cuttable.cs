using System.Collections.Generic;
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
    private NativeList<int>[] _originalIntersectingTrianglesList;
    private NativeHashMap<float3, VertexInfo>[] _edgeVertices;
    private NativeHashMap<float3, int>[] _edgeVerticesToLeft, _edgeVerticesToRight;
    private NativeQueue<HalfNewTriangle>[] _halfNewTrianglesLeft, _halfNewTrianglesRight;
    private NativeQueue<int> _addedTrianglesLeft;
    private NativeQueue<int> _addedTrianglesRight;
    private NativeArray<VertexInfo>[] _edgeVerticesArray;

    //job handle collections
        NativeList<JobHandle> _handles, _dependencies;

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
        FillCutEdge();
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

        _addedTrianglesLeft = new NativeQueue<int>(Allocator.TempJob);
        _addedTrianglesRight = new NativeQueue<int>(Allocator.TempJob);

        //allocate job handle lists
        _handles = new NativeList<JobHandle>(Allocator.Temp);
        _dependencies = new NativeList<JobHandle>(Allocator.Temp);
    }

    private void DisposeTemporalContainers()
    {
        _sideIds.Dispose();
        _dataLeft.Dispose();
        _dataRight.Dispose();

        _originalIndexToLeft.Dispose();
        _originalIndexToRight.Dispose();

        _addedTrianglesLeft.Dispose();
        _addedTrianglesRight.Dispose();

        for (int i = 0; i < _subMeshCount; i++)
        {
            _triangleTypes[i].Dispose();
            _triangleIndexesLeft[i].Dispose();
            _triangleIndexesRight[i].Dispose();
            _originalIntersectingTriangles[i].Dispose();
            _originalIntersectingTrianglesList[i].Dispose();
            _edgeVertices[i].Dispose();
            _edgeVerticesToLeft[i].Dispose();
            _edgeVerticesToRight[i].Dispose();
            _halfNewTrianglesLeft[i].Dispose();
            _halfNewTrianglesRight[i].Dispose();
            _edgeVerticesArray[i].Dispose();
        }

        //dispose job handle lists
        _handles.Dispose();
        _dependencies.Dispose();
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
            leftSide = _dataLeft.AsParallelWriter(),
            rightSide = _dataRight.AsParallelWriter()
        };

        JobHandle _getVertexesSideJobHandle = _getVertexesSideJob.Schedule(_originalGeneratedMesh.vertices.Length, (_verticesCount / 10 + 1));
        _handles.Add(_getVertexesSideJobHandle);

        //make hash-maps for triangle indexes and mesh data
        SetMehsDataAndHashMapsJob _setMeshAndHashMaps = new SetMehsDataAndHashMapsJob
        {
            sideData = _dataLeft,
            verticesSide = _leftPart.vertices,
            normalsSide = _leftPart.normals,
            uvsSide = _leftPart.uvs,
            originalIndexesToSide = _originalIndexToLeft
        };

        _handles.Add(_setMeshAndHashMaps.Schedule(_getVertexesSideJobHandle));

        _setMeshAndHashMaps = new SetMehsDataAndHashMapsJob
        {
            sideData = _dataRight,
            verticesSide = _rightPart.vertices,
            normalsSide = _rightPart.normals,
            uvsSide = _rightPart.uvs,
            originalIndexesToSide = _originalIndexToRight
        };

        _handles.Add(_setMeshAndHashMaps.Schedule(_getVertexesSideJobHandle));

        //check triangles
        JobHandle _dependency = JobHandle.CombineDependencies(_handles[_handles.Length - 1], _handles[_handles.Length - 2]);
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

            _handles.Add(_checkTrianglesParallelJob.Schedule(_triangleTypes[i].Length, _triangleTypes[i].Length / 10 + 1, _dependency));
            _dependencies.Add(_handles[_handles.Length - 1]);
        }

        //reassign triangles
        //allocate indexes queue
        _triangleIndexesLeft = new NativeQueue<int>[_subMeshCount];
        _triangleIndexesRight = new NativeQueue<int>[_subMeshCount];
        _originalIntersectingTriangles = new NativeQueue<int>[_subMeshCount];

        for (int i = 0; i < _subMeshCount; i++)
        {
            _triangleIndexesLeft[i] = new NativeQueue<int>(Allocator.TempJob);
            _triangleIndexesRight[i] = new NativeQueue<int>(Allocator.TempJob);
            _originalIntersectingTriangles[i] = new NativeQueue<int>(Allocator.TempJob);

            ReassignTrianglesJob _reassignTrianglesJob = new ReassignTrianglesJob
            {
                triangleIndexes = _originalGeneratedMesh.triangles[i].AsArray(),
                triangleTypes = _triangleTypes[i],
                leftTriangleIndexes = _triangleIndexesLeft[i].AsParallelWriter(),
                rightTriangleIndexes = _triangleIndexesRight[i].AsParallelWriter(),
                intersectingTriangleIndexes = _originalIntersectingTriangles[i].AsParallelWriter(),
                originalIndexesToLeft = _originalIndexToLeft,
                originalIndexesToRight = _originalIndexToRight
            };
            
            //schedule job
            _handles.Add(_reassignTrianglesJob.Schedule(_triangleTypes[i].Length, (_triangleTypes[i].Length / 10 + 1), _dependencies[i]));
            _dependencies[i] = _handles[_handles.Length - 1];
        }

        NativeList<JobHandle> _localDependencies = new NativeList<JobHandle>(Allocator.Persistent);
        _localDependencies.AddRange(_dependencies.AsArray());
        _dependencies.Clear();
        //assign triangles to mesh
        for (int i = 0; i < _subMeshCount; i++)
        {
            CopyTrianglesToListJob _assignTriangles = new CopyTrianglesToListJob
            {
                triangleIndexes = _triangleIndexesLeft[i],
                listTriangles = _leftPart.triangles[i]
            };

            _handles.Add(_assignTriangles.Schedule(_localDependencies[i]));
            _dependencies.Add(_handles[_handles.Length - 1]);

            _assignTriangles = new CopyTrianglesToListJob
            {
                triangleIndexes = _triangleIndexesRight[i],
                listTriangles = _rightPart.triangles[i]
            };

            _handles.Add(_assignTriangles.Schedule(_localDependencies[i]));
            _dependencies.Add(_handles[_handles.Length - 1]);
        }
        _localDependencies.Dispose();
    }

    private void FillCutEdge()
    {
        JobHandle _previousCombined = JobHandle.CombineDependencies(_dependencies);
        _dependencies.Clear();

        _originalIntersectingTrianglesList = new NativeList<int>[_subMeshCount];
        //copy intersected triangles to list so we can iterate in parallel
        for (int i = 0; i < _subMeshCount; i++)
        {
            _originalIntersectingTrianglesList[i] = new NativeList<int>(Allocator.TempJob);

            CopyTrianglesToListJob _copyTrianglesToList = new CopyTrianglesToListJob
            {
                triangleIndexes = _originalIntersectingTriangles[i],
                listTriangles = _originalIntersectingTrianglesList[i]
            };

            _handles.Add(_copyTrianglesToList.Schedule(_previousCombined));
            _dependencies.Add(_handles[_handles.Length - 1]);
        }

        //can't schedule next job instantly cause it needs intersected triangles count
        JobHandle.CompleteAll(_handles);
        _handles.Clear();

        //iterate throuhg all intersected triangles in every sub-mesh, add edge vertices and half-new triangles
        _edgeVertices = new NativeHashMap<float3, VertexInfo>[_subMeshCount];
        _halfNewTrianglesLeft = new NativeQueue<HalfNewTriangle>[_subMeshCount];
        _halfNewTrianglesRight = new NativeQueue<HalfNewTriangle>[_subMeshCount];
        for (int i = 0; i < _subMeshCount; i++)
        {
            _edgeVertices[i] = new NativeHashMap<float3, VertexInfo>((_originalIntersectingTrianglesList[i].Length * 2 / 3), Allocator.TempJob);
            _halfNewTrianglesLeft[i] = new NativeQueue<HalfNewTriangle>(Allocator.TempJob);
            _halfNewTrianglesRight[i] = new NativeQueue<HalfNewTriangle>(Allocator.TempJob);

            CutTrianglesParallelJob _cutTriangles = new CutTrianglesParallelJob
            {
                planeCenter = new float3(_cuttingPlaneCenter.x, _cuttingPlaneCenter.y, _cuttingPlaneCenter.z),
                planeNormal = new float3(_cuttingPlaneNormal.x, _cuttingPlaneNormal.y, _cuttingPlaneNormal.z),
                vertices = _originalGeneratedMesh.vertices,
                normals = _originalGeneratedMesh.normals,
                uvs = _originalGeneratedMesh.uvs,
                triangles = _originalIntersectingTrianglesList[i],
                sideIDs = _sideIds,
                edgeVertices = _edgeVertices[i].AsParallelWriter(),
                leftHalfTriangles = _halfNewTrianglesLeft[i].AsParallelWriter(),
                rightHalfTriangles = _halfNewTrianglesRight[i].AsParallelWriter()
            };

            _handles.Add(_cutTriangles.Schedule(_originalIntersectingTrianglesList[i].Length / 3, (_originalIntersectingTrianglesList[i].Length / 3 / 10 + 1), _dependencies[i]));
            _dependencies[i] = _handles[_handles.Length - 1];
        }

        JobHandle.CompleteAll(_handles);
        _handles.Clear();

        //add new vertices and fill hash-maps
        _edgeVerticesToLeft = new NativeHashMap<float3, int>[_subMeshCount];
        _edgeVerticesToRight = new NativeHashMap<float3, int>[_subMeshCount];
        _edgeVerticesArray = new NativeArray<VertexInfo>[_subMeshCount];
        JobHandle _previousHandle = _dependencies[0];
        for (int i = 0; i < _subMeshCount; i++)
        {
            _edgeVerticesToLeft[i] = new NativeHashMap<float3, int>(_edgeVertices[i].Length, Allocator.TempJob);
            _edgeVerticesToRight[i] = new NativeHashMap<float3, int>(_edgeVertices[i].Length, Allocator.TempJob);

            _edgeVerticesArray[i] = _edgeVertices[i].GetValueArray(Allocator.TempJob);

            AddEdgeVerticesJob _addEdgeVertices = new AddEdgeVerticesJob
            {
                edgeVertices = _edgeVerticesArray[i],
                sideVertices = _leftPart.vertices,
                sideNormals = _leftPart.normals,
                sideUVs = _leftPart.uvs,
                edgeVerticesToSide = _edgeVerticesToLeft[i]
            };

            _handles.Add(_addEdgeVertices.Schedule(_previousHandle));

            _addEdgeVertices = new AddEdgeVerticesJob
            {
                edgeVertices = _edgeVerticesArray[i],
                sideVertices = _rightPart.vertices,
                sideNormals = _rightPart.normals,
                sideUVs = _rightPart.uvs,
                edgeVerticesToSide = _edgeVerticesToRight[i]
            };

            _handles.Add(_addEdgeVertices.Schedule(_previousHandle));

            _dependencies[i] = JobHandle.CombineDependencies(_handles[_handles.Length - 1], _handles[_handles.Length - 2]);
            _previousHandle = _dependencies[i];
        }
            
        //add new triangles
        for (int i = 0; i < _subMeshCount; i++)
        {
            AddEdgeTrianglesJob _addEdgeTriangles = new AddEdgeTrianglesJob
            {
                sideTriangles = _leftPart.triangles[i],
                originalIndexesToSide = _originalIndexToLeft,
                vertexToSide = _edgeVerticesToLeft[i],
                halfNewTriangles = _halfNewTrianglesLeft[i]
            };

            _handles.Add(_addEdgeTriangles.Schedule(_dependencies[i]));

            _addEdgeTriangles = new AddEdgeTrianglesJob
            {
                sideTriangles = _rightPart.triangles[i],
                originalIndexesToSide = _originalIndexToRight,
                vertexToSide = _edgeVerticesToRight[i],
                halfNewTriangles = _halfNewTrianglesRight[i]
            };

            _handles.Add(_addEdgeTriangles.Schedule(_dependencies[i]));
            
            _dependencies[i] = JobHandle.CombineDependencies(_handles[_handles.Length - 1], _handles[_handles.Length - 2]);
        }

        JobHandle.CompleteAll(_handles);
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
        Rigidbody _currentRB = gameObject.GetComponent<Rigidbody>();
        if (_currentRB == null)
        {
            _currentRB = gameObject.AddComponent<Rigidbody>();
        }
        _currentRB.ResetCenterOfMass();
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

            //set 32bit index format if needed
            if (vertices.Length > 65535)
            {
                _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

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

        public bool Empty()
        {
            if (originalIndex == -1)
                return true;

            return false;
        }
    }

    //
    //separate parts jobs
    //
    #region SeparatePartsJobs

    [BurstCompile]
    public struct GetVertexesSideJob : IJobParallelFor
    {
        [ReadOnly] public float3 planeCenter;
        [ReadOnly] public float3 planeNormal;
        [ReadOnly] public NativeArray<float3> vertices;
        [ReadOnly] public NativeArray<float3> normals;
        [ReadOnly] public NativeArray<float2> uvs; 
        [WriteOnly] public NativeArray<int> sideIds;
        [WriteOnly] public NativeQueue<VertexInfo>.ParallelWriter leftSide, rightSide;
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
        public NativeQueue<VertexInfo> sideData;
        [WriteOnly] public NativeHashMap<int, int> originalIndexesToSide;
        [WriteOnly] public NativeList<float3> verticesSide, normalsSide;
        [WriteOnly] public NativeList<float2> uvsSide;

        public void Execute()
        {
            int _counter = 0;
            while (sideData.Count > 0)
            {
                var _data = sideData.Dequeue();
                originalIndexesToSide.TryAdd(_data.originalIndex, _counter++);
                verticesSide.Add(_data.vertex);
                normalsSide.Add(_data.normal);
                uvsSide.Add(_data.uv);
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

        [WriteOnly] public NativeQueue<int>.ParallelWriter leftTriangleIndexes, rightTriangleIndexes, intersectingTriangleIndexes;

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
                    //if intersecting
                    intersectingTriangleIndexes.Enqueue(_vertexA);
                    intersectingTriangleIndexes.Enqueue(_vertexB);
                    intersectingTriangleIndexes.Enqueue(_vertexC);
                    break;
                }
            }
        }
    }

    #endregion

    //
    //cutting triangles
    //
    #region CutTrianglesJobs

    public struct Edge
    {
        public int a, b;
    }

    public struct HalfNewTriangle
    {
        public int a, b;
        public VertexInfo c, d;
    }

    [BurstCompile]
    public struct CutTrianglesParallelJob : IJobParallelFor
    {
        [ReadOnly] public float3 planeCenter, planeNormal;
        [ReadOnly] public NativeArray<float3> vertices, normals;
        [ReadOnly] public NativeArray<float2> uvs;
        [ReadOnly] public NativeList<int> triangles;
        [ReadOnly] public NativeArray<int> sideIDs;

        [WriteOnly] public NativeHashMap<float3, VertexInfo>.ParallelWriter edgeVertices;
        [WriteOnly] public NativeQueue<HalfNewTriangle>.ParallelWriter leftHalfTriangles, rightHalfTriangles;

        public void Execute(int index)
        {
            int _currentStartIndex = index * 3;
            int _vertexA = triangles[_currentStartIndex];
            int _vertexB = triangles[_currentStartIndex + 1];
            int _vertexC = triangles[_currentStartIndex + 2];

            //create edges
            Edge _ab = new Edge{a = _vertexA, b = _vertexB};
            Edge _bc = new Edge{a = _vertexB, b = _vertexC};
            Edge _ca = new Edge{a = _vertexC, b = _vertexA};

            //check every edge to find one that is not intersected
            //check a-b
            if (sideIDs[_ab.a] == sideIDs[_ab.b])
            {
                CutTriangle(_ab, _bc, _ca);
            }
            //check b-c
            if (sideIDs[_bc.a] == sideIDs[_bc.b])
            {
                CutTriangle(_bc, _ca, _ab);
            }
            //check c-a
            if (sideIDs[_ca.a] == sideIDs[_ca.b])
            {
                CutTriangle(_ca, _ab, _bc);
            }
        }

        private void CutTriangle(Edge solidEdge, Edge firstIntersected, Edge secondIntersected)
        {
            VertexInfo _newVertexOne = GetNewVertex(firstIntersected);
            VertexInfo _newVertexTwo = GetNewVertex(secondIntersected);

            //add vertices
            edgeVertices.TryAdd(_newVertexOne.vertex, _newVertexOne);
            edgeVertices.TryAdd(_newVertexTwo.vertex, _newVertexTwo);

            //create half-new triangles
            switch (sideIDs[solidEdge.a])
            {
                case -1:
                {
                    //base on right side
                    rightHalfTriangles.Enqueue(new HalfNewTriangle
                    {
                        a = solidEdge.a,
                        b = solidEdge.b,
                        c = _newVertexOne,
                        d = new VertexInfo{originalIndex = -1}
                    });
                    rightHalfTriangles.Enqueue(new HalfNewTriangle
                    {
                        a = solidEdge.a,
                        b = -1,
                        c = _newVertexOne,
                        d = _newVertexTwo
                    });
                    leftHalfTriangles.Enqueue(new HalfNewTriangle
                    {
                        a = firstIntersected.b,
                        b = -1,
                        c = _newVertexTwo,
                        d = _newVertexOne
                    });
                    break;
                }
                default:
                {
                    //base on left side
                    leftHalfTriangles.Enqueue(new HalfNewTriangle
                    {
                        a = solidEdge.a,
                        b = solidEdge.b,
                        c = _newVertexOne,
                        d = new VertexInfo{originalIndex = -1}
                    });
                    leftHalfTriangles.Enqueue(new HalfNewTriangle
                    {
                        a = solidEdge.a,
                        b = -1,
                        c = _newVertexOne,
                        d = _newVertexTwo
                    });
                    rightHalfTriangles.Enqueue(new HalfNewTriangle
                    {
                        a = firstIntersected.b,
                        b = -1,
                        c = _newVertexTwo,
                        d = _newVertexOne
                    });
                    break;
                }
            }
        }

        private VertexInfo GetNewVertex(Edge edge)
        {
            VertexInfo _newVertex = new VertexInfo();

            float _relation = FindIntersectionPosOnSegment(vertices[edge.a], vertices[edge.b]);

            _newVertex.vertex = math.lerp(vertices[edge.a], vertices[edge.b], _relation);
            _newVertex.normal = math.lerp(normals[edge.a], normals[edge.b], _relation);
            _newVertex.uv = math.lerp(uvs[edge.a], uvs[edge.b], _relation);

            return _newVertex;
        }

        private float FindIntersectionPosOnSegment(float3 vertexA, float3 vertexB)
        {
            float3 _vectorAC = planeCenter - vertexA;
            float3 _vectorAB = vertexB - vertexA;

            float _productAC = math.dot(planeNormal, _vectorAC);
            float _productAB = math.dot(planeNormal, _vectorAB);

            float _relation = _productAC / _productAB;

            return math.abs(_relation);
        }
    }

    [BurstCompile]
    public struct CopyTrianglesToListJob : IJob
    {
        public NativeQueue<int> triangleIndexes;
        [WriteOnly] public NativeList<int> listTriangles;

        public void Execute()
        {
            while (triangleIndexes.Count > 0)
            {
                listTriangles.Add(triangleIndexes.Dequeue());
            }
        }
    }

    [BurstCompile]
    public struct AddEdgeVerticesJob : IJob
    {
        [ReadOnly] public NativeArray<VertexInfo> edgeVertices;

        public NativeList<float3> sideVertices, sideNormals;
        public NativeList<float2> sideUVs;

        [WriteOnly] public NativeHashMap<float3, int> edgeVerticesToSide;

        public void Execute()
        {
            for (int i = 0; i < edgeVertices.Length; i++)
            {
                //add to hash-map
                edgeVerticesToSide.TryAdd(edgeVertices[i].vertex, sideVertices.Length);

                //add vertex info
                sideVertices.Add(edgeVertices[i].vertex);
                sideNormals.Add(edgeVertices[i].normal);
                sideUVs.Add(edgeVertices[i].uv);
            }
        }
    }

    [BurstCompile]
    public struct AddEdgeTrianglesJob : IJob
    {
        [WriteOnly] public NativeList<int> sideTriangles;

        [ReadOnly] public NativeHashMap<int, int> originalIndexesToSide;
        [ReadOnly] public NativeHashMap<float3, int> vertexToSide;

        public NativeQueue<HalfNewTriangle> halfNewTriangles;

        public void Execute()
        {
            while (halfNewTriangles.Count > 0)
            {
                HalfNewTriangle _hnTriangle = halfNewTriangles.Dequeue();

                sideTriangles.Add(originalIndexesToSide[_hnTriangle.a]);
                if (_hnTriangle.b != -1)
                {
                    sideTriangles.Add(originalIndexesToSide[_hnTriangle.b]);
                }
                sideTriangles.Add(vertexToSide[_hnTriangle.c.vertex]);
                if (!_hnTriangle.d.Empty())
                {
                    sideTriangles.Add(vertexToSide[_hnTriangle.d.vertex]);
                }
            }
        }
    }

    #endregion
}
