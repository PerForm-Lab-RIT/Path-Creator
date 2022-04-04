using System.Collections.Generic;
using PathCreation.Utility;
using UnityEngine;




public class roadGen : MonoBehaviour
{

    [Header("Road settings")]
    public float roadWidth = .4f;
    [Range(0, .5f)]
    public float thickness = .15f;
    public float straightLegLength = 15;
    public float arcLengthM = 10.0f;
    public float circleRadiusM = 30.0f;
    public bool isLeftTurn = false;

    [SerializeField, HideInInspector]
    public bool flattenSurface = true;

    [Header("Material settings")]
    public Material roadMaterial;
    public Material undersideMaterial;
    //public float textureTiling; // seems like texture tiling of the same amount as straight leg length works


    [SerializeField, HideInInspector]
    GameObject meshHolder;

    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    Mesh mesh;


        
        
    [SerializeField, HideInInspector]
    public Vector3[] localPoints;
    [SerializeField, HideInInspector]
    public Vector3[] localTangents;
    [SerializeField, HideInInspector]
    public Vector3[] localNormals;


    /// Percentage along the path at each vertex (0 being start of path, and 1 being the end)
    [SerializeField, HideInInspector]
    public float[] times;

    /// Total distance between the vertices of the polyline
    [SerializeField, HideInInspector]
    public float length;

    /// Total distance from the first vertex up to each vertex in the polyline
    [SerializeField, HideInInspector]
    public float[] cumulativeLengthAtEachVertex;

    /// Bounding box of the path
    [SerializeField, HideInInspector]
    public Bounds bounds;

    /// Equal to (0,0,-1) for 2D paths, and (0,1,0) for XZ paths
    [SerializeField, HideInInspector]
    public Vector3 up;



    // Default values and constants:
    // const int accuracy = 10; // A scalar for how many times bezier path is divided when determining vertex positions
    // const float minVertexSpacing = .01f;


    [SerializeField, HideInInspector]
    public bool isClosedLoop = false;

            
    private void Start()
    {
        
        updatePoints();
        updateMesh();

    }

    public void OnValidate()
    {
        updatePoints();
        updateMesh();
    }

