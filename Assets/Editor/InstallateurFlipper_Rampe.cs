using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Rehausses de la rampe : les parois de la collision de la rampe (Rampe_Collision) font ~0.48 de haut à droite,
// moins que la bille (0.58) : avec le boost, elle passait par-dessus dans les virages.
// On prend chaque bord du haut des parois et on le monte de 0.4 (collision invisible, sans frottement).
// Les bouts ouverts (sorties de la rampe, bord du sol sans paroi) ne sont pas touchés : la bille sort toujours.
// Menu "Flipper > Rehausser les parois de la rampe" (aussi fait par Installer). À refaire si la rampe bouge.
public static partial class InstallateurFlipper
{
    const string NomGardeRampe = "Rampe_Garde";
    const string CheminMeshGarde = "Assets/Modeles/Rampe_Garde.asset";
    const float HauteurRehausse = 0.4f;

    [MenuItem("Flipper/Rehausser les parois de la rampe", priority = 18)]
    public static void MenuGardeRampe()
    {
        var rapport = new StringBuilder("Flipper > Rehausser les parois de la rampe :\n");
        var aVerifier = new StringBuilder();
        Undo.SetCurrentGroupName("Rehausser la rampe");
        int groupe = Undo.GetCurrentGroup();
        ConstruireGardesRampe(rapport, aVerifier);
        Undo.CollapseUndoOperations(groupe);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        if (aVerifier.Length > 0) rapport.Append("\nAttention :\n").Append(aVerifier);
        Debug.Log(rapport.ToString());
    }

