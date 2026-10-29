using UnityEngine;

public class OneWayGate : MonoBehaviour
{
    public Collider solidCollider;          // le collider qui bloque
    public Vector3 allowedDirection = Vector3.forward; // direction autorisée (en local)

    void OnTriggerEnter(Collider other)
    {
        Rigidbody rb = other.attachedRigidbody;
        if (rb == null) return;

        Vector3 dir = transform.TransformDirection(allowedDirection).normalized;

        // La bille va dans le sens autorisé : on laisse passer
        if (Vector3.Dot(rb.linearVelocity, dir) > 0f)
            Physics.IgnoreCollision(other, solidCollider, true);
    }

    void OnTriggerExit(Collider other)
    {
        // La bille est sortie de la zone : la porte redevient solide
        Physics.IgnoreCollision(other, solidCollider, false);
    }
}