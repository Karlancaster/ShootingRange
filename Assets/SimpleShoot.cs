using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ViveSR.anipal.Eye;
using UnityEngine.XR.Interaction.Toolkit;
using System.IO;
using UnityEngine.SceneManagement;
using Unity.VisualScripting;

[AddComponentMenu("Script")]
public class SimpleShoot : MonoBehaviour
{
    // Prefabs for bullet, casing, and muzzle flash
    public GameObject bulletPrefab;
    public GameObject casingPrefab;
    public GameObject muzzleFlashPrefab;

    // References to animator and locations for shooting
    private Animator gunAnimator;
    private Transform barrelLocation;
    private Transform casingExitLocation;

    // Timer and forces for shooting
    private float destroyTimer = 1f;
    private float shotPower = 500f;
    private float ejectPower = 150f;

    // List of target transforms and target material
    public List<Transform> targets;
    public Material targetMaterial;
    private Transform selectedTarget;

    // Eye positions
    private Vector3 leftEyePosition = Vector3.zero;
    private Vector3 rightEyePosition = Vector3.zero;
    private Vector3 combinedEyePosition = Vector3.zero;

    // Audio source and clip for shooting sound
    public AudioSource source;
    public AudioClip fireSound;
    public XRGrabInteractable grabInteractable;

    // Shot count and counts for dominant eye determination
    private int shotCount = 0;
    private int leftEyeDominantCount = 0;
    private int rightEyeDominantCount = 0;
    private const int calibrationShots = 5;
    private string determinedDominantEye = null;
    public Transform XRRig;

    // Serializable class for shot data
    [System.Serializable]
    public class ShotData
    {
        // Data fields for each shot
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
        // Initialize barrel location, animator, and select initial target
        if (barrelLocation == null)
            barrelLocation = transform;

        if (gunAnimator == null)
            gunAnimator = GetComponentInChildren<Animator>();

        SelectTarget();

        // Simulate grab interaction for VR controller
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

    // Simulate grabbing interactable object
    void SimulateGrab(XRBaseController controller)
    {
        var interactor = controller.GetComponent<XRBaseInteractor>();
        if (interactor != null && grabInteractable != null)
        {
            interactor.StartManualInteraction((IXRSelectInteractable)grabInteractable);
        }
    }

    // Method for pulling the trigger
    public void PullTrigger()
    {
        gunAnimator.SetTrigger("Fire");
    }

    // Update is called once per frame
    void Update()
    {
        // Update eye gaze and openness
        VisualizeAiming();
        UpdateEyeGaze();
        UpdateEyeOpenness();
    }

    // Update eye gaze direction
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

    // Update eye openness
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

    // Visualize aiming direction
    private void VisualizeAiming()
    {
        Vector3 leftAimDirection = (crosshair.position - leftEyePosition).normalized;
        Debug.DrawRay(leftEyePosition, leftAimDirection * aimLineLength, Color.red);

        Vector3 rightAimDirection = (crosshair.position - rightEyePosition).normalized;
        Debug.DrawRay(rightEyePosition, rightAimDirection * aimLineLength, Color.green);

        Vector3 combinedAimDirection = (crosshair.position - combinedEyePosition).normalized;
        Debug.DrawRay(combinedEyePosition, combinedAimDirection * aimLineLength, Color.blue);
    }

    // Select a target from the list
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

    // Set emission for target material
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

    // Shoot method
    void Shoot()
    {
        // Play shooting sound and instantiate muzzle flash
        source.PlayOneShot(fireSound);

        if (muzzleFlashPrefab)
        {
            var tempFlash = Instantiate(muzzleFlashPrefab, barrelLocation.position, barrelLocation.rotation);
            Destroy(tempFlash, destroyTimer);
        }

        // Instantiate bullet and apply force
        if (!bulletPrefab) return;
        var bulletInstance = Instantiate(bulletPrefab, barrelLocation.position, barrelLocation.rotation).GetComponent<Rigidbody>();
        bulletInstance.AddForce(barrelLocation.forward * shotPower);

        // Create ShotData instance and handle hit
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

        RaycastHit hitInfo = new RaycastHit();
        bool hitOccurred = false;

        Vector3[] eyePositions = { leftEyePosition, rightEyePosition, combinedEyePosition };
        foreach (var eyePosition in eyePositions)
        {
            if (Physics.Raycast(eyePosition, (crosshair.position - eyePosition).normalized, out hitInfo, Mathf.Infinity))
            {
                if (hitInfo.transform == selectedTarget || hitInfo.transform.IsChildOf(selectedTarget))
                {
                    hitOccurred = true;
                    break;
                }
            }
        }

        currentShot.hit = hitOccurred;
        HandleHit(hitInfo, currentShot, hitOccurred);
    }

