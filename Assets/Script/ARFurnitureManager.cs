using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using TMPro;

public class ARFurnitureManager : MonoBehaviour
{
    [Header("AR")]
    public ARRaycastManager raycastManager;
    public ARPlaneManager planeManager;
    public ARAnchorManager anchorManager;
    public Camera arCamera;

    [Header("Furniture")]
    public List<GameObject> furniturePrefabs;
    public int defaultIndex = 0;

    [Header("UI (TMP)")]
    public GameObject controlsPanel;
    public Slider rotateSlider;         // 0..360 (Dynamic float)
    public Slider scaleSlider;          // 0.2..3  (Dynamic float; Value = 1)
    public TMP_Text hintText;
    public TMP_Text rotateValueText;
    public TMP_Text scaleValueText;

    [Header("UI Raycast")]
    public GraphicRaycaster uiRaycaster;  // to block touches under UI

    [Header("Placement")]
    public float surfaceOffset = 0.01f;   // avoids z-fighting
    public bool faceCameraOnPlace = true;
    public bool liftByHalfHeight = true;

    [Header("Reticle (optional)")]
    public GameObject reticlePrefab;

    [Header("Debug HUD (optional)")]
    public TMP_Text debugText;
    public bool debugEnabled = false;

    // runtime
    GameObject currentPrefab;
    GameObject currentInstance;           // child of anchor
    ARAnchor currentAnchor;
    Quaternion baseLocalRotation = Quaternion.identity;
    Vector3 baseScale = Vector3.one;
    float currentYaw = 0f;
    float yLift = 0f;
    bool isDragging = false;

    GameObject reticle;
    static readonly List<ARRaycastHit> hits = new();
    ARRaycastHit lastHit;

    void Start()
    {
        if (planeManager) planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal;
        if (hintText) hintText.text = "Scan a floor, then tap to place.";
        SelectFurniture(defaultIndex);
        ShowControls(false);

        if (reticlePrefab) { reticle = Instantiate(reticlePrefab); reticle.SetActive(false); }
    }

    // ----- Catalog -----
    public void SelectFurniture(int index)
    {
        if (index < 0 || index >= furniturePrefabs.Count) return;
        currentPrefab = furniturePrefabs[index];
        DeleteCurrent(); // one at a time
        Log($"Selected: {currentPrefab.name}");
    }

    // ----- Loop -----
    void Update()
    {
        UpdateReticle();

        if (Input.touchCount == 0) return;
        var t = Input.GetTouch(0);

        if (IsPointerOverUI(t)) return;

        if (t.phase == TouchPhase.Began)
        {
            // Drag if tapping the object
            if (currentInstance && RaycastModel(t.position, out var h))
            {
                if (h.transform == currentInstance.transform || h.transform.IsChildOf(currentInstance.transform))
                { isDragging = true; return; }
            }

            // Place / Move
            if (TryHitPlane(t.position, out var pose))
            {
                if (!currentInstance)
                {
                    PlaceAt(pose);
                    ApplyUIValues();
                    ShowControls(true);
                }
                else Reanchor(pose);
            }
        }
        else if (t.phase == TouchPhase.Moved && isDragging)
        {
            if (TryHitPlane(t.position, out var pose)) Reanchor(pose);
        }
        else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
        {
            isDragging = false;
        }
    }

    // ----- UI callbacks -----
    public void OnRotateChanged(float yDeg)
    {
        currentYaw = yDeg;
        if (currentInstance)
            currentInstance.transform.localRotation =
                Quaternion.AngleAxis(currentYaw, Vector3.up) * baseLocalRotation;
        if (rotateValueText) rotateValueText.text = $"{Mathf.RoundToInt(yDeg)}°";
    }

    public void OnScaleChanged(float s)
    {
        s = Mathf.Clamp(s, 0.2f, 3f);
        if (currentInstance) currentInstance.transform.localScale = baseScale * s;
        if (scaleValueText) scaleValueText.text = $"{s:0.00}x";
    }

    public void OnDelete()
    {
        DeleteCurrent();
        ShowControls(false);
    }

    void ApplyUIValues()
    {
        if (rotateSlider) OnRotateChanged(rotateSlider.value);
        if (scaleSlider) OnScaleChanged(scaleSlider.value);
    }

