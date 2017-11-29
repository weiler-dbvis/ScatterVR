using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Net.Mail;
using UnityEngine.UI;
using System.Linq;

public class Scatterplot : MonoBehaviour
{
    public GameObject controllerLeft;
    public GameObject controllerRight;


    private SteamVR_TrackedObject trackedObjLeft;
    private SteamVR_TrackedObject trackedObjRight;
    private SteamVR_Controller.Device deviceLeft;
    private SteamVR_Controller.Device deviceRight;

    

    private int controllerLeft_index = -1;
    private int controllerRight_index = -1;

    private bool rightTriggerHold = false;
    private bool leftTriggerHold = false;

    private bool movingCubeActive = false;
    private bool scalingCubeActive = false;
    private float initialScalingDistance;
    private Vector3 initialScaleOfCube;

    private GameObject scalingIndicator1, scalingIndicator2;
    private LineRenderer lineRenderer;

    public GameObject datapointPrefab;      // objects to display as datapoints in scatterplot (e.g. cubes, spheres)
    public Material datapointMaterial;      // material that datapoints should have
    public GameObject axisLabelPrefab;

    private string pathToPointsFileCSV;      // Path to file which contains points. Seperator: whitespace. Each row one point (x,y,z)

    private string[] axisnames;
    private string[] defaultAxisnames = {"X-Axis", "Y-Axis", "Z-Axis"};

    private bool showAxisLabels;
    private bool csvHasHeader;
    private char csvSeparator;

    private List<GameObject> currentMeshes = new List<GameObject>();


    // use this method to set up scatterplot
    private void config()
    {
        // First, attach this script to a transparent cube/object (in this object, the points will be shown later)

        // set path to CSV file (relative to Assets folder - or in build version main data folder)
        pathToPointsFileCSV = "/ScatterplotData/points.csv";

        // set if data contains header (used as axis descriptors if not overwritten in next line)
        csvHasHeader = false;

        // set axis names manually - uncomment to overwrite
        //axisnames = new string[] { "Custom-X-Axis", "Custom-Y-Axis", "Custom-Z-Axis"};

        // define seperator used in csv file -> e.g. ';' or ' '
        csvSeparator = ' ';

        // toggle axis labels on and off
        showAxisLabels = true;

        //set datapoints or materials by dragging them into the script in unity

        if (datapointPrefab == null)
        {
            datapointPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            datapointPrefab.transform.localScale = new Vector3(0.005f, 0.005f, 0.005f);
        }
        if (datapointMaterial == null)
        {
            datapointMaterial = datapointPrefab.GetComponent<Renderer>().material;
        }
    }

    void Awake()
    {
        if (controllerLeft == null)
        {
            controllerLeft = GameObject.Find("Controller (left)");
        }
        if (controllerRight == null)
        {
            controllerRight = GameObject.Find("Controller (right)");
        }
        try
        {
            trackedObjLeft = controllerLeft.GetComponent<SteamVR_TrackedObject>();
            trackedObjRight = controllerRight.GetComponent<SteamVR_TrackedObject>();
        }
        catch (Exception e)
        {
            // STEAM VR probably not running
            Debug.Log("SteamVR error");
        }


        setupScalingTools();
    }

    public void Start()
    {
        config();
        Vector3[] data = loadData(pathToPointsFileCSV, csvSeparator, csvHasHeader);
        setupAxis();
        visualizeData(data);
    }

    void Update()
    {
        if (scalingIndicator1.activeSelf && scalingIndicator2.activeSelf && lineRenderer.enabled)
        {
            lineRenderer.SetPosition(0, scalingIndicator1.transform.position);
            lineRenderer.SetPosition(1, scalingIndicator2.transform.position);
        }
    }

