using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARPlaneLoadingUI : MonoBehaviour
{
    [Header("AR")]
    public ARSession arSession;
    public ARPlaneManager planeManager;
    public ARAnchorManager anchorManager;

    [Header("Overlay UI")]
    public GameObject overlayRoot;          // e.g. LoadingOverlay
    public CanvasGroup overlayCanvasGroup;   // on LoadingOverlay (optional fade)
    public VideoPlayer videoPlayer;          // on LoadingOverlay (RenderMode = Camera Near Plane)

    [Tooltip("Overlay kept for at least this many seconds before it can hide.")]
    public float minShowSeconds = 0.75f;
    public float fadeDuration = 0.25f;

    [Tooltip("Transparency of the video while scanning (0..1). 0.35 ≈ nice see-through.")]
    [Range(0f, 1f)] public float scanningAlpha = 0.35f;

    [Header("Optional: also clear your placed object")]
    public ARFurnitureManager furnitureManager;

    bool overlayVisible;
    bool waitingForPlane;
    float showTime;

    void OnEnable()
    {
        if (planeManager != null) planeManager.planesChanged += OnPlanesChanged;
        StartScanningUI();
    }

    void OnDisable()
    {
        if (planeManager != null) planeManager.planesChanged -= OnPlanesChanged;
        ForceHideOverlay(); // make absolutely sure the camera overlay is gone
    }

    void Update()
    {
        // Safety: if overlay is up and we already have a tracking plane, hide it.
        if (overlayVisible && waitingForPlane && AnyPlaneTracking())
            TryHideOverlay();
    }

    // ---------- Public: wire your Reset button to this ----------
    public void ResetScanning()
    {
        StartCoroutine(ResetRoutine());
    }

    IEnumerator ResetRoutine()
    {
        StartScanningUI();             // show translucent video immediately
        ClearAnchorsAndPlanes();       // remove anchors/planes so AR can rescan

        // Reset AR Session (fast if supported; otherwise toggle)
        bool toggle = false;
        if (arSession != null)
        {
            try
            {
#if AR_FOUNDATION_5_0_OR_NEWER
                arSession.Reset();
#else
                toggle = true;
#endif
            }
            catch { toggle = true; }
        }
        if (toggle && arSession != null)
        {
            arSession.enabled = false;
            yield return null;
            arSession.enabled = true;
        }

        // Re-enable plane detection (horizontal by default)
        if (planeManager != null)
        {
            planeManager.enabled = true;
#if UNITY_XR_ARFOUNDATION
            planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal;
#endif
        }
    }

    // ---------- Internals ----------
    void StartScanningUI()
    {
        waitingForPlane = true;
        showTime = Time.unscaledTime;

        if (overlayRoot != null && !overlayRoot.activeSelf)
            overlayRoot.SetActive(true);

        if (overlayCanvasGroup != null)
        {
            overlayCanvasGroup.alpha = 1f;
            overlayCanvasGroup.interactable = true;
            overlayCanvasGroup.blocksRaycasts = true;
        }

        // Prepare + play video and set translucent alpha on camera near plane
        if (videoPlayer != null)
            StartCoroutine(PrepareAndPlay(videoPlayer, scanningAlpha));

        overlayVisible = true;
    }

    IEnumerator PrepareAndPlay(VideoPlayer vp, float alpha)
    {
        if (vp.renderMode == VideoRenderMode.CameraNearPlane ||
            vp.renderMode == VideoRenderMode.CameraFarPlane)
        {
            vp.targetCameraAlpha = Mathf.Clamp01(alpha);
        }

        vp.isLooping = true;
        vp.playbackSpeed = 1f;
        vp.SetDirectAudioMute(0, true);

        if (!vp.isPrepared)
        {
            vp.Prepare();
            while (!vp.isPrepared) yield return null;
        }
        vp.Play();
    }

    void OnPlanesChanged(ARPlanesChangedEventArgs _)
    {
        if (!overlayVisible || !waitingForPlane) return;
        TryHideOverlay();
    }

    bool AnyPlaneTracking()
    {
        if (planeManager == null) return false;
        foreach (var p in planeManager.trackables)
            if (p.trackingState == TrackingState.Tracking) return true;
        return false;
    }

    void TryHideOverlay()
    {
        if (Time.unscaledTime - showTime < minShowSeconds) return;
        if (!AnyPlaneTracking()) return;

        waitingForPlane = false;
        StartCoroutine(HideOverlayCo());
    }

    IEnumerator HideOverlayCo()
    {
        // Fade the near-plane overlay off first (prevents “last frame” sticking)
        if (videoPlayer != null)
        {
            if (videoPlayer.renderMode == VideoRenderMode.CameraNearPlane ||
                videoPlayer.renderMode == VideoRenderMode.CameraFarPlane)
            {
                // smooth fade camera overlay
                float start = videoPlayer.targetCameraAlpha;
                float t = 0f;
                while (t < fadeDuration)
                {
                    t += Time.unscaledDeltaTime;
                    videoPlayer.targetCameraAlpha = Mathf.Lerp(start, 0f, t / fadeDuration);
                    yield return null;
                }
                videoPlayer.targetCameraAlpha = 0f;
            }
            videoPlayer.Stop();
        }

        if (overlayCanvasGroup != null)
        {
            float a0 = overlayCanvasGroup.alpha;
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                overlayCanvasGroup.alpha = Mathf.Lerp(a0, 0f, t / fadeDuration);
                yield return null;
            }
            overlayCanvasGroup.alpha = 0f;
            overlayCanvasGroup.interactable = false;
            overlayCanvasGroup.blocksRaycasts = false;
        }

        if (overlayRoot != null) overlayRoot.SetActive(false);
        overlayVisible = false;
    }

    void ForceHideOverlay()
    {
        if (videoPlayer != null)
        {
            if (videoPlayer.renderMode == VideoRenderMode.CameraNearPlane ||
                videoPlayer.renderMode == VideoRenderMode.CameraFarPlane)
                videoPlayer.targetCameraAlpha = 0f;
            videoPlayer.Stop();
        }
        if (overlayCanvasGroup != null) overlayCanvasGroup.alpha = 0f;
        if (overlayRoot != null) overlayRoot.SetActive(false);
        overlayVisible = false;
        waitingForPlane = false;
    }

    void ClearAnchorsAndPlanes()
    {
        if (furnitureManager != null) furnitureManager.OnDelete();

        if (anchorManager != null)
        {
            var list = new List<ARAnchor>();
            foreach (var a in anchorManager.trackables) if (a) list.Add(a);
            foreach (var a in list) Destroy(a.gameObject);
        }

        if (planeManager != null)
        {
            var list = new List<ARPlane>();
            foreach (var p in planeManager.trackables) if (p) list.Add(p);
            foreach (var p in list) Destroy(p.gameObject);
        }
    }
}
