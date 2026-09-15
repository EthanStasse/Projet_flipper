using UnityEngine;
using System.Collections;

public class BumperEffect : MonoBehaviour
{
    [Header("Paramètres visuels")]
    public Light bumperLight;
    public float scaleMultiplier = 1.2f; // De combien il grossit
    public float flashDuration = 0.15f;  // Durée de l'effet
    public float lightIntensity = 5f;    // Puissance de la lumière

    private Vector3 originalScale;
    private bool isAnimating = false;

    void Start()
    {
        // On sauvegarde la taille de départ
        originalScale = transform.localScale;

        if (bumperLight != null)
            bumperLight.intensity = 0f;
    }

    void OnCollisionEnter(Collision collision)
    {
        // On vérifie que c'est bien la bille qui touche
        if (collision.gameObject.name == "la_bille" && !isAnimating)
        {
            StartCoroutine(AnimateBumper());
        }
    }

    IEnumerator AnimateBumper()
    {
        isAnimating = true;

        // 1. Grossissement (uniquement sur X et Z) et allumage
        transform.localScale = new Vector3(originalScale.x * scaleMultiplier, originalScale.y, originalScale.z * scaleMultiplier);

        if (bumperLight != null)
            bumperLight.intensity = lightIntensity;

        // 2. Attente
        yield return new WaitForSeconds(flashDuration);

        // 3. Retour à la normale
        transform.localScale = originalScale;

        if (bumperLight != null)
            bumperLight.intensity = 0f;

        isAnimating = false;
    }
}