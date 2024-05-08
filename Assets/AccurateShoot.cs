using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ViveSR.anipal.Eye;
using UnityEngine.XR.Interaction.Toolkit;
using System.IO;
using UnityEngine.SceneManagement;

// AddComponentMenu allows easy access to the script from the Unity editor
[AddComponentMenu("Script")]
public class AccurateShoot : MonoBehaviour
{
    // Prefab references for bullet, casing, and muzzle flash
    public GameObject bulletPrefab;
    public GameObject casingPrefab;
    public GameObject muzzleFlashPrefab;

    // References to various locations and components
    [SerializeField] private Animator gunAnimator;
    [SerializeField] private Transform barrelLocation;
    [SerializeField] private Transform casingExitLocation;
    [Tooltip("Specify time to destroy the casing object")] [SerializeField] private float destroyTimer = 1f;
    [Tooltip("Bullet Speed")] [SerializeField] private float shotPower = 500f;
    [Tooltip("Casing Ejection Speed")] [SerializeField] private float ejectPower = 150f;

    // Target-related variables
    public List<Transform> targets; 
    public Material targetMaterial;
    private Transform selectedTarget; 

    // Eye positions and status
    private Vector3 leftEyePosition = Vector3.zero;
    private Vector3 rightEyePosition = Vector3.zero;
    private Vector3 combinedEyePosition = Vector3.zero; 
    public AudioSource source;
    public AudioClip fireSound;
    public XRGrabInteractable grabInteractable; 

    // Calibration and eye dominance variables
    private int shotCount = 0;
    private const int calibrationShots = 5;
    private string determinedDominantEye = "Inconclusive";
    public Transform XRRig; 

    // Data structure for storing shot information
    [System.Serializable]
    public class ShotData
    {
        // Shot information variables
        public int shotNumber;
        public string selectedTargetID;
        public string selectedTargetPosition;
        public string crosshairPosition;
        public string crosshairRotation;
        public string leftEyePosition;
        public string rightEyePosition;
        public string combinedEyePosition;
        public bool leftEyeOpen;
        public bool rightEyeOpen;
        public string XRPosition;
        public string XRRotation;
        public bool hit;
        public float accuracy;
        public string dominantEyeForShot;
        public float leftEyeDistanceFromRay;
        public float rightEyeDistanceFromRay;
    }
    // List to store all shot data
    List<ShotData> allShotsData = new List<ShotData>();

    // Start is called before the first frame update
    void Start()
    {
        // Retrieve determined dominant eye from player preferences
        determinedDominantEye = PlayerPrefs.GetString("DominantEye", "Inconclusive");
        
        // Set default locations if not assigned
        if (barrelLocation == null)
            barrelLocation = transform;
        if (gunAnimator == null)
            gunAnimator = GetComponentInChildren<Animator>();

        // Start by selecting a target and simulating grab interaction
        SelectTarget();
        var controllers = FindObjectsOfType<XRBaseController>();
        foreach (var controller in controllers)
        {
            if (controller.enableInputActions)
            {
                SimulateGrab(controller);
                break;
            }
        }
    }

    // Simulate grab interaction
    void SimulateGrab(XRBaseController controller)
    {
        var interactor = controller.GetComponent<XRBaseInteractor>();
        if (interactor != null && grabInteractable != null)
        {
            interactor.StartManualInteraction((IXRSelectInteractable)grabInteractable);
        }
    }

    // Pull trigger function to initiate firing
    public void PullTrigger()
    {
        gunAnimator.SetTrigger("Fire");
    }

    // Update is called once per frame
    void Update()
    {
        VisualizeAiming();
        UpdateEyeGaze();
        UpdateEyeOpenness();
    }

