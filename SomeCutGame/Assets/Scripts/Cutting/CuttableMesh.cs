using System.Collections.Generic;
using System.Diagnostics;
using Cutting.Data;
using Cutting.Jobs;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Cutting
{
    public class CuttableMesh : MonoBehaviour
    {
        public Material _cutMaterial;

        private Plane _cuttingPlane;

        private MeshFilter _meshFilter;
        private Mesh _mesh;
        private int _subMeshCount;


        private GeneratedMesh _leftPart;
        private GeneratedMesh _rightPart;

        private GeneratedMesh _originalGeneratedMesh;

        //lists
        private List<Mesh> _createdMeshes;
        private List<GeneratedMesh> _generatedMeshes;

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
        private NativeList<JobHandle> _handles, _dependencies;

        //
        private Vector3 _cuttingPlaneNormal;
        private Vector3 _cuttingPlaneCenter;

        private float3 _planeCenter;
        private float3 _planeNormal;
        private float3 _planeXAxis;
        private float3 _planeYAxis;

        //
        private bool _isPreSet;

        private Stopwatch _stopwatch = new Stopwatch();

        private void Start()
        {
            //
            _meshFilter = GetComponent<MeshFilter>();
            _mesh = _meshFilter.mesh;
            _subMeshCount = _mesh.subMeshCount;

            //
            if (_isPreSet) 
                return;
        
            _originalGeneratedMesh = new GeneratedMesh(_subMeshCount);
            //copy vertices, normals, uvs
            var verts = _mesh.vertices;
            var norms = _mesh.normals;
            var uvs = _mesh.uv;
            
            for (var i = 0; i < _mesh.vertexCount; i++)
            {
                _originalGeneratedMesh.vertices.Add(new float3(verts[i]));
                _originalGeneratedMesh.normals.Add(new float3(norms[i]));
                _originalGeneratedMesh.uvs.Add(new float2(uvs[i]));
            }
            //copy triangles
            for (var i = 0; i < _subMeshCount; i++)
            {
                var subTriangles = _mesh.GetTriangles(i);
                foreach (var t in subTriangles)
                {
                    _originalGeneratedMesh.triangles[i].Add(t);
                }
            }
        }

        private void SetGeneratedMesh(GeneratedMesh generatedMesh)
        {
            _isPreSet = true;

            _originalGeneratedMesh = generatedMesh;
        }

        private void OnDestroy()
        {
            _originalGeneratedMesh.Dispose();
        }

        public void Cut(Vector3 contactPoint, Vector3 planeNormal, Vector3 planeUp, Vector3 planeLeft)
        {
            _stopwatch.Reset();
            _stopwatch.Start();
            
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
            _leftPart = new GeneratedMesh(_subMeshCount);
            _rightPart = new GeneratedMesh(_subMeshCount);
            _generatedMeshes = new List<GeneratedMesh>();

            //perform cut
            AllocateTemporalContainers();
            Debug.Log($"Allocation time: {_stopwatch.ElapsedMilliseconds}");
            //
            SplitMesh();
            Debug.Log($"Split time: {_stopwatch.ElapsedMilliseconds}");
            FillCutEdge();
            Debug.Log($"Fill edge time: {_stopwatch.ElapsedMilliseconds}");
            FillHoles();
            Debug.Log($"Fill holes time: {_stopwatch.ElapsedMilliseconds}");
            //
            DisposeTemporalContainers();
            
            _stopwatch.Stop();
            
            Debug.Log($"Cut time: {_stopwatch.ElapsedMilliseconds}");

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

            for (var i = 0; i < _subMeshCount; i++)
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
            var verticesCount = _originalGeneratedMesh.vertices.Length;
            _sideIds = new NativeArray<int>(verticesCount, Allocator.TempJob);

            //get all vertices sides
            var getVertexesSideJob = new GetVertexesSideParallelJob
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

            var getVertexesSideJobHandle = getVertexesSideJob.Schedule(_originalGeneratedMesh.vertices.Length, (verticesCount / 10 + 1));
            _handles.Add(getVertexesSideJobHandle);

            //make hash-maps for triangle indexes and mesh data
            var setMeshAndHashMaps = new SetMehsDataAndHashMapsJob
            {
                sideData = _dataLeft,
                verticesSide = _leftPart.vertices,
                normalsSide = _leftPart.normals,
                uvsSide = _leftPart.uvs,
                originalIndexesToSide = _originalIndexToLeft
            };

            _handles.Add(setMeshAndHashMaps.Schedule(getVertexesSideJobHandle));

            setMeshAndHashMaps = new SetMehsDataAndHashMapsJob
            {
                sideData = _dataRight,
                verticesSide = _rightPart.vertices,
                normalsSide = _rightPart.normals,
                uvsSide = _rightPart.uvs,
                originalIndexesToSide = _originalIndexToRight
            };

            _handles.Add(setMeshAndHashMaps.Schedule(getVertexesSideJobHandle));

            //check triangles
            var dependency = JobHandle.CombineDependencies(_handles[_handles.Length - 1], _handles[_handles.Length - 2]);
            _triangleTypes = new NativeArray<int>[_subMeshCount];
            for (var i = 0; i < _subMeshCount; i++)
            {
                _triangleTypes[i] = new NativeArray<int>(_originalGeneratedMesh.triangles[i].Length / 3, Allocator.TempJob);

                var checkTrianglesParallelJob = new CheckTrianglesParallelJob
                {
                    sideIDs = _sideIds,
                    triangleIndexes = _originalGeneratedMesh.triangles[i].AsArray(),
                    triangleTypes = _triangleTypes[i]
                };

                _handles.Add(checkTrianglesParallelJob.Schedule(_triangleTypes[i].Length, _triangleTypes[i].Length / 10 + 1, dependency));
                _dependencies.Add(_handles[_handles.Length - 1]);
            }

            //reassign triangles
            //allocate indexes queue
            _triangleIndexesLeft = new NativeQueue<int>[_subMeshCount];
            _triangleIndexesRight = new NativeQueue<int>[_subMeshCount];
            _originalIntersectingTriangles = new NativeQueue<int>[_subMeshCount];

            for (var i = 0; i < _subMeshCount; i++)
            {
                _triangleIndexesLeft[i] = new NativeQueue<int>(Allocator.TempJob);
                _triangleIndexesRight[i] = new NativeQueue<int>(Allocator.TempJob);
                _originalIntersectingTriangles[i] = new NativeQueue<int>(Allocator.TempJob);

                var reassignTrianglesJob = new ReassignTrianglesJob
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
                _handles.Add(reassignTrianglesJob.Schedule(_triangleTypes[i].Length, (_triangleTypes[i].Length / 10 + 1), _dependencies[i]));
                _dependencies[i] = _handles[_handles.Length - 1];
            }

            var localDependencies = new NativeList<JobHandle>(Allocator.Persistent);
            localDependencies.AddRange(_dependencies.AsArray());
            _dependencies.Clear();
            //assign triangles to mesh
            for (var i = 0; i < _subMeshCount; i++)
            {
                var assignTriangles = new CopyTrianglesToListJob
                {
                    triangleIndexes = _triangleIndexesLeft[i],
                    listTriangles = _leftPart.triangles[i]
                };

                _handles.Add(assignTriangles.Schedule(localDependencies[i]));
                _dependencies.Add(_handles[_handles.Length - 1]);

                assignTriangles = new CopyTrianglesToListJob
                {
                    triangleIndexes = _triangleIndexesRight[i],
                    listTriangles = _rightPart.triangles[i]
                };

                _handles.Add(assignTriangles.Schedule(localDependencies[i]));
                _dependencies.Add(_handles[_handles.Length - 1]);
            }
            localDependencies.Dispose();
        }

        private void FillCutEdge()
        {
            var previousCombined = JobHandle.CombineDependencies(_dependencies);
            _dependencies.Clear();

            _originalIntersectingTrianglesList = new NativeList<int>[_subMeshCount];
            //copy intersected triangles to list so we can iterate in parallel
            for (var i = 0; i < _subMeshCount; i++)
            {
                _originalIntersectingTrianglesList[i] = new NativeList<int>(Allocator.TempJob);

                var copyTrianglesToList = new CopyTrianglesToListJob
                {
                    triangleIndexes = _originalIntersectingTriangles[i],
                    listTriangles = _originalIntersectingTrianglesList[i]
                };

                _handles.Add(copyTrianglesToList.Schedule(previousCombined));
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

                var cutTriangles = new CutTrianglesParallelJob
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

                _handles.Add(cutTriangles.Schedule(_originalIntersectingTrianglesList[i].Length / 3, (_originalIntersectingTrianglesList[i].Length / 3 / 10 + 1), _dependencies[i]));
                _dependencies[i] = _handles[_handles.Length - 1];
            }

            JobHandle.CompleteAll(_handles);
            _handles.Clear();
        
            //add new vertices and fill hash-maps
            _edgeVerticesToLeft = new NativeHashMap<Edge, int>[_subMeshCount];
            _edgeVerticesToRight = new NativeHashMap<Edge, int>[_subMeshCount];
            _intersectedEdges = new NativeArray<Edge>[_subMeshCount];
            var previousHandle = _dependencies[0];
            //we will need left side vertex count for farther calculations
            var leftSideVertexCount = _leftPart.vertices.Length;

            for (var i = 0; i < _subMeshCount; i++)
            {
                _edgeVerticesToLeft[i] = new NativeHashMap<Edge, int>(_edgesToVertices[i].Count(), Allocator.TempJob);
                _edgeVerticesToRight[i] = new NativeHashMap<Edge, int>(_edgesToVertices[i].Count(), Allocator.TempJob);

                _intersectedEdges[i] = _edgesToVertices[i].GetKeyArray(Allocator.TempJob);

                var addEdgeVertices = new AddEdgeVerticesJob
                {
                    edges = _intersectedEdges[i],
                    edgesToVertices = _edgesToVertices[i],
                    startVertexCount = leftSideVertexCount,
                    sideVertices = _leftPart.vertices,
                    sideNormals = _leftPart.normals,
                    sideUVs = _leftPart.uvs,
                    edgeVerticesToSide = _edgeVerticesToLeft[i]
                };

                _handles.Add(addEdgeVertices.Schedule(previousHandle));

                addEdgeVertices = new AddEdgeVerticesJob
                {
                    edges = _intersectedEdges[i],
                    edgesToVertices = _edgesToVertices[i],
                    startVertexCount = _rightPart.vertices.Length,
                    sideVertices = _rightPart.vertices,
                    sideNormals = _rightPart.normals,
                    sideUVs = _rightPart.uvs,
                    edgeVerticesToSide = _edgeVerticesToRight[i]
                };

                _handles.Add(addEdgeVertices.Schedule(previousHandle));

                _dependencies[i] = JobHandle.CombineDependencies(_handles[_handles.Length - 1], _handles[_handles.Length - 2]);
                previousHandle = _dependencies[i];
            }
            
            //add new triangles and edges
            _edgesToLeft = new NativeHashMap<int, int>[_subMeshCount];
            _edgesToRight = new NativeHashMap<int, int>[_subMeshCount];
            for (int i = 0; i < _subMeshCount; i++)
            {
                _edgesToLeft[i] = new NativeHashMap<int, int>(_originalIntersectingTrianglesList[i].Length * 2, Allocator.TempJob);
                _edgesToRight[i] = new NativeHashMap<int, int>(_originalIntersectingTrianglesList[i].Length * 2, Allocator.TempJob);

                var addEdgeTrianglesAndEdges = new AddEdgeTrianglesAndEdgesJob
                {
                    sideTriangles = _leftPart.triangles[i],
                    originalIndexesToSide = _originalIndexToLeft,
                    edgeToSideVertex = _edgeVerticesToLeft[i],
                    halfNewTriangles = _halfNewTrianglesLeft[i],
                    edgesToLeft = _edgesToLeft[i],
                    edgesToRight = _edgesToRight[i],
                    previousVertexCount = leftSideVertexCount
                };

                _handles.Add(addEdgeTrianglesAndEdges.Schedule(_dependencies[i]));

                var addEdgeTriangles = new AddEdgeTrianglesJob
                {
                    sideTriangles = _rightPart.triangles[i],
                    originalIndexesToSide = _originalIndexToRight,
                    edgeToSideVertex = _edgeVerticesToRight[i],
                    halfNewTriangles = _halfNewTrianglesRight[i]
                };

                _handles.Add(addEdgeTriangles.Schedule(_dependencies[i]));
            
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
            for (var i = 0; i < _subMeshCount; i++)
            {
                _edgeVerticesOnPlane[i] = new NativeList<float2>(_intersectedEdges[i].Length, Allocator.TempJob);
                _edgeVerticesOnPlane[i].ResizeUninitialized(_intersectedEdges[i].Length);
                _edgeVertices[i] = _edgesToVertices[i].GetValueArray(Allocator.TempJob);

                var translateCoordinates = new TranslateCoordinatesToPlaneParallelJob
                {
                    planeXAxis = _planeXAxis,
                    planeYAxis = _planeYAxis,
                    edgeVertices = _edgeVertices[i],
                    edgeVerticesOnPlane = _edgeVerticesOnPlane[i].AsDeferredJobArray()
                };

                _handles.Add(translateCoordinates.Schedule(_edgesToVertices[i].Count(), (_intersectedEdges[i].Length / 10 + 1)));
                _dependencies[i] = _handles[_handles.Length - 1];
            }
        
            //sort edge vertices
            _sortedEdgeVertices = new NativeList<int>[_subMeshCount];
            for (var i = 0; i < _subMeshCount; i++)
            {
                _sortedEdgeVertices[i] = new NativeList<int>(_edgeVertices[i].Length, Allocator.TempJob);
                _sortedEdgeVertices[i].ResizeUninitialized(_edgeVertices[i].Length);
                var sortVerticesJob = new SortEdgeVerticesParallelJob
                {
                    edgeVerticesOnPlane = _edgeVerticesOnPlane[i],
                    sortedEdgeVertices = _sortedEdgeVertices[i],
                    edgesToLeft = _edgesToLeft[i],
                    edgesToRight = _edgesToRight[i]
                };

                _handles.Add(sortVerticesJob.Schedule(_dependencies[i]));
                _dependencies[i] = _handles[_handles.Length - 1];
            }

            //triangulate surface
            _cutSurfaceTriangles = new NativeList<int>[_subMeshCount];
            for (var i = 0; i < _subMeshCount; i++)
            {
                _cutSurfaceTriangles[i] = new NativeList<int>(Allocator.TempJob);

                var triangulateJob = new TriangulateFrameJob
                {
                    edgesToLeft = _edgesToLeft[i],
                    edgesToRight = _edgesToRight[i],
                    sortedEdgeVertices = _sortedEdgeVertices[i].AsDeferredJobArray(),
                    edgeVerticesOnPlane = _edgeVerticesOnPlane[i].AsDeferredJobArray(),
                    cutSurfaceTriangles = _cutSurfaceTriangles[i]
                };

                _handles.Add(triangulateJob.Schedule(_dependencies[i]));
                _dependencies[i] = _handles[_handles.Length - 1];
            }

            //write data to meshes
            for (var i = 0; i < _subMeshCount; i++)
            {
                var copyDataJobLeft = new CopyCutSurfaceTrianglesAndVerticesJob
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

                _handles.Add(copyDataJobLeft.Schedule(_dependencies[i]));
                _dependencies[i] = _handles[_handles.Length - 1];

                var copyDataJobRight = new CopyCutSurfaceTrianglesAndVerticesJob
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

                _handles.Add(copyDataJobRight.Schedule(_dependencies[i]));
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
            var materials = new List<Material>(gameObject.GetComponent<MeshRenderer>().materials);
            materials.Add(_cutMaterial != null ? _cutMaterial : materials[0]);
            gameObject.GetComponent<MeshRenderer>().materials = materials.ToArray();
            Destroy(GetComponent<Collider>());
            gameObject.AddComponent<MeshCollider>().convex = true;
            var currentRb = gameObject.GetComponent<Rigidbody>();
            if (currentRb == null)
            {
                currentRb = gameObject.AddComponent<Rigidbody>();
            }
            currentRb.ResetCenterOfMass();
            _createdMeshes.Remove(_createdMeshes[0]);
            _generatedMeshes.Remove(_generatedMeshes[0]);

            //create new objects
            for (var i = 0; i < _createdMeshes.Count; i++)
            {
                var part = new GameObject(gameObject.name + "_part");

                var localTransform = transform;
                part.transform.SetPositionAndRotation(localTransform.position, localTransform.rotation);
                var partMeshFilter = part.AddComponent<MeshFilter>();
                partMeshFilter.mesh = _createdMeshes[i];
                var partRenderer = part.AddComponent<MeshRenderer>();
                //set materials
                partRenderer.materials = materials.ToArray();

                part.AddComponent<MeshCollider>().convex = true;
                part.AddComponent<Rigidbody>();
            
                //
                var partCuttableMeshComponent = part.AddComponent<CuttableMesh>();
                partCuttableMeshComponent.SetGeneratedMesh(_generatedMeshes[i]);
                partCuttableMeshComponent._cutMaterial = materials[materials.Count - 1];
            }
        }
    }
}
