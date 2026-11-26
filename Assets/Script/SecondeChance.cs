using TMPro;
using UnityEngine;

// 2nd chance : zone invisible dans le couloir de droite, sur l'ampoule. Quand la bille passe dessus,
// elle a une chance (au hasard) de gagner une bille en plus : la prochaine bille perdue est rejouée.
// Une seule 2nd chance en réserve à la fois ; l'ampoule clignote en doré tant qu'elle est en réserve.
// Créée par le menu Flipper > 2nd chance.
public class SecondeChance : MonoBehaviour
{
    [Range(0f, 1f), Tooltip("Chance de gagner la 2nd chance à chaque passage (0.3 = 30 %).")]
    public float probabilite = 0.3f;
    [Tooltip("Temps minimum entre deux tirages (la bille peut repasser plusieurs fois très vite).")]
    public float delaiEntreTirages = 1.5f;
    [Tooltip("L'ampoule du couloir : clignote en doré quand la 2nd chance est en réserve.")]
    public Ampoule ampoule;
    public Color couleurActive = new Color(1f, 0.82f, 0.25f);
    [Tooltip("Inscription \"2ND CHANCE\" peinte dans le couloir : pâle, puis dorée et clignotante quand elle est gagnée.")]
    public TMP_Text texte;
    [Tooltip("Lumière du néon : douce au repos, forte et clignotante quand la 2nd chance est gagnée.")]
    public Light lumiereTexte;
    public AudioClip sonGagne;
    public AudioClip sonPassage;
    [Range(0f, 1f)] public float volume = 0.8f;

    float dernierTirage = -10f;
    Ampoule.Mode modeNormal;
    Color couleurNormale;
    bool affichageActif;

    void Start()
    {
        if (ampoule != null)
        {
            modeNormal = ampoule.mode;
            couleurNormale = ampoule.couleur;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        bool estBille = Bille.Est(other.gameObject)
                     || (other.attachedRigidbody != null && Bille.Est(other.attachedRigidbody.gameObject));
        var gm = GameManager.Instance;
        if (!estBille || gm == null || gm.EnMenu || gm.PartieTerminee) return;
        if (Time.time - dernierTirage < delaiEntreTirages) return;
        dernierTirage = Time.time;

        if (gm.BillesEnPlus > 0)
        {
            Sons.Jouer(this, sonPassage, volume * 0.5f);   // déjà en réserve : rien de plus
            return;
        }
        if (Random.value < probabilite)
        {
            gm.AjouterBilleEnPlus();
            gm.AfficherMessage("2ND CHANCE !", 2f);
            Sons.Jouer(this, sonGagne, volume, 0f);
        }
        else Sons.Jouer(this, sonPassage, volume * 0.5f);
    }

    void Update()
    {
        var gm = GameManager.Instance;
        bool active = gm != null && gm.BillesEnPlus > 0;
        // Néon toujours allumé (orange) ; doré et clignotant quand la 2nd chance est gagnée
        bool fort = !active || Mathf.Repeat(Time.time * 2f, 1f) < 0.6f;
        float vacille = 0.92f + 0.08f * Mathf.PerlinNoise(Time.time * 9f, 0.3f);   // petit grésillement de néon
        if (texte != null)
        {
            Color c = active ? couleurActive : new Color(1f, 0.55f, 0.2f);
            c.a = (fort ? 1f : 0.35f) * vacille;
            texte.color = c;
        }
        if (lumiereTexte != null)
        {
            lumiereTexte.color = active ? couleurActive : new Color(1f, 0.55f, 0.2f);
            lumiereTexte.intensity = (active ? (fort ? 2.5f : 0.6f) : 1f) * vacille;
        }
        if (ampoule == null || active == affichageActif) return;
        affichageActif = active;
        ampoule.mode = active ? Ampoule.Mode.Clignote : modeNormal;
        ampoule.couleur = active ? couleurActive : couleurNormale;
        var l = ampoule.GetComponentInChildren<Light>();
        if (l != null) l.color = ampoule.couleur;
    }
}