    void FixedUpdate()
    {
        //Vector3 forward = trackedObj.transform.TransformDirection(Vector3.forward);
        //ArrayList globalListeners = null;
        if (!trackedObjLeft.isActiveAndEnabled || !trackedObjRight.isActiveAndEnabled)
        {
            return;
        }

        controllerLeft_index = (int)trackedObjLeft.index;
        controllerRight_index = (int)trackedObjRight.index;

        deviceLeft = SteamVR_Controller.Input(controllerLeft_index);
        deviceRight = SteamVR_Controller.Input(controllerRight_index);


        if (deviceRight.GetPressUp(SteamVR_Controller.ButtonMask.Trigger))
        {
            rightTriggerHold = false;
        }
        if (deviceRight.GetPressDown(SteamVR_Controller.ButtonMask.Trigger))
        {
            rightTriggerHold = true;
        }


        if (deviceLeft.GetPressUp(SteamVR_Controller.ButtonMask.Trigger))
        {
            leftTriggerHold = false;
        }
        if (deviceLeft.GetPressDown(SteamVR_Controller.ButtonMask.Trigger))
        {
            leftTriggerHold = true;
        }

        if (scalingCubeActive && !(leftTriggerHold && rightTriggerHold))
            stopScalingCube();

        if (!scalingCubeActive && leftTriggerHold && rightTriggerHold)
            startScalingCube();

        if (!movingCubeActive && !scalingCubeActive && (leftTriggerHold || rightTriggerHold))
        {
            if (leftTriggerHold) startMovingCube(controllerLeft);
            if (rightTriggerHold) startMovingCube(controllerRight);
        }

        if (movingCubeActive && !leftTriggerHold && !rightTriggerHold)
            stopMovingCube();

        if (scalingCubeActive)
        {
            scaleCube();
        }
    }
    
    private void startScalingCube()
    {
        scalingCubeActive = true;
        initialScalingDistance = getScalingDistance();
        initialScaleOfCube = gameObject.transform.localScale;
        stopMovingCube();

        scalingIndicator1.SetActive(true);
        scalingIndicator2.SetActive(true);
        lineRenderer.enabled = true;
    }

    private void stopScalingCube()
    {
        scalingCubeActive = false;
        scalingIndicator1.SetActive(false);
        scalingIndicator2.SetActive(false);
        lineRenderer.enabled = false;
    }

    private void startMovingCube(GameObject controller)
    {
        movingCubeActive = true;
        gameObject.transform.parent = controller.transform;
    }

    private void stopMovingCube()
    {
        movingCubeActive = false;
        gameObject.transform.parent = null;
    }


    private float getScalingDistance()
    {
        return Vector3.Distance(scalingIndicator1.transform.position, scalingIndicator2.transform.position);
    }

    private void scaleCube()
    {
        float currentScaleingDistance = getScalingDistance();
        float scalelevel = currentScaleingDistance / initialScalingDistance;
        gameObject.transform.localScale = new Vector3(initialScaleOfCube.x * scalelevel, initialScaleOfCube.x * scalelevel, initialScaleOfCube.x * scalelevel);
    }

    private void setupScalingTools()
    {
        scalingIndicator1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        scalingIndicator1.transform.parent = trackedObjLeft.transform;
        scalingIndicator1.transform.position = trackedObjLeft.transform.position + 0.5f * scalingIndicator1.transform.forward;
        scalingIndicator1.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
        scalingIndicator1.SetActive(false);
        scalingIndicator1.name = "ScalingIndicator1";
        scalingIndicator2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        scalingIndicator2.transform.parent = trackedObjRight.transform;
        scalingIndicator2.transform.position = trackedObjRight.transform.position + 0.5f * scalingIndicator2.transform.forward;
        scalingIndicator2.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
        scalingIndicator2.SetActive(false);
        scalingIndicator2.name = "ScalingIndicator2";

        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.enabled = false;
        lineRenderer.startWidth = 0.01f;
        lineRenderer.endWidth = 0.01f;
        Material lineRendererMaterial = new Material(gameObject.GetComponent<MeshRenderer>().material);
        lineRendererMaterial.SetColor("_Color", new Color(0, 0, 1, 0.2f));
        lineRenderer.material = lineRendererMaterial;
    }



