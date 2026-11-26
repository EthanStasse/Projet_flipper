using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Haut du plateau, façon vrai flipper (le bas, jusqu'à l'arbre "ArbreEnFeu_1 (5)", ne change pas) :
//  - les arbres en feu du centre du haut sont retirés, ceux des côtés restent (décor + bumpers) ;
//  - un banc de 3 cibles tombantes est posé au premier endroit libre au centre du haut, tourné vers
//    les flippers, avec un couloir dégagé devant pour pouvoir les viser.
// Menu "Flipper > Haut du plateau" (aussi fait par Installer). On peut le relancer : il refait le banc.
public static partial class InstallateurFlipper
{
    const string NomBancCibles = "Cibles_Tombantes";
    const float LimiteBasHaut = -10.5f;        // z : en dessous, on ne touche à rien (arbre (5) à -11.2)
    const float DemiLargeurCentre = 3.4f;      // arbres plus près que ça de l'axe des flippers = au centre
    const float LargeurCible = 0.5f, HauteurCible = 0.45f, EpaisseurCible = 0.1f, EcartCibles = 0.62f;

    [MenuItem("Flipper/Haut du plateau", priority = 8)]
    public static void MenuHautPlateau()
    {
        var rapport = new StringBuilder("Flipper > Haut du plateau :\n");
        var aVerifier = new StringBuilder();
        Undo.SetCurrentGroupName("Haut du plateau");
        int groupe = Undo.GetCurrentGroup();
        ConstruireHautPlateau(rapport, aVerifier);
        Undo.CollapseUndoOperations(groupe);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        if (aVerifier.Length > 0) rapport.Append("\nAttention :\n").Append(aVerifier);
        Debug.Log(rapport.ToString());
    }

    static void ConstruireHautPlateau(StringBuilder rapport, StringBuilder aVerifier)
    {
        Transform fg = TrouverParNom("flipper_gauche")?.transform, fd = TrouverParNom("flipper_droite")?.transform;
        if (fg == null || fd == null)
        {
            aVerifier.AppendLine("- Flippers introuvables : haut du plateau non modifié.");
            return;
        }
        Vector3 milieuFlippers = (fg.position + fd.position) * 0.5f;
        float axe = milieuFlippers.x;

        // 1. Arbres du centre du haut retirés
        var retires = new List<string>();
        foreach (GameObject arbre in SceneManager.GetActiveScene().GetRootGameObjects()
                     .Where(g => g.GetComponent<ArbreEnFeu>() != null || g.name.StartsWith("ArbreEnFeu")).ToList())
        {
            Vector3 p = arbre.transform.position;
            if (p.z <= LimiteBasHaut || Mathf.Abs(p.x - axe) >= DemiLargeurCentre) continue;
            retires.Add(arbre.name);
            Undo.DestroyObjectImmediate(arbre);
        }
        if (retires.Count > 0)
            rapport.AppendLine($"- Arbres du centre retirés : {string.Join(", ", retires)} (ceux des côtés restent).");

        // 2. Ancien banc retiré (il est refait)
        GameObject ancien = TrouverParNom(NomBancCibles);
        if (ancien != null) Undo.DestroyObjectImmediate(ancien);
        Physics.SyncTransforms();

        // 3. Place libre : d'abord au centre entre les bumpers du haut, puis on s'écarte
        float[] zs = { -5.6f, -5.2f, -6.0f, -4.8f, -6.4f, -4.4f, -6.8f, -7.2f, -7.6f, -8.0f };
        float[] dxs = { -0.9f, -0.45f, -1.35f, 0f, -1.75f, 0.45f };
        foreach (float z in zs)
            foreach (float dx in dxs)
            {
                if (!SurfacePlateau(axe + dx, z, out Vector3 point, out Vector3 normale)) continue;
                Vector3 face = Vector3.ProjectOnPlane(milieuFlippers - point, normale).normalized;   // les cibles regardent les flippers
                Quaternion rot = Quaternion.LookRotation(face, normale);
                if (!PlaceBancLibre(point, rot, normale, out string obstacle)) continue;
                PoserBanc(point, rot, normale, rapport);
                return;
            }
        aVerifier.AppendLine("- Pas de place libre trouvée au centre du haut pour les cibles tombantes.");
    }

