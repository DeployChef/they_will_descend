using UnityEngine;
using System.Collections;
using System.Collections.Generic;
public class CalculateMeshHeight : MonoBehaviour
{
    public Mesh mesh;
    public float objectHeight;
    public Material mat;
    public int matIndex;

    void Start()
    {
        mesh = gameObject.GetComponent<MeshFilter>().mesh;
        mat = gameObject.GetComponent<MeshRenderer>().materials[matIndex];
    }

    void Update()
    {
        objectHeight = mesh.bounds.size.y;
        mat.SetFloat("_ObjectHeight", objectHeight);

    }
}
