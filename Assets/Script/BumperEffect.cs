using UnityEngine;
using System.Collections;

public class BumperEffect : MonoBehaviour
{
    [Header("Paramètres de l'Effet Lumineux")]
    public Light bumperLight;
    public float targetLightIntensity = 20f;   // Puissance maximale du flash

    [Header("Paramètres de l'Effet de Transition Visuelle")]
    public Color hitColor = Color.white; // Couleur de flash lors de l'impact
    public float scaleMultiplier = 1.2f; // Grossissement final (ex: 1.2 = +20%)

    [Header("Paramètres de Durée (Transition Fluide)")]
    // Au lieu d'une pause, on définit des temps de transition pour la fluidité
    public float transitionUpDuration = 0.05f;  // Temps pour grossir (très rapide)
    public float transitionDownDuration = 0.2f; // Temps pour rétrécir (plus lent)

    // Variables privées pour stocker l'état initial
    private Vector3 originalScale;
    private Color originalColor;
    private Renderer bumperRenderer;
    private bool isAnimating = false;

    void Start()
    {
        // On sauvegarde l'échelle de départ
        originalScale = transform.localScale;

        // On éteint la lumière au départ
        if (bumperLight != null)
            bumperLight.intensity = 0f;

        // On récupère le Renderer pour changer la couleur
        bumperRenderer = GetComponentInChildren<Renderer>(); if (bumperRenderer != null)
        {
            // On sauvegarde la couleur d'origine du matériau (support pour standard et URP)
            if (bumperRenderer.material.HasProperty("_Color"))
                originalColor = bumperRenderer.material.color;
            else if (bumperRenderer.material.HasProperty("_BaseColor"))
                originalColor = bumperRenderer.material.GetColor("_BaseColor");
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        // On vérifie que c'est bien la bille qui touche
        if (collision.gameObject.name == "la_bille" && !isAnimating)
        {
            StartCoroutine(SmoothAnimateBumper());
        }
    }

    IEnumerator SmoothAnimateBumper()
    {
        isAnimating = true;

        // Calcul des valeurs cibles pour l'impact
        Vector3 hitScale = new Vector3(originalScale.x * scaleMultiplier, originalScale.y, originalScale.z * scaleMultiplier);

        // === PHASE 1 : TRANSITION D'ALLUMAGE (Grossissement et Flash) ===
        float timerUp = 0f;
        while (timerUp < transitionUpDuration)
        {
            timerUp += Time.deltaTime; // Incrémente le temps
            float t = timerUp / transitionUpDuration; // t va de 0 à 1

            // On LERP (Linear Interpolate) toutes les valeurs de façon fluide :

            // 1. Échelle
            transform.localScale = Vector3.Lerp(originalScale, hitScale, t);

            // 2. Couleur du corps
            ApplyColor(Color.Lerp(originalColor, hitColor, t));

            // 3. Intensité de la lumière
            if (bumperLight != null)
                bumperLight.intensity = Mathf.Lerp(0f, targetLightIntensity, t);

            // On cède l'exécution jusqu'à la prochaine image pour l'animation
            yield return null;
        }

        // === PHASE 2 : TRANSITION D'EXTINCTION (Retour à la normale) ===
        float timerDown = 0f;
        while (timerDown < transitionDownDuration)
        {
            timerDown += Time.deltaTime; // Incrémente le temps
            float t = timerDown / transitionDownDuration; // t va de 0 à 1

            // On LERP dans l'autre sens (t de 0 à 1 correspond à de l'impact vers l'origine) :

            // 1. Échelle
            transform.localScale = Vector3.Lerp(hitScale, originalScale, t);

            // 2. Couleur du corps
            ApplyColor(Color.Lerp(hitColor, originalColor, t));

            // 3. Intensité de la lumière
            if (bumperLight != null)
                bumperLight.intensity = Mathf.Lerp(targetLightIntensity, 0f, t);

            yield return null; // Attend la prochaine image
        }

        // === PHASE 3 : RÉINITIALISATION FINALE (Assurer les valeurs exactes) ===
        transform.localScale = originalScale;
        ApplyColor(originalColor);
        if (bumperLight != null)
            bumperLight.intensity = 0f;

        isAnimating = false;
    }

    // Fonction utilitaire pour appliquer une couleur à un Renderer (support URP)
    void ApplyColor(Color c)
    {
        if (bumperRenderer != null)
        {
            if (bumperRenderer.material.HasProperty("_Color"))
                bumperRenderer.material.color = c;
            else if (bumperRenderer.material.HasProperty("_BaseColor"))
                bumperRenderer.material.SetColor("_BaseColor", c);
        }
    }
}