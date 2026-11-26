using UnityEngine;

// Insert de bonus au sol (rollover) : la bille passe dessus -> points + l'insert s'allume.
// Éteint, il clignote doucement (en chenillard avec ses voisins) pour montrer qu'il reste à prendre.
// Allumé, une petite lumière sous l'insert éclaire le plateau autour.
// Si l'insert fait partie d'un GroupeBonus (objet parent), le groupe donne un gros bonus quand tous sont allumés.
// Il faut un collider "Is Trigger" sur cet objet, l'insert visible (Renderer) et la lumière (Light) en enfants.
[RequireComponent(typeof(Collider))]
public class CibleBonus : MonoBehaviour
{
    /// <summary>La bille vient de passer sur une pastille de bonus.</summary>
    public static event System.Action Touchee;

    [Tooltip("Points à chaque passage.")]
    public int points = 250;

    [Header("Son")]
    public AudioClip son;
    [Range(0f, 1f)] public float volume = 0.7f;
    [Tooltip("Temps minimum entre deux passages comptés.")]
    public float delaiMinimum = 0.3f;

    [Header("Couleurs")]
    public Color couleurAllumee = new Color(0.75f, 0.45f, 1f);
    [Tooltip("Luminosité de l'insert éteint (fraction de la couleur allumée).")]
    [Range(0f, 1f)] public float luminositeEteinte = 0.35f;
    [Tooltip("Intensité de la lueur de l'insert allumé.")]
    public float intensiteAllumee = 1.4f;

    [Header("Lumière sous l'insert")]
    public float intensiteLumiere = 1.5f;

    [Header("Clignotement quand éteint")]
    public bool clignoterEteinte = true;
    [Tooltip("Clignotements par seconde.")]
    public float vitesseClignotement = 1.2f;

    public bool Allumee { get; private set; }

    private Renderer rendu;
    private Light lumiere;
    private MaterialPropertyBlock bloc;
    private GroupeBonus groupe;
    private float dernierPassage = -10f;
    private float phase;
    private float forceJusqua = -1f;   // pendant le clignotement du groupe, on ne touche à rien

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        rendu = GetComponentInChildren<Renderer>();
        lumiere = GetComponentInChildren<Light>();
        if (lumiere != null) lumiere.color = couleurAllumee;
        groupe = GetComponentInParent<GroupeBonus>();
        bloc = new MaterialPropertyBlock();
        phase = transform.GetSiblingIndex() * 0.33f;   // chenillard : chaque insert est décalé
        AppliquerNiveau(luminositeEteinte);
    }

    void Update()
    {
        if (Allumee || !clignoterEteinte || Time.time < forceJusqua) return;
        float k = 0.5f + 0.5f * Mathf.Sin((Time.time * vitesseClignotement - phase) * 2f * Mathf.PI);
        AppliquerNiveau(luminositeEteinte * (0.35f + 0.65f * k));
    }

    void OnTriggerEnter(Collider other)
    {
        bool estBille = Bille.Est(other.gameObject)
                     || (other.attachedRigidbody != null && Bille.Est(other.attachedRigidbody.gameObject));
        if (!estBille || Time.time - dernierPassage < delaiMinimum)
            return;
        dernierPassage = Time.time;

        if (GameManager.Instance != null)
            GameManager.Instance.AjouterPoints(points);
        Touchee?.Invoke();
        // Plus fort quand l'insert s'allume que quand il l'était déjà
        Sons.Jouer(this, son, Allumee ? volume * 0.5f : volume);

        if (!Allumee)
        {
            Allumer(true);
            if (groupe != null) groupe.CibleAllumee();
        }
    }

    public void Allumer(bool allumer)
    {
        Allumee = allumer;
        AppliquerNiveau(allumer ? 1f : luminositeEteinte);
    }

    // Utilisé par le groupe pour faire clignoter les inserts (sans changer l'état)
    public void Appliquer(bool lumineux)
    {
        forceJusqua = Time.time + 0.3f;
        AppliquerNiveau(lumineux ? 1f : 0.1f);
    }

    // niveau : 0 = noir, 1 = allumé à fond
    void AppliquerNiveau(float niveau)
    {
        if (rendu != null)
        {
            rendu.GetPropertyBlock(bloc);
            Color c = couleurAllumee * Mathf.Lerp(0.25f, 1f, niveau);
            c.a = 1f;
            bloc.SetColor("_BaseColor", c);
            bloc.SetColor("_EmissionColor", couleurAllumee * (niveau * niveau * intensiteAllumee));
            rendu.SetPropertyBlock(bloc);
        }
        if (lumiere != null)
            lumiere.intensity = niveau >= 0.99f ? intensiteLumiere : intensiteLumiere * niveau * 0.3f;
    }
}
