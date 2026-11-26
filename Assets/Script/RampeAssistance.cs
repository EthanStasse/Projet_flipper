using System.Collections.Generic;
using UnityEngine;

// Aide sur la rampe en bois du flipper Forêt (RailForet) : une fois que la bille est sur la partie haute
// de la rampe, elle garde une vitesse minimum dans le sens où elle est entrée.
// Elle ne peut plus s'arrêter au milieu et redescendre : elle fait toute la rampe jusqu'à la sortie.
// Posé tout seul au lancement par ScoreForet, rien à mettre dans la scène.
[RequireComponent(typeof(RailForet))]
public class RampeAssistance : MonoBehaviour
{
    [Tooltip("Vitesse minimum (m/s) de la bille sur la rampe.")]
    public float vitesseMin = 3.5f;
    [Tooltip("Au-dessus de cette hauteur de piste, la bille est sur la partie haute de la rampe.")]
    public float hauteurMin = 0.45f;
    [Tooltip("Distance max (sur le côté) entre la bille et le milieu de la piste.")]
    public float largeur = 0.5f;

    RailForet rampe;
    readonly List<Vector3> courbe = new List<Vector3>();   // points de la piste (local), serrés
    int sens;                                               // +1 vers la fin de la rampe, -1 vers le début, 0 = pas sur la rampe

    void Start()
    {
        rampe = GetComponent<RailForet>();
        Echantillonner();
    }

    void Echantillonner()
    {
        courbe.Clear();
        var p = rampe.points;
        if (p == null || p.Length < 2) return;
        int n = p.Length;
        for (int i = 0; i < n - 1; i++)
        {
            Vector3 p0 = p[Mathf.Max(i - 1, 0)], p1 = p[i], p2 = p[i + 1], p3 = p[Mathf.Min(i + 2, n - 1)];
            for (int k = 0; k < 20; k++)
                courbe.Add(CatmullRom(p0, p1, p2, p3, k / 20f));
        }
        courbe.Add(p[n - 1]);
    }

    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    void FixedUpdate()
    {
        var gm = GameManager.Instance;
        Rigidbody bille = gm != null ? gm.bille : null;
        if (bille == null || bille.isKinematic || courbe.Count < 2) { sens = 0; return; }

        // Point de la piste le plus proche de la bille (en vue de dessus, dans le repère de la rampe)
        Vector3 local = transform.InverseTransformPoint(bille.position);
        int meilleur = -1;
        float dMin = largeur * largeur;
        for (int i = 0; i < courbe.Count - 1; i++)
        {
            float dx = local.x - courbe[i].x, dz = local.z - courbe[i].z;
            float d = dx * dx + dz * dz;
            if (d < dMin) { dMin = d; meilleur = i; }
        }

        // Sur la partie haute de la rampe ? (et la bille est bien sur la piste, pas en dessous)
        bool surRampe = meilleur >= 0
                     && courbe[meilleur].y >= hauteurMin
                     && local.y > courbe[meilleur].y - 0.05f
                     && local.y < courbe[meilleur].y + 0.9f;
        if (!surRampe) { sens = 0; return; }

        Vector3 tangente = transform.TransformDirection(courbe[meilleur + 1] - courbe[meilleur]).normalized;
        float vitesseLeLong = Vector3.Dot(bille.linearVelocity, tangente);

        // Le sens est fixé à l'entrée sur la rampe (la bille peut entrer par les deux bouts)
        if (sens == 0) sens = vitesseLeLong >= 0f ? 1 : -1;

        if (vitesseLeLong * sens < vitesseMin)
        {
            // On garde le mouvement sur le côté, on remet juste assez de vitesse le long de la piste
            Vector3 cote = bille.linearVelocity - tangente * vitesseLeLong;
            bille.linearVelocity = cote + tangente * (vitesseMin * sens);
        }
    }
}