    // ----- Placement helpers -----
    bool TryHitPlane(Vector2 pos, out Pose pose)
    {
        pose = default;
        if (!raycastManager || !raycastManager.Raycast(pos, hits, TrackableType.PlaneWithinPolygon))
            return false;

        lastHit = hits[0];
        var rot = lastHit.pose.rotation;

        if (!currentInstance && faceCameraOnPlace)
        {
            var up = Vector3.up;
            if (planeManager)
            {
                var p = planeManager.GetPlane(lastHit.trackableId);
                if (p) up = p.transform.up;
            }
            rot = Quaternion.LookRotation(
                Vector3.ProjectOnPlane(arCamera.transform.forward, up), up);
        }

        pose = new Pose(lastHit.pose.position, rot);
        return true;
    }

    void PlaceAt(Pose pose)
    {
        var plane = planeManager ? planeManager.GetPlane(lastHit.trackableId) : null;
        currentAnchor = (plane && anchorManager) ? anchorManager.AttachAnchor(plane, pose) : MakeAnchor(pose);

        currentInstance = Instantiate(currentPrefab, currentAnchor.transform);
        baseScale = currentInstance.transform.localScale;
        baseLocalRotation = currentInstance.transform.localRotation;

        yLift = surfaceOffset;
        var r = currentInstance.GetComponentInChildren<Renderer>();
        if (liftByHalfHeight && r) yLift += r.bounds.extents.y;

        currentInstance.transform.localPosition = new Vector3(0f, yLift, 0f);
        currentInstance.transform.localRotation =
            Quaternion.AngleAxis(currentYaw, Vector3.up) * baseLocalRotation;

        EnsureCollider(currentInstance);
    }

    void Reanchor(Pose pose)
    {
        var plane = planeManager ? planeManager.GetPlane(lastHit.trackableId) : null;
        var newAnchor = (plane && anchorManager) ? anchorManager.AttachAnchor(plane, pose) : MakeAnchor(pose);

        currentInstance.transform.SetParent(newAnchor.transform, true);
        currentInstance.transform.localPosition = new Vector3(0f, yLift, 0f);
        currentInstance.transform.localRotation =
            Quaternion.AngleAxis(currentYaw, Vector3.up) * baseLocalRotation;

        if (currentAnchor) Destroy(currentAnchor.gameObject);
        currentAnchor = newAnchor;
    }

    ARAnchor MakeAnchor(Pose pose)
    {
        var go = new GameObject("Anchor");
        go.transform.SetPositionAndRotation(pose.position, pose.rotation);
        return go.AddComponent<ARAnchor>();
    }

    // ----- Utilities -----
    bool IsPointerOverUI(Touch t)
    {
        if (uiRaycaster == null || EventSystem.current == null) return false;
        var data = new PointerEventData(EventSystem.current) { position = t.position };
        var results = new List<RaycastResult>();
        uiRaycaster.Raycast(data, results);
        foreach (var r in results)
            if (r.gameObject && (r.gameObject.GetComponent<Button>() || r.gameObject.GetComponent<Slider>()))
                return true;
        return false;
    }

    bool RaycastModel(Vector2 pos, out RaycastHit hit)
    {
        var ray = arCamera.ScreenPointToRay(pos);
        return Physics.Raycast(ray, out hit, 100f);
    }

    void EnsureCollider(GameObject root)
    {
        if (!root) return;
        if (!root.GetComponentInChildren<Collider>())
        {
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>())
            {
                var mf = mr.GetComponent<MeshFilter>();
                if (!mf || !mf.sharedMesh) continue;
                var mc = mr.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = true;
            }
        }
    }

    void UpdateReticle()
    {
        if (!reticle) return;
        var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        if (raycastManager && raycastManager.Raycast(center, hits, TrackableType.PlaneWithinPolygon))
        {
            var h = hits[0];
            reticle.transform.SetPositionAndRotation(h.pose.position + Vector3.up * 0.005f, h.pose.rotation);
            reticle.SetActive(true);
        }
        else reticle.SetActive(false);
    }

    void DeleteCurrent()
    {
        if (currentInstance) Destroy(currentInstance);
        currentInstance = null;
        if (currentAnchor) Destroy(currentAnchor.gameObject);
        currentAnchor = null;
    }

    void ShowControls(bool show) { if (controlsPanel) controlsPanel.SetActive(show); }

    void Log(string line)
    {
        if (!debugEnabled || !debugText) return;
        if (debugText.text.Length > 900) debugText.text = "";
        debugText.text = line + "\n" + debugText.text;
    }
}