    static void ConstruireGardesRampe(StringBuilder rapport, StringBuilder aVerifier)
    {
        GameObject rampe = TrouverParNom("Rampe_Collision");
        Mesh source = rampe?.GetComponent<MeshFilter>()?.sharedMesh;
        if (source == null || !source.isReadable)
        {
            aVerifier.AppendLine("- Rampe_Collision introuvable (ou maillage non lisible) : parois non rehaussées.");
            return;
        }
        Transform t = rampe.transform;
        Transform racine = t.root;
        Vector3 haut = racine.up;   // la rampe est penchée comme la table : son "haut" est la normale du plateau

        // Sommets en coordonnées monde, soudés par position (le maillage duplique les sommets aux arêtes vives)
        Vector3[] verts = source.vertices;
        var monde = verts.Select(v => t.TransformPoint(v)).ToArray();
        var cle = monde.Select(p => new Vector3Int(Mathf.RoundToInt(p.x * 1000f), Mathf.RoundToInt(p.y * 1000f), Mathf.RoundToInt(p.z * 1000f))).ToArray();

        // Arêtes de bord = utilisées par un seul triangle
        var compte = new Dictionary<(Vector3Int, Vector3Int), int>();
        var position = new Dictionary<Vector3Int, Vector3>();
        int[] tris = source.triangles;
        for (int i = 0; i < tris.Length; i += 3)
            for (int k = 0; k < 3; k++)
            {
                int a = tris[i + k], b = tris[i + (k + 1) % 3];
                var ka = cle[a]; var kb = cle[b];
                position[ka] = monde[a];
                position[kb] = monde[b];
                var arete = Compare(ka, kb) < 0 ? (ka, kb) : (kb, ka);
                compte[arete] = compte.TryGetValue(arete, out int n) ? n + 1 : 1;
            }

        // Sol de la rampe sous un point = le sommet le plus bas à moins de 0.6 (dans le plan de la table)
        Vector3 axeU = Vector3.Cross(haut, Vector3.forward).normalized, axeV = Vector3.Cross(axeU, haut);
        var grille = new Dictionary<Vector2Int, List<Vector3>>();
        foreach (Vector3 p in position.Values)
        {
            var c = new Vector2Int(Mathf.FloorToInt(Vector3.Dot(p, axeU) / 0.5f), Mathf.FloorToInt(Vector3.Dot(p, axeV) / 0.5f));
            if (!grille.TryGetValue(c, out var l)) grille[c] = l = new List<Vector3>();
            l.Add(p);
        }
        float Sol(Vector3 p)
        {
            float u = Vector3.Dot(p, axeU), v = Vector3.Dot(p, axeV), best = float.MaxValue;
            int cu = Mathf.FloorToInt(u / 0.5f), cv = Mathf.FloorToInt(v / 0.5f);
            for (int du = -2; du <= 2; du++)
                for (int dv = -2; dv <= 2; dv++)
                    if (grille.TryGetValue(new Vector2Int(cu + du, cv + dv), out var l))
                        foreach (Vector3 q in l)
                        {
                            float eu = Vector3.Dot(q, axeU) - u, ev = Vector3.Dot(q, axeV) - v;
                            if (eu * eu + ev * ev < 0.36f) best = Mathf.Min(best, Vector3.Dot(q, haut));
                        }
            return best;
        }

        var sommets = new List<Vector3>();
        var triangles = new List<int>();
        int gardees = 0;
        foreach (var paire in compte)
        {
            if (paire.Value != 1) continue;
            Vector3 a = position[paire.Key.Item1], b = position[paire.Key.Item2];
            Vector3 m = (a + b) * 0.5f;
            if (Vector3.Dot(m - racine.position, haut) < 0.15f) continue;     // bouche au niveau du plateau
            if (Vector3.Dot(m, haut) - Sol(m) < 0.15f) continue;              // bord du sol sans paroi (sortie) : on laisse ouvert
            int i0 = sommets.Count;
            sommets.Add(a); sommets.Add(b); sommets.Add(b + haut * HauteurRehausse); sommets.Add(a + haut * HauteurRehausse);
            // Les deux faces (la bille peut arriver des deux côtés)
            triangles.AddRange(new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3, i0, i0 + 2, i0 + 1, i0, i0 + 3, i0 + 2 });
            gardees++;
        }
        if (gardees == 0)
        {
            aVerifier.AppendLine("- Aucun haut de paroi trouvé sur la rampe.");
            return;
        }

        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(CheminMeshGarde);
        bool nouveau = mesh == null;
        if (nouveau) mesh = new Mesh { name = NomGardeRampe };
        mesh.Clear();
        mesh.indexFormat = sommets.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(sommets);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        if (nouveau)
        {
            CreerDossier("Assets/Modeles");
            AssetDatabase.CreateAsset(mesh, CheminMeshGarde);
        }
        else EditorUtility.SetDirty(mesh);

