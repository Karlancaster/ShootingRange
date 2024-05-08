using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ViveSR.anipal.Eye;
using UnityEngine.XR.Interaction.Toolkit;
using System.IO;
using UnityEngine.SceneManagement;

// AddComponentMenu allows easy access to this script in Unity's component menu.
[AddComponentMenu("Script")]
public class CyclopeanShoot : MonoBehaviour
{
    // Prefabs for bullet, casing, and muzzle flash.
    public GameObject bulletPrefab;
    public GameObject casingPrefab;
    public GameObject muzzleFlashPrefab;

    // Reference to the gun's animator, barrel location, and casing exit location.
    [SerializeField] private Animator gunAnimator;
    [SerializeField] private Transform barrelLocation;
    [SerializeField] private Transform casingExitLocation;

    // Time for casing destruction, bullet speed, and casing ejection speed.
    [Tooltip("Specify time to destroy the casing object")] [SerializeField] private float destroyTimer = 1f;
    [Tooltip("Bullet Speed")] [SerializeField] private float shotPower = 500f;
    [Tooltip("Casing Ejection Speed")] [SerializeField] private float ejectPower = 150f;

    // List of targets and their material.
    public List<Transform> targets;
    public Material targetMaterial;
    private Transform selectedTarget;

    // Eye positions for gaze tracking.
    private Vector3 leftEyePosition = Vector3.zero;
    private Vector3 rightEyePosition = Vector3.zero;
    private Vector3 combinedEyePosition = Vector3.zero;

    // Audio source and clip for gun firing.
    public AudioSource source;
    public AudioClip fireSound;

    // XR Interactable for grabbing.
    public XRGrabInteractable grabInteractable;

    // Shot counting and calibration variables.
    private int shotCount = 0;
    private const int calibrationShots = 5;
    public Transform XRRig;

    // Structure to hold shot data.
    [System.Serializable]
    public class ShotData
    {
        // Shot information fields.
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

    // List to store all shot data.
    List<ShotData> allShotsData = new List<ShotData>();

    void Start()
    {
        // Assign barrel location if not set.
        if (barrelLocation == null)
            barrelLocation = transform;

        // Assign gun animator if not set.
        if (gunAnimator == null)
            gunAnimator = GetComponentInChildren<Animator>();

        // Select initial target and simulate grab interaction.
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

    // Simulate grab interaction.
    void SimulateGrab(XRBaseController controller)
    {
        var interactor = controller.GetComponent<XRBaseInteractor>();
        if (interactor != null && grabInteractable != null)
        {
            interactor.StartManualInteraction((IXRSelectInteractable)grabInteractable);
        }
    }

    // Method to pull the trigger.
    public void PullTrigger()
    {
        gunAnimator.SetTrigger("Fire");
    }

    // Update method for visualizing aiming and eye tracking.
    void Update()
    {
        VisualizeAiming();
        UpdateEyeGaze();
        UpdateEyeOpenness();
    }

    // Update eye gaze positions.
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

    // Update eye openness.
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

    // Visualize aiming direction.
    private void VisualizeAiming()
    {
        Vector3 leftAimDirection = (crosshair.position - leftEyePosition).normalized;
        Debug.DrawRay(leftEyePosition, leftAimDirection * aimLineLength, Color.red);

        Vector3 rightAimDirection = (crosshair.position - rightEyePosition).normalized;
        Debug.DrawRay(rightEyePosition, rightAimDirection * aimLineLength, Color.green);

        Vector3 combinedAimDirection = (crosshair.position - combinedEyePosition).normalized;
        Debug.DrawRay(combinedEyePosition, combinedAimDirection * aimLineLength, Color.blue);
    }

    // Select a target from the list.
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

    // Set emission for target highlighting.
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

    // Method to shoot bullets.
    void Shoot()
    {
        source.PlayOneShot(fireSound);

        // Instantiate muzzle flash.
        if (muzzleFlashPrefab)
        {
            var tempFlash = Instantiate(muzzleFlashPrefab, barrelLocation.position, barrelLocation.rotation);
            Destroy(tempFlash, destroyTimer);
        }

        // Instantiate bullet prefab and apply force.
        if (!bulletPrefab) return;
        var bulletInstance = Instantiate(bulletPrefab, barrelLocation.position, barrelLocation.rotation).GetComponent<Rigidbody>();
        bulletInstance.AddForce(barrelLocation.forward * shotPower);

        // Create shot data and determine hit.
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
            dominantEyeForShot = "Cyclopean",
            XRPosition = XRRig.position.ToString(),
            XRRotation = XRRig.rotation.eulerAngles.ToString()
        };

        // Determine hit with raycast.
        Vector3 shootOrigin = DetermineShootOrigin(currentShot);
        RaycastHit hit;
        bool hitDetected = Physics.Raycast(shootOrigin, (crosshair.position - shootOrigin).normalized, out hit, Mathf.Infinity);

        // Handle hit or miss.
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
        }
    }

    // Determine shoot origin.
    private Vector3 DetermineShootOrigin(ShotData current)
    {
        current.dominantEyeForShot = "Cyclopean";
        return (leftEyePosition + rightEyePosition) / 2;
    }

    // Handle hit result.
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
            Debug.Log("Round 2 Done");
            SaveDataToCSV(allShotsData);
            SceneManager.LoadScene("secondDone");
        }
    }

    // Calculate distance from a line to a point.
    private float DistanceFromLineToPoint(Vector3 lineStart, Vector3 lineEnd, Vector3 point)
    {
        Vector3 lineDirection = (lineEnd - lineStart).normalized;
        float projectionDistance = Vector3.Dot(point - lineStart, lineDirection);
        Vector3 closestPointOnLine = lineStart + lineDirection * projectionDistance;
        return Vector3.Distance(closestPointOnLine, point);
    }

    // Set target color on hit.
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

    // Coroutine to reset target color after a delay.
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

    // Find target material index.
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

    // Save shot data to CSV file.
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

        System.IO.File.WriteAllText("CyclopeanData.csv", sb.ToString());
    }


    //This function creates a casing at the ejection slot
    void CasingRelease()
    {
        if (!casingExitLocation || !casingPrefab)
        { return; }

        //Create the casing
        GameObject tempCasing;
        tempCasing = Instantiate(casingPrefab, casingExitLocation.position, casingExitLocation.rotation) as GameObject;
        //Add force on casing to push it out
        tempCasing.GetComponent<Rigidbody>().AddExplosionForce(Random.Range(ejectPower * 0.7f, ejectPower), (casingExitLocation.position - casingExitLocation.right * 0.3f - casingExitLocation.up * 0.6f), 1f);
        //Add torque to make casing spin in random direction
        tempCasing.GetComponent<Rigidbody>().AddTorque(new Vector3(0, Random.Range(100f, 500f), Random.Range(100f, 1000f)), ForceMode.Impulse);

        //Destroy casing after X seconds
        Destroy(tempCasing, destroyTimer);
    }

}