    private void visualizeData2(Vector3[] data)
    {
        Rigidbody body = this.gameObject.GetComponent<Rigidbody>();
        if (body != null) { body.angularVelocity = Vector3.zero; }
        this.transform.rotation = Quaternion.Euler(new Vector3(0, 0, 0));

        List<CombineInstance> combine = new List<CombineInstance>();

        int vertexCount = 0;


        for (int index = 0; index < data.Length; index++)
        {
            GameObject datapoint = Instantiate(datapointPrefab);
            datapoint.transform.parent = gameObject.transform;
            datapoint.transform.localPosition = new Vector3(data[index].x - 0.5f, data[index].y - 0.5f, data[index].z - 0.5f);

            MeshFilter dataPointMeshFilter = datapoint.GetComponent<MeshFilter>();

            if (vertexCount + dataPointMeshFilter.mesh.vertexCount > 65000 || index == data.Length - 1)
            {
                GameObject submesh = new GameObject();
                submesh.AddComponent<MeshFilter>();
                submesh.AddComponent<MeshRenderer>();
                submesh.GetComponent<Renderer>().sharedMaterial = datapointMaterial;
                submesh.GetComponent<Renderer>().materials[0] = datapointMaterial;
                submesh.name = "meshChunk";

                submesh.transform.parent = transform;
                submesh.transform.GetComponent<MeshFilter>().mesh = new Mesh();
                submesh.transform.GetComponent<MeshFilter>().mesh.CombineMeshes(combine.ToArray());
                combine = new List<CombineInstance>();
                currentMeshes.Add(submesh);

                vertexCount = 0;
            }

            vertexCount += dataPointMeshFilter.sharedMesh.vertexCount;
            CombineInstance combinInstance = new CombineInstance();
            combinInstance.mesh = dataPointMeshFilter.sharedMesh;
            combinInstance.transform = dataPointMeshFilter.transform.localToWorldMatrix;
            combine.Add(combinInstance);

            GameObject.Destroy(datapoint);
        }
    }

    public void visualizeData(Vector3[] data)
    {
        GameObject dataPointBrush = Instantiate(datapointPrefab);     // Instantiate the prefab only once in the beginning as this takes a little while.
        dataPointBrush.transform.parent = gameObject.transform;
        List<CombineInstance> combine = new List<CombineInstance>();

        MeshFilter dataPointMeshFilter = dataPointBrush.GetComponent<MeshFilter>();

        int vertexCount = 0;

        for (int index = 0; index < data.Length; index++)
        {
            dataPointBrush.transform.localPosition = new Vector3(data[index].x - 0.5f, data[index].y - 0.5f, data[index].z - 0.5f);// Put brush at correct position.
           
            if (vertexCount + dataPointMeshFilter.mesh.vertexCount > 65000 || index == data.Length - 1)
            {
                GameObject submesh = new GameObject();                                         // Create the gameobject of the combined Mesh
                submesh.AddComponent<MeshFilter>();
                submesh.AddComponent<MeshRenderer>();
                submesh.GetComponent<Renderer>().sharedMaterial = datapointMaterial;
                submesh.GetComponent<Renderer>().materials[0] = datapointMaterial;
                submesh.name = "meshChunk";
                submesh.transform.parent = transform;                                             // Put the new mesh in the correct position in your scene
                Mesh CombinedMesh = new Mesh();
                CombinedMesh.CombineMeshes(combine.ToArray(), true, true);
                submesh.GetComponent<MeshFilter>().mesh = CombinedMesh;
                combine = new List<CombineInstance>();
                vertexCount = 0;
                combine = new List<CombineInstance>();
                currentMeshes.Add(submesh);       // Array that saves all the combined meshes. Useful to delete them later.
            }

            vertexCount += dataPointMeshFilter.sharedMesh.vertexCount;
            CombineInstance combinInstance = new CombineInstance();
            combinInstance.mesh = dataPointMeshFilter.sharedMesh;
            combinInstance.transform = dataPointMeshFilter.transform.localToWorldMatrix;
            combine.Add(combinInstance);
        }
        DestroyImmediate(dataPointBrush);            // Destroy the brush.
    }

    public Vector3[] loadData(string pathToFile, char separator = ' ', bool hasHeader = true)
    {
        StreamReader myReader = new StreamReader(Application.streamingAssetsPath + pathToFile);
        List<Vector3> results = new List<Vector3>();

        if (!myReader.EndOfStream && hasHeader)
        {
            string[] header = myReader.ReadLine().Split(separator);
            if (header.Length != 3)
            {
                Debug.Log("Can't read header properly!");
            }
            else
            {
                defaultAxisnames = header;
            }
        }

        if (axisnames == null)
        {
            axisnames = defaultAxisnames;
        }

        while (!myReader.EndOfStream)
        {
            string[] values = myReader.ReadLine().Split(separator);
            if (values.Length != 3)
            {
                results.Add(new Vector3(0, 0, 0));
                Debug.Log("Dataset had entry with not exactly 3 values!");
            }
            else
            {
                results.Add(new Vector3(System.Convert.ToSingle(values[0]), System.Convert.ToSingle(values[1]), System.Convert.ToSingle(values[2])));
            }
        }

        return results.ToArray();
    }


