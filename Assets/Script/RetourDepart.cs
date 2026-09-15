using UnityEngine;

public class RetourDepart : MonoBehaviour
{
    public Transform pointDeDepart;

    void OnTriggerEnter(Collider other)
    {
        // Le script vérifie maintenant que seul l'objet nommé "la_bille" est téléporté
        if (other.gameObject.name == "la_bille")
        {
            // Téléporte la bille au point de départ
            other.transform.position = pointDeDepart.position;

            // Annule la vitesse et la rotation de la bille pour qu'elle reparte proprement
            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }
}