using UnityEngine;

// Donne des points quand la bille touche cet objet (bumper, cible, poteau, rampe...).
// Marche avec un collider normal (choc) ou un collider "Is Trigger" (passage).
public class ScoreOnHit : MonoBehaviour
{
    [Tooltip("Points gagnés à chaque touche.")]
    public int points = 100;
    [Tooltip("Temps minimum entre deux touches comptées (évite de compter 10 fois le même choc).")]
    public float delaiMinimum = 0.2f;

    float dernierHit = -10f;

    void OnCollisionEnter(Collision collision) => Toucher(collision.gameObject);
    void OnTriggerEnter(Collider other) => Toucher(other.gameObject);

    void Toucher(GameObject go)
    {
        if (!Bille.Est(go) || Time.time - dernierHit < delaiMinimum)
            return;

        dernierHit = Time.time;
        if (GameManager.Instance != null)
            GameManager.Instance.AjouterPoints(points);
    }
}
