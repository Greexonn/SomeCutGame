using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;

public class CuttableGraph : MonoBehaviour
{
    public Material cutMaterial;

    private Plane _cuttingPlane;

    private MeshFilter _meshFilter;
    private Mesh _mesh;


    private MeshTriangleGraph _originalGraphMesh;

    //lists
    List<Mesh> _createdMeshes;

    //mesh data lists
    private List<Vector3> _vertices;
    private List<Vector3> _normals;
    private List<Vector2> _uvs;
    private List<int[]> _triangles;

    //triangle lists
    private List<Vector3> _triangleVertices;
    private List<Vector3> _triangleNormals;
    private List<Vector2> _triangleUVs;

    void Start()
    {
        //
        _meshFilter = GetComponent<MeshFilter>();
        _mesh = _meshFilter.mesh;

        //
        _vertices = new List<Vector3>();
        _normals = new List<Vector3>();
        _uvs = new List<Vector2>();
        _triangles = new List<int[]>();
        _mesh.GetVertices(_vertices);
        _mesh.GetNormals(_normals);
        _mesh.GetUVs(0, _uvs);
        for (int i = 0; i < _mesh.subMeshCount; i++)
        {
            _triangles.Add(_mesh.GetTriangles(i));
        }

        print(Time.time);
        GenerateGraphAsync();
    }

    private async void GenerateGraphAsync()
    {
        await Task.Run(() => GenerateGraph());
        print(Time.time);
    }

    private void GenerateGraph()
    {
        _originalGraphMesh = new MeshTriangleGraph(_vertices.ToArray(), _normals.ToArray(), _uvs.ToArray(), _triangles.ToArray());
    }

    public async void CutAsync(Vector3 contactPoint, Vector3 planeNormal)
    {
        //create cutting plane
        _cuttingPlane = new Plane(transform.InverseTransformDirection(planeNormal), transform.InverseTransformPoint(contactPoint));

        //create new meshes
        _createdMeshes = new List<Mesh>();

        //initiate list for triangles
        _triangleVertices = new List<Vector3>(3);
        _triangleNormals = new List<Vector3>(3);
        _triangleUVs = new List<Vector2>(3);

        //perform cut
        await Task.Run(() => SplitMesh());
        FillCut();
        FillHoles();

        //create new parts
        // CreateNewObjects();
    }

    private void SplitMesh()
    {
        List<bool> _triangleIndexes = new List<bool>(3);
        List<int> _verticesIDs = new List<int>(3);

        // iterate throught all triangles and split mesh in two
        for (int i = 0; i < _originalGraphMesh.triangles.Count; i += 3)
        {
            //get triangle and vertex side
            _triangleIndexes.Clear();
            _verticesIDs.Clear();
            for (int t = 0; t < 3; t++)
            {
                _verticesIDs.Add(_originalGraphMesh.triangles[i + t].vertexID);
                _triangleIndexes.Add(_cuttingPlane.GetSide(_originalGraphMesh.vertices[_verticesIDs[t]]));
            }

            //check if whole triangle on the left side or on the right side or crossing the plane
            int _triangleType = 0;
            foreach (var vert in _triangleIndexes)
            {
                if (vert)
                    _triangleType++;
                else
                    _triangleType--;
            }

            print(_triangleType);
            switch (_triangleType)
            {
                case 3: //whole triangle on left side
                {

                    break;
                }
                case -3: //whole triangle on right side
                {

                    break;
                }
                default: //triangle crossing the plane
                {
                    Debug.Log("interseption found");
                    break;
                }
            }
        }
    }

    private void SeparateParts()
    {

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
        // _generatedMeshes.Add(_leftPart);
        // _generatedMeshes.Add(_rightPart);
        // foreach (var genMesh in _generatedMeshes)
        // {
        //     _createdMeshes.Add(genMesh.GetMesh());
        // }

        //change mesh on current object
        _meshFilter.mesh = _createdMeshes[0];
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

        //create new objects
        foreach (var createdMesh in _createdMeshes)
        {
            GameObject _part = new GameObject(gameObject.name + "_part");
            _part.transform.SetPositionAndRotation(transform.position, transform.rotation);
            MeshFilter _partMeshFilter = _part.AddComponent<MeshFilter>();
            _partMeshFilter.mesh = createdMesh;
            MeshRenderer _partRenderer = _part.AddComponent<MeshRenderer>();
            //set materials
            _partRenderer.materials = _materials.ToArray();

            _part.AddComponent<MeshCollider>().convex = true;
            Rigidbody _partRigidbody = _part.AddComponent<Rigidbody>();

            //
            Cuttable _partCuttableComponent = _part.AddComponent<Cuttable>();
            _partCuttableComponent.cutMaterial = _materials[_materials.Count - 1];
        }
    }

    private MeshTriangle GetMeshTriangle(int[] verticesIDs, int subMeshID)
    {
        //clear previous data
        _triangleVertices.Clear();
        _triangleNormals.Clear();
        _triangleUVs.Clear();

        foreach (var vertexID in verticesIDs)
        {
            // _triangleVertices.Add(_originalGeneratedMesh.vertices[vertexID]);
            // _triangleNormals.Add(_originalGeneratedMesh.normals[vertexID]);
            // _triangleUVs.Add(_originalGeneratedMesh.uvs[vertexID]);
        }

        return new MeshTriangle(_triangleVertices.ToArray(), _triangleNormals.ToArray(), _triangleUVs.ToArray(), subMeshID);
    }
}
