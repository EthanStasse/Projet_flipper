using UnityEngine;

// Ajouté sur leur drain : quand la bille y entre, elle compte comme perdue pour le GameManager.
// (Leur script RetourDepart continue de la renvoyer au départ comme avant.)
public class DrainScore : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        bool estBille = Bille.Est(other.gameObject)
                     || (other.attachedRigidbody != null && Bille.Est(other.attachedRigidbody.gameObject));
        if (estBille && GameManager.Instance != null)
            GameManager.Instance.BillePerdue();
    }
}
