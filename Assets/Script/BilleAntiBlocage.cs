using UnityEngine;

// Filet de sécurité : si la bille reste quasiment immobile trop longtemps quelque part
// sur la table (coincée), on lui donne une petite poussée pour la décoincer.
// Ne fait rien dans le couloir de lancement ni dans la zone des flippers
// (la bille peut y être retenue volontairement).
[RequireComponent(typeof(Rigidbody))]
public class BilleAntiBlocage : MonoBehaviour
{
    [Tooltip("Point de départ de la bille (couloir de lancement) : pas de poussée autour.")]
    public Transform pointDeDepart;
    public float rayonZoneDepart = 1.5f;

    [Tooltip("En dessous de ce z (monde), on est dans la zone des flippers : pas de poussée.")]
    public float zLimiteFlippers = -11.5f;

    [Tooltip("Vitesse en dessous de laquelle la bille est considérée immobile.")]
    public float vitesseMin = 0.15f;
    [Tooltip("Temps d'immobilité avant la poussée (secondes).")]
    public float delai = 1.5f;
    [Tooltip("Force de la poussée (changement de vitesse).")]
    public float poussee = 2.5f;

    Rigidbody rb;
    float tempsImmobile;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        Vector3 p = rb.position;
        bool zoneProtegee = p.z < zLimiteFlippers ||
                            (pointDeDepart != null && Vector3.Distance(p, pointDeDepart.position) < rayonZoneDepart);

        if (zoneProtegee || rb.isKinematic || rb.linearVelocity.magnitude > vitesseMin)
        {
            tempsImmobile = 0f;
            return;
        }

        tempsImmobile += Time.fixedDeltaTime;
        if (tempsImmobile < delai)
            return;

        tempsImmobile = 0f;
        // poussée vers le bas de la table avec un écart aléatoire sur le côté
        Vector3 dir = new Vector3(Random.Range(-1f, 1f), 0f, -0.6f).normalized;
        rb.AddForce(dir * poussee, ForceMode.VelocityChange);
    }
}
