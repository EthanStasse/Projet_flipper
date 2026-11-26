using UnityEngine;

// Cible tombante : quand la bille la touche, elle donne ses points et s'enfonce dans le plateau
// (sa collision disparaît, la bille passe par-dessus). Elle remonte quand son banc (BancCibles) la relève.
// Créée par le menu Flipper > Haut du plateau.
[RequireComponent(typeof(Rigidbody))]
public class CibleTombante : MonoBehaviour
{
    public int points = 250;
    [Tooltip("Profondeur dont la cible s'enfonce sous le plateau.")]
    public float profondeur = 0.5f;
    [Tooltip("Durée de la chute / de la remontée (secondes).")]
    public float dureeChute = 0.07f;
    public float dureeRemontee = 0.15f;
    [Tooltip("Vitesse minimale de la bille pour faire tomber la cible.")]
    public float vitesseMini = 0.3f;
    public AudioClip son;
    [Range(0f, 1f)] public float volume = 0.8f;

    [Tooltip("Rendu de la face (brille quand la cible est debout).")]
    public Renderer face;
    public Color couleurFace = new Color(1f, 0.478f, 0.118f);
    public float emissionFace = 2.5f;

    public bool Tombee { get; private set; }
    public BancCibles banc;

    Rigidbody rb;
    Collider[] colliders;
    Vector3 positionHaute;
    float niveau = 1f;      // 1 = debout, 0 = enfoncée
    MaterialPropertyBlock bloc;
    static readonly int IdEmission = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        colliders = GetComponentsInChildren<Collider>();
        positionHaute = transform.localPosition;
        bloc = new MaterialPropertyBlock();
        AppliquerFace(1f);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (Tombee || !Bille.Est(collision.gameObject) || collision.relativeVelocity.magnitude < vitesseMini)
            return;
        Tomber();
    }

    void Tomber()
    {
        Tombee = true;
        foreach (Collider c in colliders) c.enabled = false;
        if (GameManager.Instance != null) GameManager.Instance.AjouterPoints(points);
        Sons.Jouer(this, son, volume);
        if (banc != null) banc.CibleTombee(this);
    }

    /// <summary>Remonte la cible (si la bille n'est pas juste au-dessus).</summary>
    public void Relever()
    {
        Tombee = false;
        foreach (Collider c in colliders) c.enabled = true;
    }

    /// <summary>La bille est-elle là où la cible va remonter ?</summary>
    public bool BilleAuDessus()
    {
        foreach (Collider c in colliders)
        {
            var box = c as BoxCollider;
            if (box == null) continue;
            Vector3 centre = transform.parent != null
                ? transform.parent.TransformPoint(positionHaute + Vector3.Scale(box.center, transform.localScale))
                : box.transform.TransformPoint(box.center);
            Vector3 demi = Vector3.Scale(box.size, box.transform.lossyScale) * 0.5f + Vector3.one * 0.05f;
            foreach (Collider h in Physics.OverlapBox(centre, demi, box.transform.rotation, ~0, QueryTriggerInteraction.Ignore))
                if (Bille.Est(h.gameObject)) return true;
        }
        return false;
    }

    void FixedUpdate()
    {
        float cible = Tombee ? 0f : 1f;
        float duree = Tombee ? dureeChute : dureeRemontee;
        if (Mathf.Approximately(niveau, cible)) return;
        niveau = Mathf.MoveTowards(niveau, cible, Time.fixedDeltaTime / Mathf.Max(0.01f, duree));
        Vector3 local = positionHaute - Vector3.up * (profondeur * (1f - niveau));
        Vector3 monde = transform.parent != null ? transform.parent.TransformPoint(local) : local;
        rb.MovePosition(monde);
        AppliquerFace(niveau);
    }

    void AppliquerFace(float k)
    {
        if (face == null) return;
        face.GetPropertyBlock(bloc);
        bloc.SetColor(IdEmission, couleurFace * (emissionFace * Mathf.Lerp(0.1f, 1f, k)));
        face.SetPropertyBlock(bloc);
    }
}
