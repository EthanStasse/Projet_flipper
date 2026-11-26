using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Courbes du haut (modèle 3D Assets/flipper parts/Murets/Guide_Haut_Droite_Modele.fbx) :
//  - Guide_Haut_Droite : remplace l'ancien arc fait de 16 cubes (même centre, même rayon intérieur 2.5).
//  - Guide_Haut_Gauche : le même modèle tourné de 90°, tangent au mur gauche et au mur du fond.
// La collision est le maillage du modèle lui-même (sa face intérieure est un cercle parfait), sans frottement.
// Menu "Flipper > Courbes du haut" (aussi fait par Installer). On peut le relancer : il replace tout.
public static partial class InstallateurFlipper
{
    const string CheminModeleGuide = "Assets/flipper parts/Murets/Guide_Haut_Droite_Modele.fbx";
    const string NomGuideGauche = "Guide_Haut_Gauche";
    const string NomModeleGuide = "Modele";
    // Centre de l'arc droit mesuré par l'installeur (si l'ancien guide a déjà disparu)
    static readonly Vector3 CentreGuideDroitParDefaut = new Vector3(-23.253f, 19.987f, -0.266f);

    [MenuItem("Flipper/Courbes du haut", priority = 10)]
    public static void MenuCourbesHaut()
    {
        var rapport = new StringBuilder("Flipper > Courbes du haut :\n");
        var aVerifier = new StringBuilder();
        Undo.SetCurrentGroupName("Courbes du haut");
        int groupe = Undo.GetCurrentGroup();
        PoserCourbesHaut(rapport, aVerifier);
        Undo.CollapseUndoOperations(groupe);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        if (aVerifier.Length > 0) rapport.Append("\nAttention :\n").Append(aVerifier);
        Debug.Log(rapport.ToString());
    }

    static bool EstGuideModele(GameObject guide) => guide != null && guide.transform.Find(NomModeleGuide) != null;

    static void PoserCourbesHaut(StringBuilder rapport, StringBuilder aVerifier)
    {
        var mats = MateriauxMurets();
        Material metal = AssetDatabase.LoadAssetAtPath<Material>($"{DossierMateriaux}/Metal_Brosse.mat");
        if (metal != null) mats["Metal"] = metal;
        RemplacerMateriauxFbx(CheminModeleGuide, mats);   // avant de charger : le réimport remplace l'asset
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CheminModeleGuide);
        if (fbx == null)
        {
            aVerifier.AppendLine($"- Modèle {CheminModeleGuide} introuvable : courbes du haut non changées.");
            return;
        }

        // Centre et orientation de l'arc droit : ceux de l'ancien guide (16 cubes) ou du guide déjà posé
        GameObject ancien = TrouverParNom(NomGuideHaut);
        Vector3 centre = ancien != null ? ancien.transform.position : CentreGuideDroitParDefaut;
        if (!SurfacePlateau(centre.x, centre.z, out Vector3 sol, out Vector3 normale))
        {
            aVerifier.AppendLine("- Plateau introuvable sous le centre de la courbe droite.");
            return;
        }
        Vector3 hautTable = Vector3.ProjectOnPlane(Vector3.forward, normale).normalized;
        Vector3 droite = Vector3.Cross(normale, hautTable);
        Quaternion rot = Quaternion.LookRotation(hautTable, normale);
        // Le centre de l'ancien arc est sur la surface de jeu : on le garde tel quel (même plan)
        if (ancien != null) centre = ancien.transform.position;
        else centre = sol;

        // Ancien guide (cubes ou modèle) retiré, ancien guide gauche aussi : on refait les deux
        if (ancien != null) Undo.DestroyObjectImmediate(ancien);
        GameObject ancienGauche = TrouverParNom(NomGuideGauche);
        if (ancienGauche != null) Undo.DestroyObjectImmediate(ancienGauche);
        Physics.SyncTransforms();

