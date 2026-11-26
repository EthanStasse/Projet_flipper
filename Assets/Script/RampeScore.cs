using UnityEngine;

// Bonus "RAMPE" sur le flipper du groupe : une zone invisible (trigger) est posée au point le plus haut
// de leur rampe (RailForet). La bille ne peut l'atteindre qu'en montant la rampe : chaque passage rapporte des points.
// Posé tout seul au lancement par ScoreForet, rien à mettre dans la scène.
public class RampeScore : MonoBehaviour
{
    public int points = 500;
    [Tooltip("Temps minimum entre deux bonus (la bille peut redescendre par le même chemin).")]
    public float delaiMinimum = 1.5f;
    float dernier = -10f;

    /// <summary>Crée la zone au sommet de la rampe. Renvoie false si la rampe n'a pas de points.</summary>
    public static bool Poser(RailForet rampe, int points)
    {
        if (rampe.points == null || rampe.points.Length < 2) return false;
        if (rampe.GetComponentInChildren<RampeScore>(true) != null) return true;

        Vector3 sommet = rampe.points[0];
        foreach (Vector3 p in rampe.points)
            if (p.y > sommet.y) sommet = p;

        var go = new GameObject("Bonus_Rampe (score)");
        go.transform.SetParent(rampe.transform, false);
        // Centre de la bille quand elle roule sur la piste à cet endroit (la bille fait 0.58 de diamètre)
        go.transform.localPosition = sommet + Vector3.up * 0.3f;
        var zone = go.AddComponent<SphereCollider>();
        zone.isTrigger = true;
        zone.radius = 0.5f;
        go.AddComponent<RampeScore>().points = points;
        return true;
    }

    void OnTriggerEnter(Collider other)
    {
        GameObject go = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
        if (!Bille.Est(go) || Time.time - dernier < delaiMinimum) return;
        dernier = Time.time;

        var gm = GameManager.Instance;
        if (gm == null || gm.EnMenu || gm.PartieTerminee) return;
        gm.AjouterPoints(points);
        gm.AfficherMessage($"RAMPE !  +{points * Mathf.Max(1, gm.Multiplicateur):N0}");
    }
}
