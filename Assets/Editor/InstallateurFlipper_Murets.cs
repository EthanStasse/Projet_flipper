using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Murets : les vieux cubes (bordure droite + rabatoires) sont remplacés à l'écran par les modèles 3D
// de Assets/flipper parts/Murets/<nom>_Modele.fbx.
//  - Les cubes restent en place mais deviennent invisibles : ils gardent les collisions et les points
//    (ScoreOnHit), donc le jeu se comporte exactement pareil.
//  - Chaque modèle est posé à la position et à la rotation de son cube (les modèles ont déjà la bonne taille).
//  - Rangement dans la scène :  Murets / Collisions (les cubes)  et  Murets / Modeles (les modèles).
// Menu "Flipper > Habiller les murets" (aussi fait par Installer). On peut le relancer : il replace tout.
public static partial class InstallateurFlipper
{
    const string DossierMurets = "Assets/flipper parts/Murets";
    const string NomGroupeMurets = "Murets";

    // Nom du cube dans la scène -> nom du fichier FBX (sans "_Modele.fbx")
    static readonly (string cube, string modele)[] Murets =
    {
        ("bordure_droite (1)",    "bordure_droite"),
        ("rabatoire_top_droite",  "rabatoire_top_droite"),
        ("rabatoire_haut_droite", "rabatoire_haut_droite"),
        ("rabatoire_bas_droite",  "rabatoire_bas_droite"),
        // Les rabatoires gauches sont le miroir exact des droits (Flipper > Rabatoires gauches = droits) :
        // ils prennent les modèles des droits (symétriques sur leur longueur, la rotation suffit)
        ("rabatoire_top_gauche",  "rabatoire_top_droite"),
        ("rabatoire_haut_gauche", "rabatoire_haut_droite"),
        ("rabatoire_bas_gauche",  "rabatoire_bas_droite"),
        // Muret en biais contre le mur gauche (Flipper > Arbre contre le mur gauche)
        ("rabatoire_mur_gauche",  "rabatoire_haut_droite"),
    };

    // Les 3 rabatoires gauches deviennent le miroir des droits, par rapport à l'axe entre les deux flippers.
    [MenuItem("Flipper/Rabatoires gauches = droits (miroir)", priority = 7)]
    public static void MenuMiroirRabatoires()
    {
        var rapport = new StringBuilder("Flipper > Rabatoires gauches = droits :\n");
        Undo.SetCurrentGroupName("Rabatoires en miroir");
        int groupe = Undo.GetCurrentGroup();
        MiroirRabatoires(rapport);
        HabillerMurets(rapport, new StringBuilder());
        Undo.CollapseUndoOperations(groupe);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log(rapport.ToString());
    }

    static void MiroirRabatoires(StringBuilder rapport)
    {
        Transform fg = TrouverParNom("flipper_gauche")?.transform, fd = TrouverParNom("flipper_droite")?.transform;
        if (fg == null || fd == null)
        {
            rapport.AppendLine("- Flippers introuvables : axe de symétrie inconnu, rien changé.");
            return;
        }
        float axe = (fg.position.x + fd.position.x) * 0.5f;
        foreach (string partie in new[] { "top", "haut", "bas" })
        {
            Transform droite = TrouverParNom($"rabatoire_{partie}_droite")?.transform;
            Transform gauche = TrouverParNom($"rabatoire_{partie}_gauche")?.transform;
            if (droite == null || gauche == null) continue;
            Undo.RecordObject(gauche, "Miroir rabatoire");
            Vector3 p = droite.position;
            Quaternion q = droite.rotation;
            // Symétrie par rapport au plan vertical x = axe : x -> 2·axe - x, rotation (x, -y, -z, w)
            gauche.SetPositionAndRotation(new Vector3(2f * axe - p.x, p.y, p.z), new Quaternion(q.x, -q.y, -q.z, q.w));
            gauche.localScale = droite.localScale;   // les groupes parents sont sans rotation ni échelle
            rapport.AppendLine($"- {gauche.name} = miroir de {droite.name} (x = {gauche.position.x:F3}).");
        }
    }

    [MenuItem("Flipper/Habiller les murets", priority = 4)]
    public static void MenuHabillerMurets()
    {
        var rapport = new StringBuilder("Flipper > Habiller les murets :\n");
        var aVerifier = new StringBuilder();
        Undo.SetCurrentGroupName("Habiller les murets");
        int groupe = Undo.GetCurrentGroup();
        HabillerMurets(rapport, aVerifier);
        Undo.CollapseUndoOperations(groupe);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        if (aVerifier.Length > 0) rapport.Append("\nAttention :\n").Append(aVerifier);
        Debug.Log(rapport.ToString());
    }