        // Mur gauche : on le cherche à hauteur de bille, un peu sous le début de l'arc, vers la gauche
        Vector3 depart = centre - droite * 2f - hautTable * 1f + normale * 0.3f;
        var hits = Physics.RaycastAll(depart, -droite, 30f, ~0, QueryTriggerInteraction.Ignore)
                          .Where(h => !EstSurfaceOuVitre(h.collider)).OrderBy(h => h.distance).ToList();
        // Le mur, c'est la caisse (PinballTable) : on ignore un arbre ou un bumper qui serait en chemin
        Transform caisse = TrouverParNom("PinballTable")?.transform;
        RaycastHit? murGauche = null;
        foreach (RaycastHit h in hits)
            if (caisse == null || h.collider.transform.IsChildOf(caisse)) { murGauche = h; break; }
        if (murGauche == null)
        {
            aVerifier.AppendLine("- Mur gauche introuvable : courbe gauche non posée.");
        }

        GameObject droit = PoserGuide(NomGuideHaut, fbx, centre, rot, rapport);
        rapport.AppendLine($"- {NomGuideHaut} : nouveau modèle posé à la place de l'ancien arc (centre {centre.x:F2}, {centre.z:F2}).");

        if (murGauche != null)
        {
            float d = murGauche.Value.distance;
            Vector3 centreGauche = centre - droite * (d - 0.5f);   // tangent au mur gauche (rayon 2.5), même hauteur sur la table
            // Tourné de -90° autour de la normale : l'arc va du mur du fond (+Z) au mur gauche (-X)
            Quaternion rotGauche = rot * Quaternion.Euler(0f, -90f, 0f);
            PoserGuide(NomGuideGauche, fbx, centreGauche, rotGauche, rapport);
            rapport.AppendLine($"- {NomGuideGauche} : posé contre \"{murGauche.Value.collider.name}\" (centre {centreGauche.x:F2}, {centreGauche.z:F2}).");
        }

        // Ce que les courbes touchent (hors plateau, vitre, murs de la caisse et elles-mêmes)
        Physics.SyncTransforms();
        foreach (string nom in new[] { NomGuideHaut, NomGuideGauche })
        {
            GameObject g = TrouverParNom(nom);
            if (g == null) continue;
            Transform table = TrouverParNom("PinballTable")?.transform;
            var genes = new HashSet<string>();
            for (int i = 0; i <= 12; i++)
            {
                float a = i / 12f * 90f * Mathf.Deg2Rad;
                Vector3 local = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 2.3f + Vector3.up * 0.35f;   // juste devant la face intérieure
                foreach (Collider c in Physics.OverlapSphere(g.transform.TransformPoint(local), 0.15f, ~0, QueryTriggerInteraction.Ignore))
                    if (!EstSurfaceOuVitre(c) && !c.transform.IsChildOf(g.transform) && (table == null || !c.transform.IsChildOf(table)) && c.name != NomMurFond)
                        genes.Add(c.name);
            }
            if (genes.Count > 0) aVerifier.AppendLine($"- Devant {nom} (là où roule la bille) : {string.Join(", ", genes)}.");
        }
    }

    static GameObject PoserGuide(string nom, GameObject fbx, Vector3 centre, Quaternion rot, StringBuilder rapport)
    {
        var parent = new GameObject(nom);
        Undo.RegisterCreatedObjectUndo(parent, nom);
        parent.transform.SetPositionAndRotation(centre, rot);

        var modele = (GameObject)PrefabUtility.InstantiatePrefab(fbx, parent.transform);
        modele.name = NomModeleGuide;
        modele.transform.localPosition = Vector3.zero;
        modele.transform.localRotation = Quaternion.identity;
        modele.transform.localScale = Vector3.one;

        // Collision = le maillage du modèle (face intérieure en cercle parfait de rayon 2.5), sans frottement
        PhysicsMaterial rail = MateriauRail();
        foreach (MeshFilter mf in modele.GetComponentsInChildren<MeshFilter>(true))
        {
            var mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = false;
            mc.sharedMaterial = rail;
        }
        return parent;
    }
}
