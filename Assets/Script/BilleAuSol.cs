using UnityEngine;

// Garde la bille collée au sol (la table, ou la piste de la rampe) quelle que soit la collision :
// bumper, flipper, mur, bosse de la rampe... elle ne peut plus sauter.
// À chaque pas de physique, on cherche le sol juste sous la bille :
// - si la vitesse l'éloigne du sol, on la réoriente le long du sol en gardant la même vitesse
//   (la bille garde son élan, elle peut toujours monter la rampe) ;
// - si elle a légèrement décollé, on la plaque contre le sol.
[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class BilleAuSol : MonoBehaviour
{
    [Tooltip("Distance sous la bille dans laquelle on considère qu'il y a un sol.")]
    public float distanceDetection = 0.35f;
    [Tooltip("Accélération qui plaque la bille contre le sol quand elle décolle un peu.")]
    public float forceCollage = 40f;
    [Tooltip("Écart avec le sol au-delà duquel la bille est considérée comme décollée.")]
    public float seuilDecollage = 0.04f;
    [Tooltip("Au-delà de cette inclinaison (degrés), la surface est un mur et non un sol.")]
    public float penteMax = 60f;

    Rigidbody rb;
    SphereCollider sphere;
    float rayon;
    readonly RaycastHit[] touches = new RaycastHit[16];

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        sphere = GetComponent<SphereCollider>();
        Vector3 s = transform.lossyScale;
        rayon = sphere.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
    }

    void FixedUpdate()
    {
        if (rb.isKinematic)
            return;

        // petite sphère lancée vers le bas depuis le centre de la bille
        float r = rayon * 0.9f;
        float marge = rayon - r;
        int n = Physics.SphereCastNonAlloc(rb.position, r, Vector3.down, touches, marge + distanceDetection,
                                           ~0, QueryTriggerInteraction.Ignore);

        // le sol le plus proche (on ignore la bille elle-même et les murs)
        bool trouve = false;
        RaycastHit sol = default;
        for (int i = 0; i < n; i++)
        {
            RaycastHit h = touches[i];
            if (h.collider == sphere || h.distance <= 0f)
                continue;
            if (Vector3.Angle(h.normal, Vector3.up) > penteMax)
                continue;
            if (!trouve || h.distance < sol.distance)
            {
                sol = h;
                trouve = true;
            }
        }
        if (!trouve)
            return;

        Vector3 normale = sol.normal;
        Vector3 v = rb.linearVelocity;
        float versLeHaut = Vector3.Dot(v, normale);
        if (versLeHaut > 0.05f)
        {
            // pas de rebond : la vitesse est remise le long du sol, sans perdre de vitesse,
            // et la rotation est accordée au roulement (sinon la bille glisse et freine)
            Vector3 tangente = v - normale * versLeHaut;
            Vector3 nouvelle = tangente.sqrMagnitude > 1e-6f ? tangente.normalized * v.magnitude : Vector3.zero;
            rb.linearVelocity = nouvelle;
            rb.angularVelocity = Vector3.Cross(normale, nouvelle) / rayon;
        }

        // la bille a vraiment décollé : on la ramène contre le sol (plus elle est haute, plus fort)
        float ecart = sol.distance - marge;
        if (ecart > seuilDecollage)
            rb.AddForce(-normale * forceCollage * Mathf.Clamp01(ecart / 0.1f), ForceMode.Acceleration);
    }
}