        GameObject garde = TrouverParNom(NomGardeRampe);
        if (garde == null)
        {
            garde = new GameObject(NomGardeRampe);
            Undo.RegisterCreatedObjectUndo(garde, NomGardeRampe);
        }
        Undo.RecordObject(garde.transform, "Rehausse");
        garde.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);   // maillage en coordonnées monde
        garde.transform.localScale = Vector3.one;
        var mc = ObtenirOuAjouter<MeshCollider>(garde);
        mc.sharedMesh = null;
        mc.sharedMesh = mesh;
        mc.convex = false;
        mc.sharedMaterial = MateriauRail();
        rapport.AppendLine($"- {gardees} bords du haut des parois de la rampe rehaussés de {HauteurRehausse} (collision invisible, sans frottement). " +
                           "Les sorties restent ouvertes.");
    }

    // ==================================================================== SCORE SUR LE BACKGLASS

    const string NomScoreBackglass = "Score_Backglass";

    [MenuItem("Flipper/Score sur le backglass", priority = 19)]
    public static void MenuScoreBackglass()
    {
        var rapport = new StringBuilder("Flipper > Score sur le backglass :\n");
        var aVerifier = new StringBuilder();
        Undo.SetCurrentGroupName("Score sur le backglass");
        int groupe = Undo.GetCurrentGroup();
        PoserScoreBackglass(rapport, aVerifier);
        Undo.CollapseUndoOperations(groupe);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        if (aVerifier.Length > 0) rapport.Append("\nAttention :\n").Append(aVerifier);
        Debug.Log(rapport.ToString());
    }

    static void PoserScoreBackglass(StringBuilder rapport, StringBuilder aVerifier)
    {
        Renderer vitre = TrouverParNom("Backglass")?.GetComponent<Renderer>();
        GameObject plateau = TrouverParNom("Playfield");
        if (vitre == null || plateau == null)
        {
            aVerifier.AppendLine("- Backglass ou Playfield introuvable : score non posé.");
            return;
        }
        // Le backglass est un panneau : sa normale est l'axe le plus fin de sa boîte ; on garde le côté qui regarde la table
        Bounds b = vitre.bounds;
        Vector3 taille = b.size;
        Vector3 normale = taille.x < taille.y && taille.x < taille.z ? Vector3.right
                        : taille.y < taille.z ? Vector3.up : Vector3.forward;
        Vector3 versTable = plateau.GetComponent<Renderer>().bounds.center - b.center;
        if (Vector3.Dot(normale, versTable) < 0f) normale = -normale;
        Vector3 hautPanneau = Vector3.ProjectOnPlane(Vector3.up, normale).sqrMagnitude > 0.01f
            ? Vector3.ProjectOnPlane(Vector3.up, normale).normalized : Vector3.forward;
        float largeur = Vector3.ProjectOnPlane(taille, normale).magnitude;   // approx. si penché
        Vector3 droitePanneau = Vector3.Cross(hautPanneau, normale).normalized;
        float l = Mathf.Abs(Vector3.Dot(taille, droitePanneau)), h = Mathf.Abs(Vector3.Dot(taille, hautPanneau));
        float epaisseur = Mathf.Abs(Vector3.Dot(taille, normale));

        GameObject racine = TrouverParNom(NomScoreBackglass);
        if (racine != null) Undo.DestroyObjectImmediate(racine);
        racine = new GameObject(NomScoreBackglass);
        Undo.RegisterCreatedObjectUndo(racine, NomScoreBackglass);
        // Texte TMP : lisible quand on le regarde depuis son -Z → -Z vers la table (vers le joueur)
        racine.transform.SetPositionAndRotation(b.center + normale * (epaisseur * 0.5f + 0.03f), Quaternion.LookRotation(-normale, hautPanneau));

        var police = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(CheminPoliceTmp);
        Material neon = police != null ? MateriauNeon(police) : null;
        TMPro.TextMeshPro Ligne(string nom, float y, float hauteur, Color couleur)
        {
            var go = new GameObject(nom);
            go.transform.SetParent(racine.transform, false);
            go.transform.localPosition = new Vector3(0f, y * h, 0f);
            var t = go.AddComponent<TMPro.TextMeshPro>();
            if (police != null) t.font = police;
            if (neon != null) t.fontSharedMaterial = neon;
            t.rectTransform.sizeDelta = new Vector2(l * 0.86f, h * hauteur);
            t.enableAutoSizing = true;
            t.fontSizeMin = 0.1f;
            t.fontSizeMax = 200f;
            t.fontStyle = TMPro.FontStyles.Bold;
            t.alignment = TMPro.TextAlignmentOptions.Center;
            t.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            t.color = couleur;
            t.text = nom;
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return t;
        }
        var sb = racine.AddComponent<ScoreBackglass>();
        sb.haut = Ligne("Haut", 0.3f, 0.18f, new Color(1f, 0.55f, 0.2f));
        sb.score = Ligne("Score", 0f, 0.36f, Color.white);
        sb.bas = Ligne("Bas", -0.3f, 0.16f, new Color(1f, 0.8f, 0.4f));
        EditorUtility.SetDirty(sb);
        rapport.AppendLine($"- Score posé sur le backglass (panneau de {l:F2} x {h:F2}, face tournée vers la table).");
    }

    // ==================================================================== CAMÉRAS

    // Vue "dessus" = la vue actuelle de la caméra (gardée telle quelle).
    // Vue "joueur" = devant la machine, dans l'axe de la table, le plus près possible tout en voyant
    // le bord avant du plateau (flippers) en bas de l'écran et le haut du score du backglass en haut.
    [MenuItem("Flipper/Caméras (joueur et dessus)", priority = 20)]
    public static void MenuCameras()
    {
        var rapport = new StringBuilder("Flipper > Caméras :\n");
        var aVerifier = new StringBuilder();
        Undo.SetCurrentGroupName("Caméras");
        int groupe = Undo.GetCurrentGroup();
        ConfigurerCameras(rapport, aVerifier);
        Undo.CollapseUndoOperations(groupe);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        if (aVerifier.Length > 0) rapport.Append("\nAttention :\n").Append(aVerifier);
        Debug.Log(rapport.ToString());
    }

    static void ConfigurerCameras(StringBuilder rapport, StringBuilder aVerifier)
    {
        Camera cam = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
        Renderer plateau = TrouverParNom("Playfield")?.GetComponent<Renderer>();
        if (cam == null || plateau == null)
        {
            aVerifier.AppendLine("- Caméra ou Playfield introuvable.");
            return;
        }
        var cf = cam.GetComponent<CameraFlipper>();
        bool nouveau = cf == null;
        if (nouveau) cf = Undo.AddComponent<CameraFlipper>(cam.gameObject);
        else Undo.RecordObject(cf, "Caméras");
        if (nouveau)   // la vue actuelle devient la vue du dessus
            cf.vueDessus = new CameraFlipper.Vue { position = cam.transform.position, rotation = cam.transform.rotation, champ = cam.fieldOfView };

        Bounds pf = plateau.bounds;
        Vector3 avant = new Vector3(pf.center.x, pf.min.y, pf.min.z);   // bord avant du plateau (le plus bas, côté flippers)
        // Point le plus haut à voir : le haut du score sur le backglass (sinon le haut de la table)
        Vector3 hautVu = new Vector3(pf.center.x, pf.max.y, pf.max.z);
        var scoreHaut = TrouverParNom("Score_Backglass")?.GetComponentsInChildren<Renderer>();
        if (scoreHaut != null && scoreHaut.Length > 0)
            hautVu = new Vector3(pf.center.x, scoreHaut.Max(r => r.bounds.max.y) + 0.5f, scoreHaut.Max(r => r.bounds.center.z));

        const float champ = 60f;
        Vector3 position = Vector3.zero;
        float pitch = 0f;
        for (float d = 6f; d <= 50f; d += 0.25f)
        {
            Vector3 c = new Vector3(pf.center.x, avant.y + 1.15f * d, avant.z - d);
            float aBas = Mathf.Atan2(avant.y - c.y, avant.z - c.z) * Mathf.Rad2Deg;
            float aHaut = Mathf.Atan2(hautVu.y - c.y, hautVu.z - c.z) * Mathf.Rad2Deg;
            position = c;
            pitch = (aBas + aHaut) * 0.5f;
            if (aHaut - aBas <= champ * 0.9f) break;
        }
        cf.vueJoueur = new CameraFlipper.Vue { position = position, rotation = Quaternion.Euler(-pitch, 0f, 0f), champ = champ };
        cf.commencerEnVueJoueur = true;
        EditorUtility.SetDirty(cf);
        rapport.AppendLine($"- Vue joueur : caméra en ({position.x:F1}, {position.y:F1}, {position.z:F1}), penchée de {-pitch:F0}° vers la table. " +
                           "Vue dessus = l'ancienne vue. Touche C pour changer en jeu.");
    }

    static int Compare(Vector3Int a, Vector3Int b) =>
        a.x != b.x ? a.x.CompareTo(b.x) : a.y != b.y ? a.y.CompareTo(b.y) : a.z.CompareTo(b.z);
}