    public void updatePoints()
    {

        // how much of an angle you want to turn  in degrees
        float turnAngle = arcLengthM / circleRadiusM;
        if (turnAngle > Mathf.PI)
        {
            Debug.LogError("Arc Length divided by circle radius must be less than 180 degrees.");

        }

        float intervals = (turnAngle * Mathf.Rad2Deg) / (1.0f); // calculates number of vertices on the arc, spaced out by 1 degree
        int verticesPerMeter = Mathf.RoundToInt(intervals / arcLengthM); // we should calculate the spacing of the vertices, there should be more for longer arcLength (var interval increases with turnAngle)

        length = 2 * straightLegLength + arcLengthM;
        int numVertsOnAStraightLeg = Mathf.RoundToInt(straightLegLength * verticesPerMeter);
        int numVertsOnArc = Mathf.RoundToInt(arcLengthM * verticesPerMeter);
        int numVerts = 2 * numVertsOnAStraightLeg + numVertsOnArc;
        
        localPoints = new Vector3[numVerts];
        localNormals = new Vector3[numVerts];
        localTangents = new Vector3[numVerts];
        cumulativeLengthAtEachVertex = new float[numVerts];

        /// Percentage along the path at each vertex (0 being start of path, and 1 being the end)
        times = new float[numVerts];

        // The incoming straight leg of the road.

        Vector3 straightPathDir = new Vector3(0, 0, 1); // assuming coordinate system origin is "reset" before each straight path
        Vector3 startingPoint = new Vector3(0, 0, -straightLegLength);

        for (int i = 0; i < numVertsOnAStraightLeg; i++)
        {
            cumulativeLengthAtEachVertex[i] = i * (straightLegLength / numVertsOnAStraightLeg);
            float distanceOnLeg = straightLegLength * ((float)i / numVertsOnAStraightLeg);

            localPoints[i] = startingPoint + straightPathDir * distanceOnLeg;
            localTangents[i] = new Vector3(0, 0, 1);
            localNormals[i] = new Vector3(1, 0, 0);

            times[i] = cumulativeLengthAtEachVertex[i] / length; // tells you how far along the ENTIRE path you are (like a percentage of both straight segments and curve combined)
        }

        // The arc

        float rateOfChangeRads = turnAngle / numVertsOnArc;

        // move clockwise from pi by rateOfChangeRads * i for right turn
        // move counterclockwise for left
        if (isLeftTurn)
        {
            for (int i = numVertsOnAStraightLeg; i < numVertsOnAStraightLeg + numVertsOnArc; i++)
            {
                // circle center position along the local x axis
                Vector3 circleCenter = new Vector3(-circleRadiusM, 0, 0);
                float rad = rateOfChangeRads * (float)(i - numVertsOnAStraightLeg);
                // x,z location relative to the center of the circle
                Vector3 unshiftedPointOnCircle = circleRadiusM * new Vector3(Mathf.Cos(rad), 0, Mathf.Sin(rad));

                localPoints[i] = unshiftedPointOnCircle + circleCenter;
                localNormals[i] = -(circleCenter - localPoints[i]).normalized; //P2 - P1 gives direction, the normal is always the direction vector pointing towards the circle center -AG
                localTangents[i] = -Vector3.Cross(localNormals[i], Vector3.down); // tangent will be the result of the cross product between Vector up (left hand rule) and normal. Already normalized. -AG
                cumulativeLengthAtEachVertex[i] = straightLegLength + ((float)(i - numVertsOnAStraightLeg) * arcLengthM / numVertsOnArc);
                times[i] = cumulativeLengthAtEachVertex[i] / length;
            }
        }
        else
        {
            for (int i = numVertsOnAStraightLeg; i < numVertsOnAStraightLeg + numVertsOnArc; i++)
            {
                // circle center position along the local x axis
                Vector3 circleCenter = new Vector3(circleRadiusM, 0, 0);
                float rad = Mathf.PI - rateOfChangeRads * (float)(i - numVertsOnAStraightLeg);
                // x,z location relative to the center of the circle
                Vector3 unshiftedPointOnCircle = circleRadiusM * new Vector3(Mathf.Cos(rad), 0, Mathf.Sin(rad));

                localPoints[i] = unshiftedPointOnCircle + circleCenter;
                localNormals[i] = (circleCenter - localPoints[i]).normalized; //P2 - P1 gives direction, the normal is always the direction vector pointing towards the circle center -AG
                localTangents[i] = Vector3.Cross(localNormals[i], Vector3.up); // tangent will be the result of the cross product between Vector up (left hand rule) and normal. Already normalized. -AG
                cumulativeLengthAtEachVertex[i] = straightLegLength + ((float)(i - numVertsOnAStraightLeg) * arcLengthM / numVertsOnArc);
                times[i] = cumulativeLengthAtEachVertex[i] / length;
            }

        }

        Vector3 newStraightDir = localTangents[numVertsOnAStraightLeg + numVertsOnArc - 1];
        Vector3 newLocalTangent = newStraightDir;
        Vector3 newLocalNormal = localNormals[numVertsOnAStraightLeg + numVertsOnArc - 1];
        Vector3 newStartingPoint = localPoints[numVertsOnAStraightLeg + numVertsOnArc - 1];

        float j = 0;
        for (int i = numVertsOnArc + numVertsOnAStraightLeg; i < numVerts; i++)
        {
            cumulativeLengthAtEachVertex[i] = straightLegLength + arcLengthM + j * (straightLegLength / numVertsOnAStraightLeg); // don't think this is right anymore since we changed i
            float distanceOnLeg = straightLegLength * j / (float)numVertsOnAStraightLeg;
            //((float)(i - (numVertsOnArc + numVertsOnAStraightLeg)) / (float)numVertsOnAStraightLeg);

            localPoints[i] = newStartingPoint + newStraightDir * distanceOnLeg; // straight path direction is now in the direction of the last tangents on curve
            localTangents[i] = newLocalTangent;
            localNormals[i] = newLocalNormal;
            j++;

            times[i] = cumulativeLengthAtEachVertex[i] / length; // not sure what the point of this is? -AG
        }



        // Todo:  add final point ( the end of the exit leg along the final tangent )
    }

        

    //protected override void PathUpdated()
    void updateMesh()
    {
            
        AssignMeshComponents();
        AssignMaterials();
        CreateRoadMesh();
            
    }

