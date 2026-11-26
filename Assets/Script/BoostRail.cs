using UnityEngine;

// Accélérateur de rail : tant que la bille touche ce collider (le rail), sa vitesse est régulée :
// accélérée jusqu'à "vitesseMin" quand elle monte, freinée au-dessus de "vitesseMax".
// Elle est aussi plaquée contre le fond (adhérence). Donne des points à l'entrée sur le rail.
// À mettre sur l'objet qui porte le collider du rail (ex. Rampe_Collision). Flipper > Installer le fait.
public class BoostRail : MonoBehaviour
{
    [Header("Boost")]
    [Tooltip("Vitesse (m/s) minimale en montée : la bille est accélérée tant qu'elle va moins vite.")]
    public float vitesseMin = 11f;
    [Tooltip("Vitesse (m/s) maximale sur le rail : au-dessus, la bille est freinée (sinon elle décolle dans les bosses).")]
    public float vitesseMax = 13f;
    [Tooltip("Accélération (m/s²) du boost et du frein. Doit être plus forte que la gravité pour monter les pentes.")]
    public float acceleration = 40f;
    [Tooltip("En dessous de cette vitesse, on ne sait plus dans quel sens pousser : pas de boost.")]
    public float vitesseMiniPourPousser = 0.5f;
    [Tooltip("Force (m/s²) qui plaque la bille contre le fond du rail, comme les fils d'un vrai rail : " +
             "elle ne décolle pas dans les bosses.")]
    public float adherence = 40f;

    [Header("Score")]
    public int pointsEntree = 500;

    [Header("Son")]
    public AudioClip son;
    [Range(0f, 1f)] public float volume = 0.8f;
    [Tooltip("Temps minimum entre deux bonus d'entrée (une montée = un bonus).")]
    public float delaiPoints = 2f;
    public string message = "BOOST !";

    private float dernierBonus = -10f;

    /// <summary>La bille vient d'entrer dans un rail (au plus une fois par "delaiPoints").</summary>
    public static event System.Action EntreeRail;

    void OnCollisionEnter(Collision collision)
    {
        if (!Bille.Est(collision.gameObject) || Time.time - dernierBonus < delaiPoints)
            return;

        dernierBonus = Time.time;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AjouterPoints(pointsEntree);
            GameManager.Instance.AfficherMessage($"{message}  +{pointsEntree}");
        }
        Sons.Jouer(this, son, volume, 0.02f);
        EntreeRail?.Invoke();
    }

    void OnCollisionStay(Collision collision)
    {
        if (!Bille.Est(collision.gameObject)) return;
        Rigidbody rb = collision.rigidbody;
        if (rb == null || rb.isKinematic) return;

        // Adhérence : pour chaque contact sous la bille (le fond, pas les parois), on la plaque contre le rail
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 versBille = rb.worldCenterOfMass - collision.GetContact(i).point;
            if (versBille.sqrMagnitude < 1e-6f) continue;
            versBille.Normalize();
            if (Vector3.Dot(versBille, Vector3.up) > 0.5f)
            {
                rb.AddForce(-versBille * adherence, ForceMode.Acceleration);
                break;
            }
        }

        // Régulation de vitesse
        Vector3 v = rb.linearVelocity;
        float vitesse = v.magnitude;
        if (vitesse > vitesseMax)
            rb.AddForce(-v / vitesse * acceleration, ForceMode.Acceleration);           // frein
        else if (vitesse > vitesseMiniPourPousser && vitesse < vitesseMin && v.y >= 0f)
            rb.AddForce(v / vitesse * acceleration, ForceMode.Acceleration);            // boost (seulement en montée)
    }
}
