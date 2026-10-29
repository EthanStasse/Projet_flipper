using UnityEngine;
using System.Collections;

public class BumperEffect : MonoBehaviour
{
    [Header("Propulsion de la bille")]
    public float launchImpulse = 8f;
    public float launchImpulseVariation = 3f;

    [Header("Paramètres de l'Effet Lumineux")]
    public Light bumperLight;
    public float targetLightIntensity = 20f;

    [Header("Paramètres de l'Effet de Transition Visuelle")]
    public Color hitColor = Color.white;
    public float scaleMultiplier = 1.2f;

    [Header("Paramètres de Durée (Transition Fluide)")]
    public float transitionUpDuration = 0.05f;
    public float transitionDownDuration = 0.2f;

    private Vector3 originalScale;
    private Color originalColor;
    private Renderer bumperRenderer;
    public Transform bumperVisual;
    private bool isAnimating;

    void Start()
    {
        if (bumperLight != null)
            bumperLight.intensity = 0f;

        bumperRenderer = GetComponentInChildren<Renderer>();
        if (bumperRenderer != null)
        {
            if (bumperVisual == null && bumperRenderer.transform != transform)
                bumperVisual = bumperRenderer.transform;

            if (bumperRenderer.material.HasProperty("_Color"))
                originalColor = bumperRenderer.material.color;
            else if (bumperRenderer.material.HasProperty("_BaseColor"))
                originalColor = bumperRenderer.material.GetColor("_BaseColor");
        }

        if (bumperVisual == transform)
        {
            Debug.LogWarning("Assignez à Bumper Visual un enfant sans collider pour animer l'échelle sans agrandir le collider du bumper.", this);
            bumperVisual = null;
        }

        if (bumperVisual != null)
            originalScale = bumperVisual.localScale;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.name != "la_bille")
            return;

        Rigidbody ballRigidbody = collision.rigidbody;
        if (ballRigidbody == null)
        {
            Debug.LogWarning("Le bumper a touché la bille, mais aucun Rigidbody n'a été trouvé.", collision.gameObject);
            return;
        }

        if (ballRigidbody.isKinematic)
        {
            Debug.LogWarning("La bille ne peut pas être propulsée : son Rigidbody est en mode Is Kinematic.", ballRigidbody);
            return;
        }

        Vector3 launchDirection = ballRigidbody.worldCenterOfMass - transform.position;
        launchDirection.y = 0f;

        if (launchDirection.sqrMagnitude < 0.0001f)
        {
            launchDirection = collision.GetContact(0).point - transform.position;
            launchDirection.y = 0f;
        }

        if (launchDirection.sqrMagnitude < 0.0001f)
        {
            Debug.LogWarning("Impossible de déterminer la direction radiale de propulsion du bumper.", this);
            return;
        }

        float currentLaunchImpulse = launchImpulse + Random.Range(-launchImpulseVariation, launchImpulseVariation);
        currentLaunchImpulse = Mathf.Max(0.1f, currentLaunchImpulse);
        ballRigidbody.AddForce(launchDirection.normalized * currentLaunchImpulse, ForceMode.Impulse);

        if (!isAnimating)
            StartCoroutine(SmoothAnimateBumper());
    }

    IEnumerator SmoothAnimateBumper()
    {
        isAnimating = true;

        Vector3 hitScale = new Vector3(
            originalScale.x * scaleMultiplier,
            originalScale.y,
            originalScale.z * scaleMultiplier);

        float timerUp = 0f;
        while (timerUp < transitionUpDuration)
        {
            timerUp += Time.deltaTime;
            float t = Mathf.Clamp01(timerUp / transitionUpDuration);

            if (bumperVisual != null)
                bumperVisual.localScale = Vector3.Lerp(originalScale, hitScale, t);

            ApplyColor(Color.Lerp(originalColor, hitColor, t));

            if (bumperLight != null)
                bumperLight.intensity = Mathf.Lerp(0f, targetLightIntensity, t);

            yield return null;
        }

        float timerDown = 0f;
        while (timerDown < transitionDownDuration)
        {
            timerDown += Time.deltaTime;
            float t = Mathf.Clamp01(timerDown / transitionDownDuration);

            if (bumperVisual != null)
                bumperVisual.localScale = Vector3.Lerp(hitScale, originalScale, t);

            ApplyColor(Color.Lerp(hitColor, originalColor, t));

            if (bumperLight != null)
                bumperLight.intensity = Mathf.Lerp(targetLightIntensity, 0f, t);

            yield return null;
        }

        if (bumperVisual != null)
            bumperVisual.localScale = originalScale;

        ApplyColor(originalColor);
        if (bumperLight != null)
            bumperLight.intensity = 0f;

        isAnimating = false;
    }

    void ApplyColor(Color color)
    {
        if (bumperRenderer == null)
            return;

        if (bumperRenderer.material.HasProperty("_Color"))
            bumperRenderer.material.color = color;
        else if (bumperRenderer.material.HasProperty("_BaseColor"))
            bumperRenderer.material.SetColor("_BaseColor", color);
    }
}
