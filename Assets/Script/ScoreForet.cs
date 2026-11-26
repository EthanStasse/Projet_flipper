using UnityEngine;

// Score du flipper du groupe (scène Flipper_Foret), branché au lancement sans modifier leur scène ni leurs scripts :
//   - bumpers (BumperEffect) : 100 points, sapins (Sapin) : 250, rochers (Rocher) : 50
//   - monter la rampe (RailForet) jusqu'en haut : 500 (RampeScore)
//   - le drain (RetourDepart) fait perdre la bille : 3 billes par partie, comme dans la forêt en feu.
// Pour changer les points : modifier les valeurs ci-dessous.
public static class ScoreForet
{
    public const int PointsBumper = 100;
    public const int PointsSapin = 250;
    public const int PointsRocher = 50;
    public const int PointsRampe = 500;

    public static void Brancher()
    {
        int n = 0;
        foreach (var b in Object.FindObjectsByType<BumperEffect>(FindObjectsInactive.Include)) n += Donner(b.gameObject, PointsBumper);
        foreach (var s in Object.FindObjectsByType<Sapin>(FindObjectsInactive.Include)) n += Donner(s.gameObject, PointsSapin);
        foreach (var r in Object.FindObjectsByType<Rocher>(FindObjectsInactive.Include)) n += Donner(r.gameObject, PointsRocher);

        int rampes = 0;
        foreach (var r in Object.FindObjectsByType<RailForet>(FindObjectsInactive.Include))
        {
            if (RampeScore.Poser(r, PointsRampe)) rampes++;
            if (r.GetComponent<RampeAssistance>() == null) r.gameObject.AddComponent<RampeAssistance>();
        }

        int drains = 0;
        foreach (var d in Object.FindObjectsByType<RetourDepart>(FindObjectsInactive.Include))
            if (d.GetComponent<DrainScore>() == null) { d.gameObject.AddComponent<DrainScore>(); drains++; }

        Debug.Log($"[ScoreForet] Score branché : {n} objets qui rapportent des points, {rampes} rampe(s), {drains} drain(s).");
    }

    // ScoreOnHit sur chaque collider de l'objet (le collider peut être sur un enfant)
    static int Donner(GameObject go, int points)
    {
        int n = 0;
        foreach (var col in go.GetComponentsInChildren<Collider>(true))
        {
            var s = col.GetComponent<ScoreOnHit>();
            if (s == null) s = col.gameObject.AddComponent<ScoreOnHit>();
            s.points = points;
            n++;
        }
        return n;
    }
}