    // Update eye gaze based on SRanipal eye data
    private void UpdateEyeGaze()
    {
        if (SRanipal_Eye_Framework.Status == SRanipal_Eye_Framework.FrameworkStatus.WORKING)
        {
            Vector3 localLeftEyePosition, localRightEyePosition;
            if (SRanipal_Eye_v2.GetGazeRay(GazeIndex.LEFT, out localLeftEyePosition, out _) &&
                SRanipal_Eye_v2.GetGazeRay(GazeIndex.RIGHT, out localRightEyePosition, out _))
            {
                leftEyePosition = mainCamera.TransformPoint(localLeftEyePosition);
                rightEyePosition = mainCamera.TransformPoint(localRightEyePosition);
                combinedEyePosition = (leftEyePosition + rightEyePosition) / 2;
            }
        }
    }

    // Update eye openness based on SRanipal eye data
    private void UpdateEyeOpenness()
    {
        float leftEyeOpenness;
        float rightEyeOpenness;
        SRanipal_Eye_v2.GetEyeOpenness(EyeIndex.LEFT, out leftEyeOpenness);
        SRanipal_Eye_v2.GetEyeOpenness(EyeIndex.RIGHT, out rightEyeOpenness);
        const float eyeClosedThreshold = 0.1f;
        isLeftEyeOpen = leftEyeOpenness > eyeClosedThreshold;
        isRightEyeOpen = rightEyeOpenness > eyeClosedThreshold;
    }

    // Visualize aiming rays for left, right, and combined eyes
    private void VisualizeAiming()
    {
        Vector3 leftAimDirection = (crosshair.position - leftEyePosition).normalized;
        Debug.DrawRay(leftEyePosition, leftAimDirection * aimLineLength, Color.red);
        Vector3 rightAimDirection = (crosshair.position - rightEyePosition).normalized;
        Debug.DrawRay(rightEyePosition, rightAimDirection * aimLineLength, Color.green);
        Vector3 combinedAimDirection = (crosshair.position - combinedEyePosition).normalized;
        Debug.DrawRay(combinedEyePosition, combinedAimDirection * aimLineLength, Color.blue);
    }

    // Select a random target
    private void SelectTarget()
    {
        foreach (Transform target in targets)
        {
            SetEmission(target, false);
        }
        int randomIndex = Random.Range(0, targets.Count);
        selectedTarget = targets[randomIndex];
        SetEmission(targets[randomIndex], true);
    }

