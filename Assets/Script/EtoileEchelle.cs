using TMPro;
using UnityEngine;

// Une étoile de l'échelle de score : s'allume en fondu, scintille comme une braise, peut clignoter.
// Pilotée par EchelleScore. Créée par le menu Flipper > Échelle d'étoiles.
public class EtoileEchelle : MonoBehaviour
{
    public Color couleur = new Color(1f, 0.478f, 0.118f);   // #FF7A1E
    [Tooltip("Brillance du verre allumé / éteint.")]
    public float emissionAllumee = 4f;
    public float emissionEteinte = 0.12f;
    [Tooltip("Intensité de la Point Light sous l'étoile allumée (éteinte = lumière coupée).")]
    public float intensiteLumiere = 1.2f;
    [Tooltip("Durée du fondu d'allumage / d'extinction (secondes).")]
    public float dureeFondu = 0.15f;
    [Range(0f, 0.5f), Tooltip("Force du scintillement de braise quand l'étoile est allumée.")]
    public float scintillement = 0.18f;
    [Tooltip("Vitesse du clignotement (allumages par seconde).")]
    public float frequenceClignotement = 4f;

    public Light lumiere;
    public TMP_Text texte;

    public bool Allumee { get; private set; }

    Renderer rendu;
    MaterialPropertyBlock bloc;
    float niveau;           // 0 = éteinte, 1 = allumée (avec fondu)
    bool clignote;
    float graine;
    static readonly int IdEmission = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        rendu = GetComponent<Renderer>();
        bloc = new MaterialPropertyBlock();
        graine = Random.value * 100f;
        Appliquer(0f);
    }

    public void Allumer(bool allumer)
    {
        Allumee = allumer;
    }

    public void Clignoter(bool actif)
    {
        clignote = actif;
    }

    /// <summary>Éteint tout de suite, sans fondu (remise à zéro).</summary>
    public void Eteindre()
    {
        Allumee = false;
        clignote = false;
        niveau = 0f;
        Appliquer(0f);
    }

    void Update()
    {
        float cible = Allumee ? 1f : 0f;
        if (clignote) cible = Mathf.Repeat(Time.time * frequenceClignotement, 1f) < 0.5f ? 1f : 0f;
        niveau = Mathf.MoveTowards(niveau, cible, Time.deltaTime / Mathf.Max(0.01f, dureeFondu));

        float braise = 1f - scintillement * Mathf.PerlinNoise(graine, Time.time * 7f);
        Appliquer(niveau * braise);
    }

    void Appliquer(float k)
    {
        if (lumiere != null)
        {
            lumiere.intensity = intensiteLumiere * k;
            lumiere.enabled = k > 0.001f;
        }
        if (rendu != null)
        {
            rendu.GetPropertyBlock(bloc);
            bloc.SetColor(IdEmission, couleur * Mathf.Lerp(emissionEteinte, emissionAllumee, k));
            rendu.SetPropertyBlock(bloc);
        }
        if (texte != null)
        {
            Color c = couleur;
            c.a = Mathf.Lerp(0.35f, 1f, k);
            texte.color = c;
        }
    }
}