    void CreateRoadMesh()
    {

        Vector3[] verts = new Vector3[NumPoints * 8];
        Vector2[] uvs = new Vector2[verts.Length];
        Vector3[] normals = new Vector3[verts.Length];

        int numTris = 2 * (NumPoints - 1) + ((isClosedLoop) ? 2 : 0);
        int[] roadTriangles = new int[numTris * 3];
        int[] underRoadTriangles = new int[numTris * 3];
        int[] sideOfRoadTriangles = new int[numTris * 2 * 3];

        int vertIndex = 0;
        int triIndex = 0;

        // Vertices for the top of the road are layed out:
        // 0  1
        // 8  9
        // and so on... So the triangle map 0,8,1 for example, defines a triangle from top left to bottom left to bottom right.
        int[] triangleMap = { 0, 8, 1, 1, 8, 9 };
        int[] sidesTriangleMap = { 4, 6, 14, 12, 4, 14, 5, 15, 7, 13, 15, 5 };

        bool usePathNormals = true;

        for (int i = 0; i < NumPoints; i++)
        {
            //Vector3 localUp = (usePathNormals) ? Vector3.Cross(vPath.GetTangent(i), vPath.GetNormal(i)) : vPath.up;
            //Vector3 localRight = (usePathNormals) ? vPath.GetNormal(i) : Vector3.Cross(localUp, vPath.GetTangent(i));

            Vector3 localUp = (usePathNormals) ? Vector3.Cross(GetTangent(i), GetNormal(i)) : up;
            Vector3 localRight = (usePathNormals) ? GetNormal(i) : Vector3.Cross(localUp, GetTangent(i));

            // Find position to left and right of current vPath vertex
            Vector3 vertSideA = GetPoint(i) - localRight * Mathf.Abs(roadWidth);
            Vector3 vertSideB = GetPoint(i) + localRight * Mathf.Abs(roadWidth);

            // Add top of road vertices
            verts[vertIndex + 0] = vertSideA;
            verts[vertIndex + 1] = vertSideB;
            // Add bottom of road vertices
            verts[vertIndex + 2] = vertSideA - localUp * thickness;
            verts[vertIndex + 3] = vertSideB - localUp * thickness;

            // Duplicate vertices to get flat shading for sides of road
            verts[vertIndex + 4] = verts[vertIndex + 0];
            verts[vertIndex + 5] = verts[vertIndex + 1];
            verts[vertIndex + 6] = verts[vertIndex + 2];
            verts[vertIndex + 7] = verts[vertIndex + 3];

            // Set uv on y axis to vPath time (0 at start of vPath, up to 1 at end of vPath)
            uvs[vertIndex + 0] = new Vector2(0, times[i]);
            uvs[vertIndex + 1] = new Vector2(1, times[i]);

            // Top of road normals
            normals[vertIndex + 0] = localUp;
            normals[vertIndex + 1] = localUp;
            // Bottom of road normals
            normals[vertIndex + 2] = -localUp;
            normals[vertIndex + 3] = -localUp;
            // Sides of road normals
            normals[vertIndex + 4] = -localRight;
            normals[vertIndex + 5] = localRight;
            normals[vertIndex + 6] = -localRight;
            normals[vertIndex + 7] = localRight;

            // Set triangle indices
            if (i < NumPoints - 1)
            {
                for (int j = 0; j < triangleMap.Length; j++)
                {
                    roadTriangles[triIndex + j] = (vertIndex + triangleMap[j]) % verts.Length;
                    // reverse triangle map for under road so that triangles wind the other way and are visible from underneath
                    underRoadTriangles[triIndex + j] = (vertIndex + triangleMap[triangleMap.Length - 1 - j] + 2) % verts.Length;
                }
                for (int j = 0; j < sidesTriangleMap.Length; j++)
                {
                    sideOfRoadTriangles[triIndex * 2 + j] = (vertIndex + sidesTriangleMap[j]) % verts.Length;
                }

            }

            vertIndex += 8;
            triIndex += 6;
        }

        mesh.Clear();
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.normals = normals;
        mesh.subMeshCount = 3;
        mesh.SetTriangles(roadTriangles, 0);
        mesh.SetTriangles(underRoadTriangles, 1);
        mesh.SetTriangles(sideOfRoadTriangles, 2);
        mesh.RecalculateBounds();
    }

    // Add MeshRenderer and MeshFilter components to this gameobject if not already attached
    void AssignMeshComponents()
    {

        meshHolder = this.gameObject;
        //if (meshHolder == null)
        //{
        //    meshHolder = new GameObject("Road Mesh Holder");
        //}

        meshHolder.transform.rotation = Quaternion.identity;
        meshHolder.transform.position = Vector3.zero;
        meshHolder.transform.localScale = Vector3.one;

        // Ensure mesh renderer and filter components are assigned
        if (!meshHolder.gameObject.GetComponent<MeshFilter>())
        {
            meshHolder.gameObject.AddComponent<MeshFilter>();
        }
        if (!meshHolder.GetComponent<MeshRenderer>())
        {
            meshHolder.gameObject.AddComponent<MeshRenderer>();
        }

        meshRenderer = meshHolder.GetComponent<MeshRenderer>();
        meshFilter = meshHolder.GetComponent<MeshFilter>();
        if (mesh == null)
        {
            mesh = new Mesh();
        }
        //meshFilter.sharedMesh = mesh;
    }

    void AssignMaterials()
    {
        float textureTiling = straightLegLength; // seems like this is a reasonable way to define the texture tiling value for it to look reasonable.

        if (roadMaterial != null && undersideMaterial != null)
        {
            meshRenderer.sharedMaterials = new Material[] { roadMaterial, undersideMaterial, undersideMaterial };
            meshRenderer.sharedMaterials[0].mainTextureScale = new Vector3(1, textureTiling);
        }
    }

    public int NumPoints
    {
        get
        {
            return localPoints.Length;
        }
    }

    public Vector3 GetTangent(int index)
    {
        return transform.rotation * localTangents[index];
   
    }

    public Vector3 GetNormal(int index)
    {

        return transform.rotation * localNormals[index];
    }

    public Vector3 GetPoint(int index)
    {
            
        return transform.TransformPoint(localPoints[index]);
    }

}