    // Handle hit detection and update ShotData
    private void HandleHit(RaycastHit hit, ShotData currentShot, bool hitOccured)
    {
        Transform bullseyeTransform = selectedTarget.GetChild(0);
        Vector3 fromBullseyeToCrosshairDir = (crosshair.position - bullseyeTransform.position).normalized;
        Transform targetTransform = selectedTarget;
        Renderer targetRenderer = targetTransform.GetComponent<Renderer>();

        Vector3 shootorigin = combinedEyePosition;
        AdjustDominantEyeCounts(fromBullseyeToCrosshairDir, bullseyeTransform.position, currentShot);
        if (currentShot.dominantEyeForShot == "Right")
        {
            shootorigin = rightEyePosition;
        }
        else if (currentShot.dominantEyeForShot == "Left")
        {
            shootorigin = leftEyePosition;
        }
        else
        {
            shootorigin = combinedEyePosition;
        }

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

        float leftEyeDistToRay = Vector3.Cross(fromBullseyeToCrosshairDir, leftEyePosition - bullseyeTransform.position).magnitude;
        float rightEyeDistToRay = Vector3.Cross(fromBullseyeToCrosshairDir, rightEyePosition - bullseyeTransform.position).magnitude;
        currentShot.leftEyeDistanceFromRay = leftEyeDistToRay;
        currentShot.rightEyeDistanceFromRay = rightEyeDistToRay;

        allShotsData.Add(currentShot);
        SelectTarget();

        if (shotCount == calibrationShots)
        {
            Debug.Log("Round 1 done");
            DetermineDominantEye();
            SceneManager.LoadScene("firstDone");
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

    // Adjust counts for dominant eye determination
    private void AdjustDominantEyeCounts(Vector3 fromBullseyeToCrosshairDir, Vector3 bullseyePosition, ShotData currentShot)
    {
        float leftEyeDistToRay = Vector3.Cross(fromBullseyeToCrosshairDir, leftEyePosition - bullseyePosition).magnitude;
        float rightEyeDistToRay = Vector3.Cross(fromBullseyeToCrosshairDir, rightEyePosition - bullseyePosition).magnitude;

        if (!isLeftEyeOpen && isRightEyeOpen)
        {
            rightEyeDominantCount++;
            Debug.Log("Left eye is closed so Right eye is dominant");
            currentShot.dominantEyeForShot = "Right";
        }
        else if (!isRightEyeOpen && isLeftEyeOpen)
        {
            leftEyeDominantCount++;
            Debug.Log("Right eye is closed so Left eye is dominant");
            currentShot.dominantEyeForShot = "Left";
        }
        else
        {
            if (leftEyeDistToRay < rightEyeDistToRay)
            {
                leftEyeDominantCount++;
                Debug.Log("both eyes open but Left eye is dominant");
                currentShot.dominantEyeForShot = "Left";
                currentShot.leftEyeDistanceFromRay = leftEyeDistToRay;
                currentShot.rightEyeDistanceFromRay = rightEyeDistToRay;
            }
            else
            {
                rightEyeDominantCount++;
                Debug.Log("Both eyes open but Right eye is dominant");
                currentShot.dominantEyeForShot = "Right";
                currentShot.leftEyeDistanceFromRay = leftEyeDistToRay;
                currentShot.rightEyeDistanceFromRay = rightEyeDistToRay;
            }
        }
    }

    // Set target color on hit
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

    // Coroutine to reset target color after a delay
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

    // Find the index of the target material
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

    // Determine dominant eye based on counts
    private void DetermineDominantEye()
    {
        if (leftEyeDominantCount > rightEyeDominantCount)
        {
            determinedDominantEye = "Left";
            Debug.Log("Determined dominant eye: Left");
        }
        else if (rightEyeDominantCount > leftEyeDominantCount)
        {
            determinedDominantEye = "Right";
            Debug.Log("Determined dominant eye: Right");
        }
        else
        {
            determinedDominantEye = "Inconclusive";
            Debug.Log("Dominance is inconclusive. Further analysis may be required.");
        }

        shotCount = 0;
        leftEyeDominantCount = 0;
        rightEyeDominantCount = 0;
        SaveDataToCSV(allShotsData);
        PlayerPrefs.SetString("DominantEye", determinedDominantEye);
        PlayerPrefs.Save();
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

        System.IO.File.WriteAllText("ShotData.csv", sb.ToString());
    }
}
