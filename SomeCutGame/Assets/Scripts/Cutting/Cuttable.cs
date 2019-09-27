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
    private NativeHashMap<Edge, VertexInfo>[] _edgesToVertices;
    private NativeHashMap<Edge, int>[] _edgeVerticesToLeft, _edgeVerticesToRight;
    private NativeQueue<HalfNewTriangle>[] _halfNewTrianglesLeft, _halfNewTrianglesRight;
    private NativeQueue<int> _addedTrianglesLeft;
    private NativeQueue<int> _addedTrianglesRight;
    private NativeArray<Edge>[] _intersectedEdges;

    private NativeArray<VertexInfo>[] _edgeVertices;
    private NativeHashMap<int, int>[] _edgesToLeft, _edgesToRight;
    private NativeList<float2>[] _edgeVerticesOnPlane;
    private NativeList<int>[] _sortedEdgeVertices;
    private NativeList<int>[] _cutSurfaceTriangles;

    //job handle collections
        NativeList<JobHandle> _handles, _dependencies;

    //
    private Vector3 _cuttingPlaneNormal;
    private Vector3 _cuttingPlaneCenter;

    private float3 _planeCenter;
    private float3 _planeNormal;
    private float3 _planeXAxis;
    private float3 _planeYAxis;

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
                _originalGeneratedMesh.vertices.Add(new float3(_verts[i]));
                _originalGeneratedMesh.normals.Add(new float3(_norms[i]));
                _originalGeneratedMesh.uvs.Add(new float2(_uvs[i]));
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

    public void Cut(Vector3 contactPoint, Vector3 planeNormal, Vector3 planeUp, Vector3 planeLeft)
    {
        //create cutting plane
        _cuttingPlane = new Plane(transform.InverseTransformDirection(planeNormal), transform.InverseTransformPoint(contactPoint));
        _cuttingPlaneNormal = _cuttingPlane.normal;
        _cuttingPlaneCenter = transform.InverseTransformPoint(contactPoint);
        _planeCenter = new float3(_cuttingPlaneCenter);
        _planeNormal = new float3(_cuttingPlaneNormal);
        _planeXAxis = new float3(transform.InverseTransformDirection(planeUp));
        _planeYAxis = new float3(transform.InverseTransformDirection(planeLeft));

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
            _edgesToVertices[i].Dispose();
            _edgeVerticesToLeft[i].Dispose();
            _edgeVerticesToRight[i].Dispose();
            _halfNewTrianglesLeft[i].Dispose();
            _halfNewTrianglesRight[i].Dispose();
            _intersectedEdges[i].Dispose();
            _edgeVerticesOnPlane[i].Dispose();
            _edgeVertices[i].Dispose();
            _sortedEdgeVertices[i].Dispose();
            _edgesToLeft[i].Dispose();
            _edgesToRight[i].Dispose();
            _cutSurfaceTriangles[i].Dispose();
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
        _edgesToVertices = new NativeHashMap<Edge, VertexInfo>[_subMeshCount];
        _halfNewTrianglesLeft = new NativeQueue<HalfNewTriangle>[_subMeshCount];
        _halfNewTrianglesRight = new NativeQueue<HalfNewTriangle>[_subMeshCount];
        for (int i = 0; i < _subMeshCount; i++)
        {
            _edgesToVertices[i] = new NativeHashMap<Edge, VertexInfo>((_originalIntersectingTrianglesList[i].Length * 2 / 3), Allocator.TempJob);
            _halfNewTrianglesLeft[i] = new NativeQueue<HalfNewTriangle>(Allocator.TempJob);
            _halfNewTrianglesRight[i] = new NativeQueue<HalfNewTriangle>(Allocator.TempJob);

            CutTrianglesParallelJob _cutTriangles = new CutTrianglesParallelJob
            {
                planeCenter = _cuttingPlaneCenter,
                planeNormal = _cuttingPlaneNormal,
                vertices = _originalGeneratedMesh.vertices,
                normals = _originalGeneratedMesh.normals,
                uvs = _originalGeneratedMesh.uvs,
                triangles = _originalIntersectingTrianglesList[i],
                sideIDs = _sideIds,
                edgesToVertices = _edgesToVertices[i].AsParallelWriter(),
                leftHalfTriangles = _halfNewTrianglesLeft[i].AsParallelWriter(),
                rightHalfTriangles = _halfNewTrianglesRight[i].AsParallelWriter()
            };

            _handles.Add(_cutTriangles.Schedule(_originalIntersectingTrianglesList[i].Length / 3, (_originalIntersectingTrianglesList[i].Length / 3 / 10 + 1), _dependencies[i]));
            _dependencies[i] = _handles[_handles.Length - 1];
        }

        JobHandle.CompleteAll(_handles);
        _handles.Clear();
        
        //add new vertices and fill hash-maps
        _edgeVerticesToLeft = new NativeHashMap<Edge, int>[_subMeshCount];
        _edgeVerticesToRight = new NativeHashMap<Edge, int>[_subMeshCount];
        _intersectedEdges = new NativeArray<Edge>[_subMeshCount];
        JobHandle _previousHandle = _dependencies[0];
        //we will need left side vertex count for farther calculations
        int _leftSideVertexCount = _leftPart.vertices.Length;

        for (int i = 0; i < _subMeshCount; i++)
        {
            _edgeVerticesToLeft[i] = new NativeHashMap<Edge, int>(_edgesToVertices[i].Length, Allocator.TempJob);
            _edgeVerticesToRight[i] = new NativeHashMap<Edge, int>(_edgesToVertices[i].Length, Allocator.TempJob);

            _intersectedEdges[i] = _edgesToVertices[i].GetKeyArray(Allocator.TempJob);

            AddEdgeVerticesJob _addEdgeVertices = new AddEdgeVerticesJob
            {
                edges = _intersectedEdges[i],
                edgesToVertices = _edgesToVertices[i],
                startVertexCount = _leftSideVertexCount,
                sideVertices = _leftPart.vertices,
                sideNormals = _leftPart.normals,
                sideUVs = _leftPart.uvs,
                edgeVerticesToSide = _edgeVerticesToLeft[i]
            };

            _handles.Add(_addEdgeVertices.Schedule(_previousHandle));

            _addEdgeVertices = new AddEdgeVerticesJob
            {
                edges = _intersectedEdges[i],
                edgesToVertices = _edgesToVertices[i],
                startVertexCount = _rightPart.vertices.Length,
                sideVertices = _rightPart.vertices,
                sideNormals = _rightPart.normals,
                sideUVs = _rightPart.uvs,
                edgeVerticesToSide = _edgeVerticesToRight[i]
            };

            _handles.Add(_addEdgeVertices.Schedule(_previousHandle));

            _dependencies[i] = JobHandle.CombineDependencies(_handles[_handles.Length - 1], _handles[_handles.Length - 2]);
            _previousHandle = _dependencies[i];
        }
            
        //add new triangles and edges
        _edgesToLeft = new NativeHashMap<int, int>[_subMeshCount];
        _edgesToRight = new NativeHashMap<int, int>[_subMeshCount];
        for (int i = 0; i < _subMeshCount; i++)
        {
            _edgesToLeft[i] = new NativeHashMap<int, int>(_originalIntersectingTrianglesList[i].Length * 2, Allocator.TempJob);
            _edgesToRight[i] = new NativeHashMap<int, int>(_originalIntersectingTrianglesList[i].Length * 2, Allocator.TempJob);

            AddEdgeTrianglesAndEdgesJob _addEdgeTrianglesAndEdges = new AddEdgeTrianglesAndEdgesJob
            {
                sideTriangles = _leftPart.triangles[i],
                originalIndexesToSide = _originalIndexToLeft,
                edgeToSideVertex = _edgeVerticesToLeft[i],
                halfNewTriangles = _halfNewTrianglesLeft[i],
                edgesToLeft = _edgesToLeft[i],
                edgesToRight = _edgesToRight[i],
                previousVertexCount = _leftSideVertexCount
            };

            _handles.Add(_addEdgeTrianglesAndEdges.Schedule(_dependencies[i]));

            AddEdgeTrianglesJob _addEdgeTriangles = new AddEdgeTrianglesJob
            {
                sideTriangles = _rightPart.triangles[i],
                originalIndexesToSide = _originalIndexToRight,
                edgeToSideVertex = _edgeVerticesToRight[i],
                halfNewTriangles = _halfNewTrianglesRight[i]
            };

            _handles.Add(_addEdgeTriangles.Schedule(_dependencies[i]));
            
            _dependencies[i] = JobHandle.CombineDependencies(_handles[_handles.Length - 1], _handles[_handles.Length - 2]);
        }

        JobHandle.CompleteAll(_handles);
    }

    private void FillHoles()
    {
        _handles.Clear();

        //translate vertices coordinates to plane coordinates
        _edgeVerticesOnPlane = new NativeList<float2>[_subMeshCount];
        _edgeVertices = new NativeArray<VertexInfo>[_subMeshCount];
        for (int i = 0; i < _subMeshCount; i++)
        {
            _edgeVerticesOnPlane[i] = new NativeList<float2>(_intersectedEdges[i].Length, Allocator.TempJob);
            _edgeVerticesOnPlane[i].ResizeUninitialized(_intersectedEdges[i].Length);
            _edgeVertices[i] = _edgesToVertices[i].GetValueArray(Allocator.TempJob);

            TranslateCoordinatesToPlaneParallelJob _translateCoordinates = new TranslateCoordinatesToPlaneParallelJob
            {
                planeXAxis = _planeXAxis,
                planeYAxis = _planeYAxis,
                edgeVertices = _edgeVertices[i],
                edgeVerticesOnPlane = _edgeVerticesOnPlane[i].AsParallelWriter()
            };

            _handles.Add(_translateCoordinates.Schedule(_edgesToVertices[i].Length, (_intersectedEdges[i].Length / 10 + 1)));
            _dependencies[i] = _handles[_handles.Length - 1];
        }
        
        //sort edge vertices
        _sortedEdgeVertices = new NativeList<int>[_subMeshCount];
        for (int i = 0; i < _subMeshCount; i++)
        {
            _sortedEdgeVertices[i] = new NativeList<int>(_edgeVertices[i].Length, Allocator.TempJob);
            _sortedEdgeVertices[i].ResizeUninitialized(_edgeVertices[i].Length);
            SortEdgeVerticesParallelJob _sortVerticesJob = new SortEdgeVerticesParallelJob
            {
                edgeVerticesOnPlane = _edgeVerticesOnPlane[i],
                sortedEdgeVertices = _sortedEdgeVertices[i],
                edgesToLeft = _edgesToLeft[i],
                edgesToRight = _edgesToRight[i]
            };

            _handles.Add(_sortVerticesJob.Schedule(_dependencies[i]));
            _dependencies[i] = _handles[_handles.Length - 1];
        }

        //triangulate surface
        _cutSurfaceTriangles = new NativeList<int>[_subMeshCount];
        for (int i = 0; i < _subMeshCount; i++)
        {
            _cutSurfaceTriangles[i] = new NativeList<int>(Allocator.TempJob);

            TriangulateFrameJob _triangulateJob = new TriangulateFrameJob
            {
                edgesToLeft = _edgesToLeft[i],
                edgesToRight = _edgesToRight[i],
                sortedEdgeVertices = _sortedEdgeVertices[i].AsDeferredJobArray(),
                edgeVerticesOnPlane = _edgeVerticesOnPlane[i].AsDeferredJobArray(),
                cutSurfaceTriangles = _cutSurfaceTriangles[i]
            };

            _handles.Add(_triangulateJob.Schedule(_dependencies[i]));
            _dependencies[i] = _handles[_handles.Length - 1];
        }

        //write data to meshes
        for (int i = 0; i < _subMeshCount; i++)
        {
            CopyCutSurfaceTrianglesAndVertices _copyDataJobLeft = new CopyCutSurfaceTrianglesAndVertices
            {
                edgeVertices = _edgeVertices[i],
                cutSurfaceTriangles = _cutSurfaceTriangles[i].AsDeferredJobArray(),
                sideVertices = _leftPart.vertices,
                sideNormals = _leftPart.normals,
                sideUVs = _leftPart.uvs,
                sideTriangles = _leftPart.triangles[_subMeshCount],
                verticesStartCount = _leftPart.vertices.Length,
                normal = -_cuttingPlaneNormal,
                inverseOrder = true
            };

            _handles.Add(_copyDataJobLeft.Schedule(_dependencies[i]));
            _dependencies[i] = _handles[_handles.Length - 1];

            CopyCutSurfaceTrianglesAndVertices _copyDataJobRight = new CopyCutSurfaceTrianglesAndVertices
            {
                edgeVertices = _edgeVertices[i],
                cutSurfaceTriangles = _cutSurfaceTriangles[i].AsDeferredJobArray(),
                sideVertices = _rightPart.vertices,
                sideNormals = _rightPart.normals,
                sideUVs = _rightPart.uvs,
                sideTriangles = _rightPart.triangles[_subMeshCount],
                verticesStartCount = _rightPart.vertices.Length,
                normal = _cuttingPlaneNormal,
                inverseOrder = false
            };

            _handles.Add(_copyDataJobRight.Schedule(_dependencies[i]));
            _dependencies[i] = _handles[_handles.Length - 1];
        }

        JobHandle.CompleteAll(_handles);

        // foreach (var vert in _sortedEdgeVertices[0])
        // {
        //     print(vert);
        // }
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

        public GeneratedMesh(string meshName, int subMeshCount)
        {
            vertices = new NativeList<float3>(Allocator.Persistent);
            normals = new NativeList<float3>(Allocator.Persistent);
            uvs = new NativeList<float2>(Allocator.Persistent);
            triangles = new NativeList<int>[subMeshCount + 1];
            for (int i = 0; i < subMeshCount + 1; i++)
            {
                triangles[i] = new NativeList<int>(Allocator.Persistent);
            }
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

    [BurstCompile]
    public struct Edge : System.IEquatable<Edge>
    {
        public int a, b;

        public bool Equals(Edge other)
        {
            if (this.a == other.a)
            {
                if (this.b == other.b)
                    return true;
            }
            else
            {
                if (this.a == other.b)
                    if (this.b == other.a)
                        return true;
            }

            return false;
        }

        public override int GetHashCode() => a.GetHashCode() + b.GetHashCode();

        public bool Empty()
        {
            if (a == -1 || b == -1)
                return true;
            else
                return false;
        }
    }

    public struct HalfNewTriangle
    {
        public int a, b;
        public Edge c, d;
    }

    [BurstCompile]
    public struct CutTrianglesParallelJob : IJobParallelFor
    {
        [ReadOnly] public float3 planeCenter, planeNormal;
        [ReadOnly] public NativeArray<float3> vertices, normals;
        [ReadOnly] public NativeArray<float2> uvs;
        [ReadOnly] public NativeList<int> triangles;
        [ReadOnly] public NativeArray<int> sideIDs;

        [WriteOnly] public NativeHashMap<Edge, VertexInfo>.ParallelWriter edgesToVertices;
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
            else if (sideIDs[_bc.a] == sideIDs[_bc.b])
            {
                CutTriangle(_bc, _ca, _ab);
            }
            //check c-a
            else if (sideIDs[_ca.a] == sideIDs[_ca.b])
            {
                CutTriangle(_ca, _ab, _bc);
            }
        }

        private void CutTriangle(Edge solidEdge, Edge firstIntersected, Edge secondIntersected)
        {
            VertexInfo _newVertexOne = GetNewVertex(firstIntersected);
            VertexInfo _newVertexTwo = GetNewVertex(secondIntersected);

            //add vertices
            edgesToVertices.TryAdd(firstIntersected, _newVertexOne);
            edgesToVertices.TryAdd(secondIntersected, _newVertexTwo);

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
                        c = firstIntersected,
                        d = new Edge{a = -1, b = -1}
                    });
                    rightHalfTriangles.Enqueue(new HalfNewTriangle
                    {
                        a = solidEdge.a,
                        b = -1,
                        c = firstIntersected,
                        d = secondIntersected
                    });
                    leftHalfTriangles.Enqueue(new HalfNewTriangle
                    {
                        a = firstIntersected.b,
                        b = -1,
                        c = secondIntersected,
                        d = firstIntersected
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
                        c = firstIntersected,
                        d = new Edge{a = -1, b = -1}
                    });
                    leftHalfTriangles.Enqueue(new HalfNewTriangle
                    {
                        a = solidEdge.a,
                        b = -1,
                        c = firstIntersected,
                        d = secondIntersected
                    });
                    rightHalfTriangles.Enqueue(new HalfNewTriangle
                    {
                        a = firstIntersected.b,
                        b = -1,
                        c = secondIntersected,
                        d = firstIntersected
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
        [ReadOnly] public NativeArray<Edge> edges;
        [ReadOnly] public NativeHashMap<Edge, VertexInfo> edgesToVertices;

        [ReadOnly] public int startVertexCount;

        [WriteOnly] public NativeList<float3> sideVertices, sideNormals;
        [WriteOnly] public NativeList<float2> sideUVs;

        [WriteOnly] public NativeHashMap<Edge, int> edgeVerticesToSide;

        public void Execute()
        {
            for (int i = 0; i < edges.Length; i++)
            {
                //add to hash-map
                edgeVerticesToSide.TryAdd(edges[i], startVertexCount + i);

                //add vertex info
                var _vertex = edgesToVertices[edges[i]];
                sideVertices.Add(_vertex.vertex);
                sideNormals.Add(_vertex.normal);
                sideUVs.Add(_vertex.uv);
            }
        }
    }

    [BurstCompile]
    public struct AddEdgeTrianglesJob : IJob
    {
        [WriteOnly] public NativeList<int> sideTriangles;

        [ReadOnly] public NativeHashMap<int, int> originalIndexesToSide;
        [ReadOnly] public NativeHashMap<Edge, int> edgeToSideVertex;

        public NativeQueue<HalfNewTriangle> halfNewTriangles;

        private int _a, _b;

        public void Execute()
        {
            while (halfNewTriangles.Count > 0)
            {
                _a = _b = -1;

                var _hnTriangle = halfNewTriangles.Dequeue();

                sideTriangles.Add(originalIndexesToSide[_hnTriangle.a]);
                if (_hnTriangle.b != -1)
                {
                    sideTriangles.Add(originalIndexesToSide[_hnTriangle.b]);
                }
                _a = edgeToSideVertex[_hnTriangle.c];
                sideTriangles.Add(_a);
                if (!_hnTriangle.d.Empty())
                {
                    _b = edgeToSideVertex[_hnTriangle.d];
                    sideTriangles.Add(_b);
                }
            }
        }
    }


    [BurstCompile]
    public struct AddEdgeTrianglesAndEdgesJob : IJob
    {
        [WriteOnly] public NativeList<int> sideTriangles;

        [ReadOnly] public NativeHashMap<int, int> originalIndexesToSide;
        [ReadOnly] public NativeHashMap<Edge, int> edgeToSideVertex;

        public NativeQueue<HalfNewTriangle> halfNewTriangles;

        [WriteOnly] public NativeHashMap<int, int> edgesToLeft, edgesToRight;

        [ReadOnly] public int previousVertexCount;

        private int _a, _b;

        public void Execute()
        {
            while (halfNewTriangles.Count > 0)
            {
                _a = _b = -1;

                var _hnTriangle = halfNewTriangles.Dequeue();

                sideTriangles.Add(originalIndexesToSide[_hnTriangle.a]);
                if (_hnTriangle.b != -1)
                {
                    sideTriangles.Add(originalIndexesToSide[_hnTriangle.b]);
                }
                _a = edgeToSideVertex[_hnTriangle.c];
                sideTriangles.Add(_a);
                if (!_hnTriangle.d.Empty())
                {
                    _b = edgeToSideVertex[_hnTriangle.d];
                    sideTriangles.Add(_b);

                    // if we have 2 new vertices we add them to edges hash-maps
                    _a -= previousVertexCount;
                    _b -= previousVertexCount;
                    edgesToLeft.TryAdd(_a, _b);
                    edgesToRight.TryAdd(_b, _a);
                }
            }
        }
    }

    #endregion

    //
    //fill holes
    //
    #region FillHolesJobs

    [BurstCompile]
    public struct TranslateCoordinatesToPlaneParallelJob : IJobParallelFor
    {
        [ReadOnly] public float3 planeXAxis;
        [ReadOnly] public float3 planeYAxis;

        [ReadOnly] public NativeArray<VertexInfo> edgeVertices;
        [WriteOnly] public NativeArray<float2> edgeVerticesOnPlane;

        public void Execute(int index)
        {
            float _x = math.dot(planeXAxis, edgeVertices[index].vertex);
            float _y = math.dot(planeYAxis, edgeVertices[index].vertex);

            edgeVerticesOnPlane[index] = new float2(_x, _y);
        }
    }

    [BurstCompile]
    public struct SortEdgeVerticesParallelJob : IJob
    {
        public NativeList<float2> edgeVerticesOnPlane;
        public NativeList<int> sortedEdgeVertices;

        public NativeHashMap<int, int> edgesToLeft, edgesToRight;

        public void Execute()
        {
            for (int i = 0; i < edgeVerticesOnPlane.Length; i++)
            {
                var _vertex = edgeVerticesOnPlane[i];
                int _place = 0;
                int _doubles = 1;
                for (int j = 0; j < edgeVerticesOnPlane.Length; j++)
                {
                    if (i != j)
                    {
                        var _vertexB = edgeVerticesOnPlane[j];

                        if (_vertex.x > _vertexB.x)
                        {
                            _place++;
                        }
                        else if (_vertex.x == _vertexB.x)
                        {
                            if (_vertex.y < _vertexB.y)
                            {
                                _place++;
                            }
                            else if (_vertex.y == _vertexB.y)
                            {
                                //if we found doubled vertex we connect original vertex to doubled connected vertex
                                //check free side ou current vertex
                                if (!edgesToLeft.ContainsKey(i)) //if left free
                                {
                                    ConnectVertex(i, j, edgesToLeft);
                                }
                                if (!edgesToRight.ContainsKey(i)) //if right free
                                {
                                    ConnectVertex(i, j, edgesToRight);
                                }
                                //double found
                                _doubles++;
                            }
                        }
                    }
                }
                //store ids in place
                for (int p = 0; p < _doubles; p++)
                {
                    sortedEdgeVertices[_place + p] = i;
                }
            }
        }

        private void ConnectVertex(int vertexID, int doubleID, NativeHashMap<int, int> side)
        {
            int _connected;
            if (edgesToLeft.TryGetValue(doubleID, out _connected))
            {
                side.TryAdd(vertexID, _connected);
                //remove double connection
                edgesToLeft.Remove(doubleID);
            }
            else if (edgesToRight.TryGetValue(doubleID, out _connected))
            {
                side.TryAdd(vertexID, _connected);
                //remove double connection
                edgesToRight.Remove(doubleID);
            }
        }
    }

    [BurstCompile]
    public struct TriangulateFrameJob : IJob
    {
        public NativeHashMap<int, int> edgesToLeft, edgesToRight;

        [ReadOnly] public NativeArray<int> sortedEdgeVertices;
        [ReadOnly] public NativeArray<float2> edgeVerticesOnPlane;

        [WriteOnly] public NativeList<int> cutSurfaceTriangles;

        private int _tipIndex;

        public void Execute()
        {
            _tipIndex = 0;
            while (_tipIndex < (sortedEdgeVertices.Length - 1))
            {
                FindTriangle();
            }
        }

        private void FindTriangle()
        {
            //
            NativeHashMap<int, int> _edgesToLeft, _edgesToRight;

            int _tip = sortedEdgeVertices[_tipIndex];
            int _left, _right;

            if (!edgesToLeft.TryGetValue(_tip, out _left) || !edgesToRight.TryGetValue(_tip, out _right))
            {
                _tipIndex++;
                return;
            }

            //check sides
            if (edgeVerticesOnPlane[_left].y < edgeVerticesOnPlane[_right].y)
            {
                //swap
                int _buff = _left;
                _left = _right;
                _right = _buff;
                //swap
                _edgesToLeft = edgesToRight;
                _edgesToRight = edgesToLeft;
            }
            else
            {
                _edgesToLeft = edgesToLeft;
                _edgesToRight = edgesToRight;
            }

            //store triangle
            cutSurfaceTriangles.Add(_tip);
            cutSurfaceTriangles.Add(_left);
            cutSurfaceTriangles.Add(_right);
            //delete old edges
            _edgesToLeft.Remove(_tip);
            _edgesToRight.Remove(_tip);
            _edgesToRight.Remove(_left);
            _edgesToLeft.Remove(_right);
            //add new edges
            _edgesToLeft.TryAdd(_right, _left);
            _edgesToRight.TryAdd(_left, _right);
            //increase tip index
            _tipIndex++;
        }

        private int FindInnerVertex(int a, int b, int c, int boundIndex)
        {
            float2 _a = edgeVerticesOnPlane[a];
            float2 _b = edgeVerticesOnPlane[b];
            float2 _c = edgeVerticesOnPlane[c];

            for (int i = (_tipIndex + 1); i < boundIndex; i++)
            {
                if ((sortedEdgeVertices[i] != b) && (sortedEdgeVertices[i] != c))
                {
                    float2 _vert = edgeVerticesOnPlane[sortedEdgeVertices[i]];
                    float _edgeAB = (_a.x - _vert.x) * (_b.y - _a.y) - (_b.x - _a.x) * (_a.y - _vert.y);
                    float _edgeBC = (_b.x - _vert.x) * (_c.y - _b.y) - (_c.x - _b.x) * (_b.y - _vert.y);
                    float _edgeCA = (_c.x - _vert.x) * (_a.y - _c.y) - (_a.x - _c.x) * (_c.y - _vert.y);

                    if ((_edgeAB <= 0) && (_edgeBC <= 0) && (_edgeCA <= 0))
                    {
                        return sortedEdgeVertices[i];
                    }
                    else if ((_edgeAB >= 0) && (_edgeBC >= 0) && (_edgeCA >= 0))
                    {
                        return sortedEdgeVertices[i];
                    }
                }
            }

            return -1;
        }
    }

    [BurstCompile]
    public struct CopyCutSurfaceTrianglesAndVertices : IJob
    {
        [ReadOnly] public NativeArray<VertexInfo> edgeVertices;
        [ReadOnly] public NativeArray<int> cutSurfaceTriangles;

        [WriteOnly] public NativeList<float3> sideVertices, sideNormals;
        [WriteOnly] public NativeList<float2> sideUVs;
        [WriteOnly] public NativeList<int> sideTriangles;

        [ReadOnly] public float3 normal;
        [ReadOnly] public bool inverseOrder;

        [ReadOnly] public int verticesStartCount;

        public void Execute()
        {
            //copy vertices
            for (int i = 0; i < edgeVertices.Length; i++)
            {
                sideVertices.Add(edgeVertices[i].vertex);
                sideNormals.Add(normal);
                sideUVs.Add(edgeVertices[i].uv);
            }
            //copy triangles
            if (inverseOrder)
            {
                for (int i = (cutSurfaceTriangles.Length - 1); i >= 0; i--)
                {
                    sideTriangles.Add(cutSurfaceTriangles[i] + verticesStartCount);
                }
            }
            else
            {
                for (int i = 0; i < cutSurfaceTriangles.Length; i++)
                {
                    sideTriangles.Add(cutSurfaceTriangles[i] + verticesStartCount);
                }
            }
        }
    }

    #endregion
}
