using UnityEngine;

// Plus la bille reste en vie longtemps, plus elle prend feu : braises, puis flammes qui la suivent,
// lueur orange, traînée qui vire au rouge. Tout repart de zéro quand la bille est perdue.
// Posé sur la bille par le menu Flipper > Bille en feu.
[RequireComponent(typeof(Rigidbody))]
public class BilleEnFeu : MonoBehaviour
{
    [Tooltip("Secondes de jeu avant que la bille commence à chauffer.")]
    public float debut = 10f;
    [Tooltip("Secondes pour passer des premières braises au feu complet.")]
    public float montee = 25f;
    public string messageFeu = "BILLE EN FEU !";

    [Header("Effets (créés par le menu)")]
    public ParticleSystem flammes;
    public Light lueur;
    public Color couleurFeu = new Color(1f, 0.45f, 0.1f);
    [Tooltip("Brillance de la bille au feu complet.")]
    public float emissionMax = 3f;
    public float lumiereMax = 2.5f;
    [Tooltip("Flammes par seconde au feu complet.")]
    public float flammesMax = 70f;

    /// <summary>0 = froide, 1 = en feu complet.</summary>
    public float Chaleur { get; private set; }

    Rigidbody rb;
    Renderer rendu;
    TrailRenderer trainee;
    Color traineeDebut, traineeFin;
    MaterialPropertyBlock bloc;
    float vivanteDepuis = -1f;
    bool annonce;
    static readonly int IdEmission = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rendu = GetComponent<Renderer>();
        trainee = GetComponent<TrailRenderer>();
        if (trainee != null)
        {
            traineeDebut = trainee.startColor;
            traineeFin = trainee.endColor;
        }
        bloc = new MaterialPropertyBlock();
        Appliquer(0f);
    }

    void Update()
    {
        var gm = GameManager.Instance;
        bool enJeu = gm != null && !gm.EnMenu && !gm.PartieTerminee && !rb.isKinematic;
        if (!enJeu)
        {
            vivanteDepuis = -1f;
            Chaleur = 0f;
            Appliquer(0f);
            return;
        }
        // Le compte commence quand la bille a quitté le lanceur
        if (vivanteDepuis < 0f)
        {
            if (Vector3.Distance(rb.position, gm.PositionDepart) < 2f) { Appliquer(0f); return; }
            vivanteDepuis = Time.time;
            annonce = false;
        }
        Chaleur = Mathf.Clamp01((Time.time - vivanteDepuis - debut) / Mathf.Max(0.1f, montee));
        Appliquer(Chaleur);
        if (Chaleur >= 1f && !annonce)
        {
            annonce = true;
            gm.AfficherMessage(messageFeu, 2f);
        }
    }

    void Appliquer(float k)
    {
        float vacille = 0.8f + 0.2f * Mathf.PerlinNoise(Time.time * 8f, 0.7f);
        if (flammes != null)
        {
            var emission = flammes.emission;
            emission.enabled = k > 0.01f;
            emission.rateOverTime = flammesMax * k;
            var main = flammes.main;
            main.startSize = new ParticleSystem.MinMaxCurve(Mathf.Lerp(0.08f, 0.25f, k), Mathf.Lerp(0.15f, 0.5f, k));
        }
        if (lueur != null)
        {
            lueur.enabled = k > 0.01f;
            lueur.intensity = lumiereMax * k * vacille;
        }
        if (rendu != null)
        {
            rendu.GetPropertyBlock(bloc);
            bloc.SetColor(IdEmission, couleurFeu * (emissionMax * k * vacille));
            rendu.SetPropertyBlock(bloc);
        }
        if (trainee != null)
        {
            trainee.startColor = Color.Lerp(traineeDebut, new Color(1f, 0.8f, 0.3f, 1f), k);
            trainee.endColor = Color.Lerp(traineeFin, new Color(0.9f, 0.15f, 0.02f, 0f), k);
        }
    }
}