    private void setupAxis()
    {
        Vector3 offsets = gameObject.GetComponent<Renderer>().bounds.size / 2;
        GameObject axisObj;

        // XAxis
        axisObj = Instantiate(axisLabelPrefab);
        axisObj.transform.parent = transform;
        axisObj.transform.localPosition = new Vector3(0,-offsets.y,-offsets.z);
        axisObj.GetComponent<TextMesh>().text = axisnames[0];

        axisObj = Instantiate(axisLabelPrefab);
        axisObj.transform.parent = transform;
        axisObj.transform.localPosition = new Vector3(0, offsets.y, -offsets.z);
        axisObj.transform.Rotate(new Vector3(0, 0, 1), 180);
        axisObj.GetComponent<TextMesh>().text = axisnames[0];

        axisObj = Instantiate(axisLabelPrefab);
        axisObj.transform.parent = transform;
        axisObj.transform.localPosition = new Vector3(0, -offsets.y, offsets.z);
        axisObj.transform.Rotate(new Vector3(0, 1, 0), 180);
        axisObj.GetComponent<TextMesh>().text = axisnames[0];

        axisObj = Instantiate(axisLabelPrefab);
        axisObj.transform.parent = transform;
        axisObj.transform.localPosition = new Vector3(0, offsets.y, offsets.z);
        axisObj.transform.Rotate(new Vector3(0, 1, 0), 180);
        axisObj.transform.Rotate(new Vector3(0, 0, 1), 180);
        axisObj.GetComponent<TextMesh>().text = axisnames[0];
        
        axisObj = Instantiate(axisLabelPrefab);
        axisObj.transform.parent = transform;
        axisObj.transform.localPosition = new Vector3(0, -offsets.y, -offsets.z);
        axisObj.transform.localEulerAngles = new Vector3(270, 180, 0);
        axisObj.GetComponent<TextMesh>().text = axisnames[0];

        axisObj = Instantiate(axisLabelPrefab);
        axisObj.transform.parent = transform;
        axisObj.transform.localPosition = new Vector3(0, offsets.y, -offsets.z);
   
        axisObj.transform.localEulerAngles = new Vector3(90,180,180);
        axisObj.GetComponent<TextMesh>().text = axisnames[0];

        axisObj = Instantiate(axisLabelPrefab);
        axisObj.transform.parent = transform;
        axisObj.transform.localPosition = new Vector3(0, -offsets.y, offsets.z);
        axisObj.transform.localEulerAngles = new Vector3(270, 0, 0);
        axisObj.GetComponent<TextMesh>().text = axisnames[0];

        axisObj = Instantiate(axisLabelPrefab);
        axisObj.transform.parent = transform;
        axisObj.transform.localPosition = new Vector3(0, offsets.y, offsets.z);
        axisObj.transform.localEulerAngles = new Vector3(90, 180, 0);
        axisObj.GetComponent<TextMesh>().text = axisnames[0];


        // YAxis
        axisObj = Instantiate(axisLabelPrefab);
        axisObj.transform.parent = transform;
        axisObj.transform.localPosition = new Vector3(-offsets.x, 0, -offsets.z);
        axisObj.transform.localEulerAngles = new Vector3(0, 0, 270);
        axisObj.GetComponent<TextMesh>().text = axisnames[1];

        axisObj = Instantiate(axisLabelPrefab);
        axisObj.transform.parent = transform;
        axisObj.transform.localPosition = new Vector3(offsets.x, 0, -offsets.z);
        axisObj.transform.localEulerAngles = new Vector3(0, 0, 90);
        axisObj.GetComponent<TextMesh>().text = axisnames[1];

        axisObj = Instantiate(axisLabelPrefab);
        axisObj.transform.parent = transform;
        axisObj.transform.localPosition = new Vector3(-offsets.x, 0, offsets.z);
        axisObj.transform.localEulerAngles = new Vector3(0, 180, 90);
        axisObj.GetComponent<TextMesh>().text = axisnames[1];

        axisObj = Instantiate(axisLabelPrefab);
        axisObj.transform.parent = transform;
        axisObj.transform.localPosition = new Vector3(offsets.x, 0, offsets.z);
        axisObj.transform.localEulerAngles = new Vector3(180, 0, 90);
        axisObj.GetComponent<TextMesh>().text = axisnames[1];

        axisObj = Instantiate(axisLabelPrefab);
        axisObj.transform.parent = transform;
        axisObj.transform.localPosition = new Vector3(-offsets.x, 0, -offsets.z);
        axisObj.transform.localEulerAngles = new Vector3(0, 90, 90);
        axisObj.GetComponent<TextMesh>().text = axisnames[1];

        axisObj = Instantiate(axisLabelPrefab);
        axisObj.transform.parent = transform;
        axisObj.transform.localPosition = new Vector3(-offsets.x, 0, offsets.z);

        axisObj.transform.localEulerAngles = new Vector3(180,270,90);
        axisObj.GetComponent<TextMesh>().text = axisnames[1];

        axisObj = Instantiate(axisLabelPrefab);
        axisObj.transform.parent = transform;
        axisObj.transform.localPosition = new Vector3(offsets.x, 0, -offsets.z);
        axisObj.transform.localEulerAngles = new Vector3(180, 90, 90);
        axisObj.GetComponent<TextMesh>().text = axisnames[1];

        axisObj = Instantiate(axisLabelPrefab);
        axisObj.transform.parent = transform;
        axisObj.transform.localPosition = new Vector3(offsets.x, 0, offsets.z);
        axisObj.transform.localEulerAngles = new Vector3(0, 270, 90);
        axisObj.GetComponent<TextMesh>().text = axisnames[1];



        // ZAxis
        axisObj = Instantiate(axisLabelPrefab);
        axisObj.transform.parent = transform;
        axisObj.transform.localPosition = new Vector3(-offsets.x, offsets.y, 0);
        axisObj.transform.localEulerAngles = new Vector3(180, 270, 0);
        axisObj.GetComponent<TextMesh>().text = axisnames[2];

        axisObj = Instantiate(axisLabelPrefab);
        axisObj.transform.parent = transform;
        axisObj.transform.localPosition = new Vector3(-offsets.x, -offsets.y, 0);
        axisObj.transform.localEulerAngles = new Vector3(0, 90, 0);
        axisObj.GetComponent<TextMesh>().text = axisnames[2];

        axisObj = Instantiate(axisLabelPrefab);
        axisObj.transform.parent = transform;
        axisObj.transform.localPosition = new Vector3(offsets.x, offsets.y, 0);
        axisObj.transform.localEulerAngles = new Vector3(0, 270, 180);
        axisObj.GetComponent<TextMesh>().text = axisnames[2];

        axisObj = Instantiate(axisLabelPrefab);
        axisObj.transform.parent = transform;
        axisObj.transform.localPosition = new Vector3(offsets.x, -offsets.y, 0);
        axisObj.transform.localEulerAngles = new Vector3(0, 270, 0);
        axisObj.GetComponent<TextMesh>().text = axisnames[2];

        axisObj = Instantiate(axisLabelPrefab);
        axisObj.transform.parent = transform;
        axisObj.transform.localPosition = new Vector3(-offsets.x, offsets.y, 0);
        axisObj.transform.localEulerAngles = new Vector3(90, 90, 0);
        axisObj.GetComponent<TextMesh>().text = axisnames[2];

        axisObj = Instantiate(axisLabelPrefab);
        axisObj.transform.parent = transform;
        axisObj.transform.localPosition = new Vector3(-offsets.x, -offsets.y, 0);
        axisObj.transform.localEulerAngles = new Vector3(270, 270, 0);
        axisObj.GetComponent<TextMesh>().text = axisnames[2];

        axisObj = Instantiate(axisLabelPrefab);
        axisObj.transform.parent = transform;
        axisObj.transform.localPosition = new Vector3(offsets.x, offsets.y, 0);
        axisObj.transform.localEulerAngles = new Vector3(90, 0, 90);
        axisObj.GetComponent<TextMesh>().text = axisnames[2];

        axisObj = Instantiate(axisLabelPrefab);
        axisObj.transform.parent = transform;
        axisObj.transform.localPosition = new Vector3(offsets.x, -offsets.y, 0);
        axisObj.transform.localEulerAngles = new Vector3(270, 90, 0);
        axisObj.GetComponent<TextMesh>().text = axisnames[2];

    }

}
