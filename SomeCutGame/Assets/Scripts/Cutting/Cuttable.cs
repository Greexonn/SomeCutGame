using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;

public class Cuttable : MonoBehaviour
{
    public Material cutMaterial;

    private Plane _cuttingPlane;

    private MeshFilter _meshFilter;
    private Mesh _mesh;


    private GeneratedMesh _leftPart;
    private GeneratedMesh _rightPart;

    private GeneratedMesh _originalGeneratedMesh;

    //lists
    List<Mesh> _createdMeshes;
    List<GeneratedMesh> _generatedMeshes;

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
        _originalGeneratedMesh = new GeneratedMesh();
        _mesh.GetVertices(_originalGeneratedMesh.vertices);
        _mesh.GetNormals(_originalGeneratedMesh.normals);
        _mesh.GetUVs(0, _originalGeneratedMesh.uvs);
        for (int i = 0; i < _mesh.subMeshCount; i++)
        {
            _originalGeneratedMesh.triangles.Add(new List<int>(_mesh.GetTriangles(i)));
        }
    }

    public async void CutAsync(Vector3 contactPoint, Vector3 planeNormal)
    {
        //create cutting plane
        _cuttingPlane = new Plane(transform.InverseTransformDirection(planeNormal), transform.InverseTransformPoint(contactPoint));

        //create new meshes
        _createdMeshes = new List<Mesh>();

        //create mesh containers
        _leftPart = new GeneratedMesh();
        _rightPart = new GeneratedMesh();
        _generatedMeshes = new List<GeneratedMesh>();

        //initiate list for triangles
        _triangleVertices = new List<Vector3>(3);
        _triangleNormals = new List<Vector3>(3);
        _triangleUVs = new List<Vector2>(3);

        //perform cut
        await Task.Run(() => SplitMesh());
        FillCut();
        FillHoles();

        //create new parts
        CreateNewObjects();
    }

    private void SplitMesh()
    {
        Dictionary<int, bool> _triangleIndexes = new Dictionary<int, bool>(3);
        List<int> _verticesIDs = new List<int>(3);

        //iterate throught all triangles and split mesh in two
        for (int i = 0; i < _originalGeneratedMesh.triangles.Count; i++)
        {
            for (int j = 0; j < _originalGeneratedMesh.triangles[i].Count; j += 3)
            {
                //get triangle and vertex side
                _triangleIndexes.Clear();
                _verticesIDs.Clear();
                for (int t = 0; t < 3; t++)
                {
                    _verticesIDs.Add(_originalGeneratedMesh.triangles[i][j + t]);
                    _triangleIndexes.Add(_verticesIDs[t], 
                                            _cuttingPlane.GetSide(_originalGeneratedMesh.vertices[_originalGeneratedMesh.triangles[i][j + t]]));
                }

                //check if whole triangle on the left side or on the right side or crossing the plane
                int _triangleType = 0;
                foreach (var key in _triangleIndexes.Keys)
                {
                    if (_triangleIndexes[key])
                        _triangleType++;
                    else
                        _triangleType--;
                }

                switch (_triangleType)
                {
                    case 3: //whole triangle on left side
                    {
                        _leftPart.AddTriangle(GetMeshTriangle(_verticesIDs.ToArray(), i));

                        break;
                    }
                    case -3: //whole triangle on right side
                    {
                        _rightPart.AddTriangle(GetMeshTriangle(_verticesIDs.ToArray(), i));

                        break;
                    }
                    default: //triangle crossing the plane
                    {

                        break;
                    }
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
        _generatedMeshes.Add(_leftPart);
        _generatedMeshes.Add(_rightPart);
        foreach (var genMesh in _generatedMeshes)
        {
            _createdMeshes.Add(genMesh.GetMesh());
        }

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
            _triangleVertices.Add(_originalGeneratedMesh.vertices[vertexID]);
            _triangleNormals.Add(_originalGeneratedMesh.normals[vertexID]);
            _triangleUVs.Add(_originalGeneratedMesh.uvs[vertexID]);
        }

        return new MeshTriangle(_triangleVertices.ToArray(), _triangleNormals.ToArray(), _triangleUVs.ToArray(), subMeshID);
    }
}