    // Toggle emission of the target's material
    private void SetEmission(Transform target, bool shouldEmit)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material[] mats = renderer.materials;
            int targetMaterialIndex = -1;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i].name.StartsWith("Target Paper"))
                {
                    targetMaterialIndex = i;
                    break;
                }
            }
            if (targetMaterialIndex != -1)
            {
                if (shouldEmit)
                {
                    mats[targetMaterialIndex].EnableKeyword("_EMISSION");
                    mats[targetMaterialIndex].SetColor("_EmissionColor", Color.red);
                }
                else
                {
                    mats[targetMaterialIndex].DisableKeyword("_EMISSION");
                }
                renderer.materials = mats;
            }
        }
    }

    // Shoot function
    void Shoot()
    {
        source.PlayOneShot(fireSound);
        if (muzzleFlashPrefab)
        {
            var tempFlash = Instantiate(muzzleFlashPrefab, barrelLocation.position, barrelLocation.rotation);
            Destroy(tempFlash, destroyTimer);
        }
        if (!bulletPrefab) return;
        var bulletInstance = Instantiate(bulletPrefab, barrelLocation.position, barrelLocation.rotation).GetComponent<Rigidbody>();
        bulletInstance.AddForce(barrelLocation.forward * shotPower);
        ShotData currentShot = new ShotData
        {
            shotNumber = ++shotCount,
            selectedTargetID = selectedTarget.name,
            selectedTargetPosition = selectedTarget.position.ToString(),
            crosshairPosition = crosshair.position.ToString(),
            crosshairRotation = crosshair.rotation.eulerAngles.ToString(),
            leftEyePosition = leftEyePosition.ToString(),
            rightEyePosition = rightEyePosition.ToString(),
            combinedEyePosition = ((leftEyePosition + rightEyePosition) / 2).ToString(),
            leftEyeOpen = isLeftEyeOpen,
            rightEyeOpen = isRightEyeOpen,
            XRPosition = XRRig.position.ToString(),
            XRRotation = XRRig.rotation.eulerAngles.ToString()
        };
        Vector3 shootOrigin = DetermineShootOrigin(currentShot);
        RaycastHit hit;
        bool hitDetected = Physics.Raycast(shootOrigin, (crosshair.position - shootOrigin).normalized, out hit, Mathf.Infinity);
        if (hitDetected && (hit.transform == selectedTarget || hit.transform.IsChildOf(selectedTarget)))
        {
            currentShot.hit = true;
            HandleHit(hit, currentShot, true, shootOrigin);
        }
        else
        {
            Debug.LogWarning("Missed all targets.");
            currentShot.hit = false;
            RaycastHit dummyHit = new RaycastHit();
            dummyHit.point = shootOrigin + (crosshair.position - shootOrigin).normalized * 100;
            HandleHit(dummyHit, currentShot, false, shootOrigin);
            SelectTarget();
        }
    }

    // Determine shoot origin based on eye openness and dominance
    private Vector3 DetermineShootOrigin(ShotData current)
    {
        if (isRightEyeOpen && isLeftEyeOpen)
        {
            if (determinedDominantEye == "Right")
            {
                current.dominantEyeForShot = "Right";
                return rightEyePosition;
            }
            else if (determinedDominantEye == "Left")
            {
                current.dominantEyeForShot = "Left";
                return leftEyePosition;
            }
            else
            {
                current.dominantEyeForShot = "Cyclopean";
                return (leftEyePosition + rightEyePosition) / 2;
            }
        }
        else if (!isLeftEyeOpen && isRightEyeOpen)
        {
            current.dominantEyeForShot = "Right";
            return rightEyePosition;
        }
        else if (isLeftEyeOpen && !isRightEyeOpen)
        {
            current.dominantEyeForShot = "Left";
            return leftEyePosition;
        }
        else
        {
            Debug.Log("Both eyes are open");
            return combinedEyePosition;
        }
    }

    // Handle hit detection and its effects
    private void HandleHit(RaycastHit hit, ShotData currentShot, bool hitOccured, Vector3 shootorigin)
    {
        Transform bullseyeTransform = selectedTarget.GetChild(0);
        Transform targetTransform = selectedTarget;
        Renderer targetRenderer = targetTransform.GetComponent<Renderer>();
        if (hitOccured)
        {
            currentShot.accuracy = Vector3.Distance(hit.point, bullseyeTransform.position);
            SetTargetColor(targetRenderer, true);
        }
        else
        {
            Vector3 lineStart = shootorigin;
            Vector3 lineEnd = crosshair.position;
            currentShot.accuracy = DistanceFromLineToPoint(lineStart, lineEnd, bullseyeTransform.position);
        }
        StartCoroutine(ResetTargetColor(targetRenderer));
        Vector3 fromBullseyeToCrosshairDir = (crosshair.position - bullseyeTransform.position).normalized;
        float leftEyeDistToRay = Vector3.Cross(fromBullseyeToCrosshairDir, leftEyePosition - bullseyeTransform.position).magnitude;
        float rightEyeDistToRay = Vector3.Cross(fromBullseyeToCrosshairDir, rightEyePosition - bullseyeTransform.position).magnitude;
        currentShot.leftEyeDistanceFromRay = leftEyeDistToRay;
        currentShot.rightEyeDistanceFromRay = rightEyeDistToRay;
        allShotsData.Add(currentShot);
        SelectTarget();
        if (shotCount == calibrationShots)
        {
            Debug.Log("Done");
            SaveDataToCSV(allShotsData);
            SceneManager.LoadScene("AllDone");
        }
    }

    // Calculate distance from line to point
    private float DistanceFromLineToPoint(Vector3 lineStart, Vector3 lineEnd, Vector3 point)
    {
        Vector3 lineDirection = (lineEnd - lineStart).normalized;
        float projectionDistance = Vector3.Dot(point - lineStart, lineDirection);
        Vector3 closestPointOnLine = lineStart + lineDirection * projectionDistance;
        return Vector3.Distance(closestPointOnLine, point);
    }

    // Set target color based on hit status
    private void SetTargetColor(Renderer renderer, bool hit)
    {
        if (renderer != null && hit)
        {
            Material[] mats = renderer.materials;
            int targetMaterialIndex = FindTargetMaterialIndex(mats);
            if (targetMaterialIndex != -1)
            {
                mats[targetMaterialIndex].color = Color.green;
            }
        }
    }

    // Reset target color after a short delay
    IEnumerator ResetTargetColor(Renderer renderer)
    {
        yield return new WaitForSeconds(1);
        if (renderer != null)
        {
            Material[] mats = renderer.materials;
            int targetMaterialIndex = FindTargetMaterialIndex(mats);
            if (targetMaterialIndex != -1)
            {
                mats[targetMaterialIndex].color = Color.white;
            }
        }
    }

    // Find the index of "Target Paper" material
    private int FindTargetMaterialIndex(Material[] materials)
    {
        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i].name.StartsWith("Target Paper"))
            {
                return i;
            }
        }
        return -1;
    }

    // Save shot data to CSV file
    void SaveDataToCSV(List<ShotData> data)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("ShotNumber,SelectedTargetID,SelectedTargetPosition,CrosshairPosition,CrosshairRotation,LeftEyePosition,RightEyePosition,CombinedEyePosition,LeftEyeOpen,RightEyeOpen,XRPosition,XRRotation,Hit,Accuracy,DominantEyeForShot,LeftEyeDistanceFromRay,RightEyeDistanceFromRay");
        foreach (var shot in data)
        {
            string leftEyePos = $"\"{shot.leftEyePosition}\"";
            string rightEyePos = $"\"{shot.rightEyePosition}\"";
            string combinedEyePos = $"\"{shot.combinedEyePosition}\"";
            string xrPos = $"\"{shot.XRPosition}\"";
            string xrRot = $"\"{shot.XRRotation}\"";
            string selectedTargetPos = $"\"{shot.selectedTargetPosition}\"";
            string crosshairPos = $"\"{shot.crosshairPosition}\"";
            string crosshairRot = $"\"{shot.crosshairRotation}\"";
            string leftEyeOpen = shot.leftEyeOpen ? "True" : "False";
            string rightEyeOpen = shot.rightEyeOpen ? "True" : "False";
            sb.AppendLine($"{shot.shotNumber},{shot.selectedTargetID},{selectedTargetPos},{crosshairPos},{crosshairRot},{leftEyePos},{rightEyePos},{combinedEyePos},{leftEyeOpen},{rightEyeOpen},{xrPos},{xrRot},{shot.hit},{shot.accuracy},{shot.dominantEyeForShot},{shot.leftEyeDistanceFromRay},{shot.rightEyeDistanceFromRay}");
        }
        System.IO.File.WriteAllText("DominantRoundData.csv", sb.ToString());
    }

    // Casing release function
    void CasingRelease()
    {
        if (!casingExitLocation || !casingPrefab)
        { return; }
        GameObject tempCasing;
        tempCasing = Instantiate(casingPrefab, casingExitLocation.position, casingExitLocation.rotation) as GameObject;
        tempCasing.GetComponent<Rigidbody>().AddExplosionForce(Random.Range(ejectPower * 0.7f, ejectPower), (casingExitLocation.position - casingExitLocation.right * 0.3f - casingExitLocation.up * 0.6f), 1f);
        tempCasing.GetComponent<Rigidbody>().AddTorque(new Vector3(0, Random.Range(100f, 500f), Random.Range(100f, 1000f)), ForceMode.Impulse);
        Destroy(tempCasing, destroyTimer);
    }
}
