using UnityEngine;

// Petit utilitaire pour reconnaître la bille : par son tag "Ball" (ou son nom, en secours).
public static class Bille
{
    public const string Tag = "Ball";

    public static bool Est(GameObject go)
    {
        return go != null && (go.CompareTag(Tag) || go.name == "la_bille");
    }
}
