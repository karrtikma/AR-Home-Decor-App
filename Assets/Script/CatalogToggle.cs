using System.Collections;
using UnityEngine;

/// Put this on your Catalog panel (the GameObject that holds Chair/Table/Lamp).
/// Requires a CanvasGroup on the same object.
[RequireComponent(typeof(CanvasGroup))]
public class CatalogToggle : MonoBehaviour
{
    [Header("Behavior")]
    [Tooltip("Hide the catalog on first load.")]
    public bool startHidden = true;

    [Tooltip("Disable the GameObject after fade-out (saves draw calls).")]
    public bool deactivateOnHide = true;

    [Tooltip("Seconds for the fade in/out.")]
    public float fadeDuration = 0.25f;

    [Tooltip("Ease curve for the fade.")]
    public AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Optional: tiny scale punch")]
    public bool scalePunch = true;
    public Vector3 hiddenScale = Vector3.one * 0.98f; // 98% then up to 100%

    CanvasGroup group;
    bool isAnimating;
    bool isVisible;

    void Reset() => group = GetComponent<CanvasGroup>();
    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        if (startHidden) SetVisible(false, instant: true);
        else SetVisible(true, instant: true);
    }

    /// Hook this to the Menu button OnClick.
    public void Toggle()
    {
        if (isAnimating) return;
        if (isVisible) Hide();
        else Show();
    }

    public void Show()
    {
        if (isAnimating || isVisible) return;
        StartCoroutine(FadeRoutine(show: true));
    }

    public void Hide()
    {
        if (isAnimating || !isVisible) return;
        StartCoroutine(FadeRoutine(show: false));
    }

    IEnumerator FadeRoutine(bool show)
    {
        isAnimating = true;

        if (show)
        {
            if (deactivateOnHide && !gameObject.activeSelf) gameObject.SetActive(true);
            group.blocksRaycasts = false;
            group.interactable = false;
            if (scalePunch) transform.localScale = hiddenScale;
        }

        float startA = Mathf.Clamp01(group.alpha);
        float endA = show ? 1f : 0f;
        float t = 0f;

        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float u = fadeDuration > 0f ? t / fadeDuration : 1f;
            float k = fadeCurve.Evaluate(Mathf.Clamp01(u));

            float a = Mathf.Lerp(startA, endA, k);
            group.alpha = a;

            if (scalePunch)
            {
                float s = Mathf.LerpUnclamped(hiddenScale.x, 1f, k);
                transform.localScale = new Vector3(s, s, s);
            }

            yield return null;
        }

        group.alpha = endA;

        if (show)
        {
            group.blocksRaycasts = true;
            group.interactable = true;
            if (scalePunch) transform.localScale = Vector3.one;
            isVisible = true;
        }
        else
        {
            group.blocksRaycasts = false;
            group.interactable = false;
            isVisible = false;
            if (deactivateOnHide) gameObject.SetActive(false);
            if (scalePunch) transform.localScale = Vector3.one;
        }

        isAnimating = false;
    }

    void SetVisible(bool show, bool instant)
    {
        if (deactivateOnHide && !show) gameObject.SetActive(false);
        else gameObject.SetActive(true);

        group.alpha = show ? 1f : 0f;
        group.interactable = show;
        group.blocksRaycasts = show;
        isVisible = show;
        if (scalePunch) transform.localScale = Vector3.one;
    }
}
