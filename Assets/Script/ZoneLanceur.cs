using UnityEngine;

// Zone trigger placée dans le couloir du lanceur.
// - La bille ne peut être tirée que si elle est dans cette boîte.
// - L'axe bleu (Z) de cet objet = direction dans laquelle la bille est tirée (flèche jaune dans la vue Scene).
[RequireComponent(typeof(BoxCollider))]
public class ZoneLanceur : MonoBehaviour
{
    private BoxCollider zone;

    void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    void Awake()
    {
        zone = GetComponent<BoxCollider>();
        zone.isTrigger = true;
    }

    // Vérification géométrique : fiable même si la bille est immobile (Rigidbody "endormi").
    public bool ContientBille()
    {
        if (zone == null) zone = GetComponent<BoxCollider>();

        Vector3 centre = transform.TransformPoint(zone.center);
        Vector3 echelle = transform.lossyScale;
        Vector3 demiTaille = new Vector3(
            Mathf.Abs(zone.size.x * echelle.x),
            Mathf.Abs(zone.size.y * echelle.y),
            Mathf.Abs(zone.size.z * echelle.z)) * 0.5f;

        foreach (var c in Physics.OverlapBox(centre, demiTaille, transform.rotation, ~0, QueryTriggerInteraction.Ignore))
        {
            if (Bille.Est(c.gameObject) || (c.attachedRigidbody != null && Bille.Est(c.attachedRigidbody.gameObject)))
                return true;
        }
        return false;
    }

    void OnDrawGizmos()
    {
        var b = GetComponent<BoxCollider>();
        if (b == null) return;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0f, 1f, 0.3f, 0.25f);
        Gizmos.DrawCube(b.center, b.size);
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(b.center, b.size);

        // Flèche : direction du tir
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = Color.yellow;
        Vector3 depart = transform.TransformPoint(b.center);
        Vector3 fin = depart + transform.forward * 3f;
        Gizmos.DrawLine(depart, fin);
        Gizmos.DrawLine(fin, fin - transform.forward * 0.5f + transform.right * 0.3f);
        Gizmos.DrawLine(fin, fin - transform.forward * 0.5f - transform.right * 0.3f);
    }
}
