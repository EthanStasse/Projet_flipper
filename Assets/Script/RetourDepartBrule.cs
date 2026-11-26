using UnityEngine;

// Zone de perte (drain) : collider "Is Trigger" placé sous les flippers.
// Quand la bille y entre, elle est perdue : le GameManager la remet dans le lanceur après 1 seconde.
[RequireComponent(typeof(BoxCollider))]
public class RetourDepartBrule : MonoBehaviour
{
    void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        bool estBille = Bille.Est(other.gameObject)
                     || (other.attachedRigidbody != null && Bille.Est(other.attachedRigidbody.gameObject));
        if (!estBille)
            return;

        if (GameManager.Instance != null)
            GameManager.Instance.BillePerdue();
    }

    void OnDrawGizmos()
    {
        var b = GetComponent<BoxCollider>();
        if (b == null) return;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
        Gizmos.DrawCube(b.center, b.size);
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(b.center, b.size);
    }
}