    static void HabillerMurets(StringBuilder rapport, StringBuilder aVerifier)
    {
        Dictionary<string, Material> materiaux = MateriauxMurets();

        // Groupes de rangement (à l'origine, sans rotation ni échelle : rien ne bouge en y rangeant)
        Transform groupe = Groupe(NomGroupeMurets, null);
        Transform collisions = Groupe("Collisions", groupe);
        Transform modeles = Groupe("Modeles", groupe);

        int nb = 0;
        foreach (var (nomCube, nomModele) in Murets)
        {
            GameObject cube = TrouverParNom(nomCube);
            if (cube == null)
            {
                if (nomCube != NomMuretMurGauche)   // celui-là n'existe qu'après "Arbre contre le mur gauche"
                    aVerifier.AppendLine($"- Cube \"{nomCube}\" introuvable dans la scène.");
                continue;
            }
            string chemin = $"{DossierMurets}/{nomModele}_Modele.fbx";
            RemplacerMateriauxFbx(chemin, materiaux);   // avant de charger : le réimport remplace l'asset
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(chemin);
            if (fbx == null)
            {
                aVerifier.AppendLine($"- Modèle {chemin} introuvable : \"{nomCube}\" reste un cube visible.");
                continue;
            }

            // Le cube : rangé, invisible, mais il garde son collider et ses scripts
            if (cube.transform.parent != collisions)
                Undo.SetTransformParent(cube.transform, collisions, "Ranger muret");
            var rendu = cube.GetComponent<Renderer>();
            if (rendu != null && rendu.enabled)
            {
                Undo.RecordObject(rendu, "Cacher muret");
                rendu.enabled = false;
            }

            // Le modèle : "Modele_..." (le nom ne commence pas par bordure/rabatoire, pour que
            // l'installeur ne lui ajoute ni collider, ni points, ni texture)
            string nomInstance = $"Modele_{nomCube.Replace(" (1)", "")}";   // d'après le cube (gauche et droite peuvent partager un FBX)
            Transform existant = modeles.Find(nomInstance);
            // Modèle changé (ex. rabatoire gauche qui prend le modèle du droit) : on refait l'instance
            if (existant != null && PrefabUtility.GetCorrespondingObjectFromSource(existant.gameObject) != fbx)
            {
                Undo.DestroyObjectImmediate(existant.gameObject);
                existant = null;
            }
            GameObject modele;
            if (existant != null) modele = existant.gameObject;
            else
            {
                modele = (GameObject)PrefabUtility.InstantiatePrefab(fbx, modeles);
                modele.name = nomInstance;
                Undo.RegisterCreatedObjectUndo(modele, nomInstance);
            }
            Undo.RecordObject(modele.transform, "Placer muret");
            modele.transform.SetPositionAndRotation(cube.transform.position, cube.transform.rotation);
            // Le FBX est à la taille du cube d'origine ; si le cube a été rallongé (ex. muret du bas gauche
            // prolongé jusqu'au mur), le modèle est étiré sur sa longueur seulement
            Vector3 echelle = Vector3.one;
            Mesh mesh = modele.GetComponentInChildren<MeshFilter>()?.sharedMesh;
            if (mesh != null)
            {
                Vector3 c = cube.transform.lossyScale, m = mesh.bounds.size;
                if (c.x >= c.z && m.x > 0.01f) echelle.x = c.x / m.x;
                else if (c.z > c.x && m.z > 0.01f) echelle.z = c.z / m.z;
            }
            modele.transform.localScale = echelle;
            nb++;
        }
        rapport.AppendLine($"- {nb} muret(s) habillé(s) avec leur modèle 3D (cubes cachés, collisions et points conservés).");
    }

    static Transform Groupe(string nom, Transform parent)
    {
        Transform t = parent != null ? parent.Find(nom) : TrouverParNom(nom)?.transform;
        if (t != null) return t;
        var go = new GameObject(nom);
        Undo.RegisterCreatedObjectUndo(go, nom);
        if (parent != null) go.transform.SetParent(parent, false);
        return go.transform;
    }

    // Les FBX n'ont que des couleurs simples : on branche nos matériaux URP à la place (par nom de matériau)
    static void RemplacerMateriauxFbx(string chemin, Dictionary<string, Material> materiaux)
    {
        if (!(AssetImporter.GetAtPath(chemin) is ModelImporter imp)) return;
        bool change = false;
        foreach (var paire in materiaux)
        {
            var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), paire.Key);
            if (imp.GetExternalObjectMap().TryGetValue(id, out Object actuel) && actuel == paire.Value) continue;
            imp.AddRemap(id, paire.Value);
            change = true;
        }
        if (change) imp.SaveAndReimport();
    }

    static Dictionary<string, Material> MateriauxMurets()
    {
        string dossier = $"{DossierMateriaux}/Murets";
        Color braise = new Color(1f, 0.42f, 0.12f);

        // Bois carbonisé : mêmes textures que les murs de la table
        Texture2D boisA = Charger("BoisBrule_Albedo"), boisN = Charger("BoisBrule_Normal", true), boisE = Charger("BoisBrule_Emission");
        Material bois = boisA != null
            ? MateriauLit($"{dossier}/Muret_Bois.mat", boisA, boisN, boisE, braise * 1.6f, 0f, 0.25f, Vector2.one, Color.white)
            : MateriauCouleur($"{dossier}/Muret_Bois.mat", new Color(0.12f, 0.08f, 0.06f), Color.black, 0.25f);

        // Braises qui brillent
        Material braises = MateriauCouleur($"{dossier}/Muret_Braise.mat", new Color(0.35f, 0.1f, 0.03f), braise * 2.5f, 0.4f);

        // Plastique rouge transparent (comme la rampe)
        string cheminPlastique = $"{dossier}/Muret_Plastique.mat";
        Material plastique = MateriauCouleur(cheminPlastique, new Color(0.85f, 0.08f, 0.05f, 0.55f), Color.black, 0.9f);
        plastique.SetFloat("_Surface", 1f);   // transparent
        plastique.SetFloat("_Blend", 0f);     // alpha
        plastique.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        plastique.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        plastique.SetFloat("_ZWrite", 0f);
        plastique.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        plastique.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(plastique);

        return new Dictionary<string, Material>
        {
            { "Bois_Carbonise", bois },
            { "Braise", braises },
            { "Plastique_Rouge_Transparent", plastique },
        };
    }
}
