using UnityEngine;

// Ampoule du plateau : fait varier sa lumière (Point Light enfant) et la brillance de son verre.
// Posée par le menu Flipper > Ampoules et lanceur.
public class Ampoule : MonoBehaviour
{
    public enum Mode { Fixe, Clignote, Chenillard, Vacille }

    public Mode mode = Mode.Fixe;
    public Color couleur = new Color(1f, 0.75f, 0.4f);
    [Tooltip("Intensité de la Point Light quand l'ampoule est allumée à fond.")]
    public float intensiteLumiere = 2f;
    [Tooltip("Force de la brillance du verre (émission) allumé à fond.")]
    public float emission = 3f;
    [Tooltip("Durée d'un cycle (secondes) pour Clignote et Chenillard.")]
    public float periode = 1f;
    [Range(0f, 1f), Tooltip("Décalage dans le cycle (0..1) : sert à faire avancer un chenillard.")]
    public float decalage = 0f;

    Light lumiere;
    Renderer rendu;
    MaterialPropertyBlock bloc;
    float graine;
    static readonly int IdEmission = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        lumiere = GetComponentInChildren<Light>();
        rendu = GetComponent<Renderer>();
        bloc = new MaterialPropertyBlock();
        graine = Random.value * 100f;
    }

    void Update()
    {
        float t = Time.time / Mathf.Max(0.05f, periode) + decalage;
        float k;
        switch (mode)
        {
            case Mode.Clignote:
                k = Mathf.Repeat(t, 1f) < 0.5f ? 1f : 0.08f;
                break;
            case Mode.Chenillard:
                // allumée un tiers du cycle, avec une petite traîne
                float p = Mathf.Repeat(t, 1f);
                k = p < 0.33f ? 1f : Mathf.Lerp(0.6f, 0.05f, (p - 0.33f) / 0.25f);
                break;
            case Mode.Vacille:
                k = 0.65f + 0.35f * Mathf.PerlinNoise(graine, Time.time * 6f);
                break;
            default:
                k = 1f;
                break;
        }

        if (lumiere != null) lumiere.intensity = intensiteLumiere * k;
        if (rendu != null)
        {
            rendu.GetPropertyBlock(bloc);
            bloc.SetColor(IdEmission, couleur * (emission * k));
            rendu.SetPropertyBlock(bloc);
        }
    }
}
