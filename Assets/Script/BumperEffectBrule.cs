using UnityEngine;
using System.Collections;

// Bumper : donne des points et repousse la bille quand elle le touche, avec un flash (couleur, lumière, taille).
// À mettre sur l'objet racine du bumper. Flipper > Installer l'ajoute automatiquement (avec les colliders).
public class BumperEffectBrule : MonoBehaviour
{
    /// <summary>Un bumper (ou un arbre en feu) vient de marquer des points.</summary>
    public static event System.Action Touche;

    [Header("Score")]
    public int points = 100;

    [Header("Son")]
    public AudioClip son;
    [Range(0f, 1f)] public float volume = 0.8f;
    [Tooltip("Temps minimum entre deux touches comptées.")]
    public float delaiMinimum = 0.1f;

    [Header("Propulsion de la bille (vitesse ajoutée, en m/s)")]
    public float impulsion = 8f;
    public float variationImpulsion = 2f;

    [Header("Effet lumineux (optionnel)")]
    public Light lumiere;
    public float intensiteLumiere = 20f;

    [Header("Effet visuel")]
    public Color couleurTouche = Color.white;
    public float multiplicateurEchelle = 1.2f;
    [Tooltip("Enfant à agrandir lors du choc. Laisser vide = automatique (seulement s'il n'a pas de collider).")]
    public Transform visuel;

    [Header("Durées de l'effet")]
    public float dureeMontee = 0.05f;
    public float dureeDescente = 0.2f;

    private Vector3 echelleOrigine;
    private Color couleurOrigine;
    private Renderer rendu;
    private bool enAnimation;
    private float dernierChoc = -10f;

    void Start()
    {
        if (lumiere != null)
            lumiere.intensity = 0f;

        rendu = GetComponentInChildren<Renderer>();
        if (rendu != null)
        {
            if (visuel == null && rendu.transform != transform)
                visuel = rendu.transform;

            if (rendu.material.HasProperty("_BaseColor"))
                couleurOrigine = rendu.material.GetColor("_BaseColor");
            else if (rendu.material.HasProperty("_Color"))
                couleurOrigine = rendu.material.color;
        }

        // On n'agrandit pas un objet qui porte un collider (sinon le collider grossit aussi)
        if (visuel != null && (visuel == transform || visuel.GetComponent<Collider>() != null))
            visuel = null;

        if (visuel != null)
            echelleOrigine = visuel.localScale;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!Bille.Est(collision.gameObject))
            return;

        Rigidbody rbBille = collision.rigidbody;
        if (rbBille == null || rbBille.isKinematic)
            return;

        // Direction : du centre du bumper vers la bille, à plat
        Vector3 direction = rbBille.worldCenterOfMass - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = collision.GetContact(0).point - transform.position;
            direction.y = 0f;
        }
        if (direction.sqrMagnitude < 0.0001f)
            return;

        if (Time.time - dernierChoc >= delaiMinimum)
        {
            dernierChoc = Time.time;
            if (GameManager.Instance != null)
                GameManager.Instance.AjouterPoints(points);
            Sons.Jouer(this, son, volume);
            Touche?.Invoke();
        }

        float force = Mathf.Max(0.1f, impulsion + Random.Range(-variationImpulsion, variationImpulsion));
        rbBille.AddForce(direction.normalized * force, ForceMode.VelocityChange);

        if (!enAnimation)
            StartCoroutine(AnimerBumper());
    }

    IEnumerator AnimerBumper()
    {
        enAnimation = true;

        Vector3 echelleChoc = new Vector3(
            echelleOrigine.x * multiplicateurEchelle,
            echelleOrigine.y,
            echelleOrigine.z * multiplicateurEchelle);

        for (float t = 0f; t < dureeMontee; t += Time.deltaTime)
        {
            Appliquer(Mathf.Clamp01(t / dureeMontee), echelleChoc);
            yield return null;
        }
        for (float t = 0f; t < dureeDescente; t += Time.deltaTime)
        {
            Appliquer(1f - Mathf.Clamp01(t / dureeDescente), echelleChoc);
            yield return null;
        }
        Appliquer(0f, echelleChoc);

        enAnimation = false;
    }

    // k = 0 : état normal, k = 1 : état "touché"
    void Appliquer(float k, Vector3 echelleChoc)
    {
        if (visuel != null)
            visuel.localScale = Vector3.Lerp(echelleOrigine, echelleChoc, k);

        if (lumiere != null)
            lumiere.intensity = Mathf.Lerp(0f, intensiteLumiere, k);

        if (rendu == null)
            return;
        Color couleur = Color.Lerp(couleurOrigine, couleurTouche, k);
        if (rendu.material.HasProperty("_BaseColor"))
            rendu.material.SetColor("_BaseColor", couleur);
        else if (rendu.material.HasProperty("_Color"))
            rendu.material.color = couleur;
    }
}