    // L'emprise du banc est libre, et le couloir devant (vers les flippers) aussi, pour pouvoir viser
    static bool PlaceBancLibre(Vector3 point, Quaternion rot, Vector3 normale, out string obstacle)
    {
        obstacle = null;
        float demi = EcartCibles + LargeurCible * 0.5f + 0.25f;
        // Emprise (avec les triggers : pas de cible posée sur une pastille)
        for (float u = -demi; u <= demi + 0.01f; u += 0.2f)
            for (float v = -0.35f; v <= 0.35f + 0.01f; v += 0.175f)
                if (Obstacle(point + rot * new Vector3(u, 0f, v), normale, true, out obstacle)) return false;
        // Couloir devant, sur 3.5 de long et la largeur du banc (les triggers ne gênent pas la bille)
        for (float u = -demi + 0.2f; u <= demi - 0.2f + 0.01f; u += 0.3f)
            for (float v = 0.5f; v <= 3.5f; v += 0.3f)
                if (Obstacle(point + rot * new Vector3(u, 0f, v), normale, false, out obstacle)) return false;
        return true;
    }

    // Un objet entre le plateau et 0.6 au-dessus (au-dessus, la bille passe dessous)
    static bool Obstacle(Vector3 p, Vector3 normale, bool avecTriggers, out string nom)
    {
        nom = null;
        if (!SurfacePlateau(p.x, p.z, out Vector3 sol, out Vector3 n)) { nom = "bord du plateau"; return true; }
        var mode = avecTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
        foreach (RaycastHit h in Physics.RaycastAll(sol + n * 3f, -n, 3.05f, ~0, mode))
        {
            if (EstSurfaceOuVitre(h.collider)) continue;
            if (Vector3.Dot(h.point - sol, n) > 0.6f) continue;
            nom = h.collider.name;
            return true;
        }
        return false;
    }

    static void PoserBanc(Vector3 point, Quaternion rot, Vector3 normale, StringBuilder rapport)
    {
        Material corps = MateriauCouleur($"{DossierMateriaux}/Cibles/Cible_Corps.mat", new Color(0.32f, 0.05f, 0.03f), Color.black, 0.75f);
        Material face = MateriauCouleur($"{DossierMateriaux}/Cibles/Cible_Face.mat", new Color(0.6f, 0.22f, 0.06f), new Color(1f, 0.478f, 0.118f), 0.8f);
        Material metal = AssetDatabase.LoadAssetAtPath<Material>($"{DossierMateriaux}/Metal_Brosse.mat") ?? corps;
        PhysicsMaterial physique = Materiau("Plateau", 0.1f, 0.3f, PhysicsMaterialCombine.Average);
        AudioClip sonChute = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/son/Flipper_Clac.wav");
        AudioClip sonBonus = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/son/Bonus_Jingle.wav");

        var racine = new GameObject(NomBancCibles);
        Undo.RegisterCreatedObjectUndo(racine, NomBancCibles);
        racine.transform.SetPositionAndRotation(point, rot);

        // Fente métallique dans le plateau (décor, sans collision)
        Cube(racine.transform, "Socle", new Vector3(0f, 0.004f, 0f), new Vector3(EcartCibles * 2f + LargeurCible + 0.2f, 0.008f, 0.24f), metal);

        var cibles = new List<CibleTombante>();
        for (int i = 0; i < 3; i++)
        {
            var c = new GameObject($"Cible_{i + 1}");
            c.transform.SetParent(racine.transform, false);
            c.transform.localPosition = new Vector3((i - 1) * EcartCibles, 0f, 0f);
            var box = c.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, HauteurCible * 0.5f, 0f);
            box.size = new Vector3(LargeurCible, HauteurCible, EpaisseurCible);
            box.sharedMaterial = physique;
            var rb = c.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            Cube(c.transform, "Corps", new Vector3(0f, HauteurCible * 0.5f, 0f), new Vector3(LargeurCible, HauteurCible, EpaisseurCible), corps);
            // Face avant (côté flippers = +Z local), lumineuse quand la cible est debout
            Renderer rFace = Cube(c.transform, "Face", new Vector3(0f, HauteurCible * 0.55f, EpaisseurCible * 0.5f + 0.004f),
                                  new Vector3(LargeurCible * 0.84f, HauteurCible * 0.7f, 0.008f), face);

            var cible = c.AddComponent<CibleTombante>();
            cible.face = rFace;
            cible.son = sonChute;
            cible.profondeur = HauteurCible + 0.05f;
            cibles.Add(cible);
        }

        var banc = racine.AddComponent<BancCibles>();
        banc.cibles = cibles.ToArray();
        banc.sonBonus = sonBonus;
        EditorUtility.SetDirty(banc);
        rapport.AppendLine($"- Banc de 3 cibles tombantes posé à ({point.x:F2}, {point.z:F2}), tourné vers les flippers : " +
                           "250 pts par cible, 2500 quand les 3 sont tombées, puis elles remontent.");
    }

    static Renderer Cube(Transform parent, string nom, Vector3 position, Vector3 taille, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = nom;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = taille;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        return r;
    }
}
