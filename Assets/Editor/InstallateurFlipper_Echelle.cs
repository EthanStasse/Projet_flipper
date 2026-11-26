using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Échelle d'étoiles (score 100 → 1000) au centre bas du plateau, entre les lance-pierres.
// Menu "Flipper > Échelle d'étoiles" (aussi fait par Installer). On peut le relancer : il replace tout.
//  - Modèle : étoile à 5 branches plate (Assets/Modeles/Etoile_Insert.asset), dessus plat, bord biseauté
//    en métal, 0.03 d'épaisseur, sans collider : la bille roule dessus.
//  - Chaque étoile : une Point Light orange dessous (éteinte au départ), sa valeur en TextMeshPro à plat,
//    et le script EtoileEchelle. Le tout sous "Echelle_Etoiles", qui porte EchelleScore.
//  - Les pastilles "Bonus_Centre" sont remontées au premier endroit libre au-dessus de l'échelle.
public static partial class InstallateurFlipper
{
    const string NomEchelle = "Echelle_Etoiles";
    const string CheminMeshEtoile = "Assets/Modeles/Etoile_Insert.asset";
    const string CheminPoliceTmp = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    static readonly Color CouleurBraiseEchelle = new Color(1f, 0.478f, 0.118f);   // #FF7A1E

    // Centre bas de l'échelle (sur l'axe entre les flippers, juste au-dessus de l'ampoule "Rejouer")
    const float EchelleX = -27.97f, EchelleZ = -16.75f;
    const float HauteurEchelle = 4.5f, LargeurEchelle = 2.2f;

    // Étoiles dans le repère de l'échelle : x vers la droite, z vers le haut de la table
    struct PlaceEtoile { public int valeur; public float x, z, taille, texteX; }
    static readonly PlaceEtoile[] PlacesEtoiles =
    {
        new PlaceEtoile { valeur = 100,  x = -0.45f, z = 0.35f, taille = 0.35f, texteX = -0.97f },
        new PlaceEtoile { valeur = 200,  x =  0.45f, z = 1.25f, taille = 0.35f, texteX =  0.97f },
        new PlaceEtoile { valeur = 300,  x = -0.45f, z = 2.15f, taille = 0.35f, texteX = -0.97f },
        new PlaceEtoile { valeur = 500,  x =  0.45f, z = 3.05f, taille = 0.35f, texteX =  0.97f },
        new PlaceEtoile { valeur = 1000, x =  0f,    z = 4.05f, taille = 0.55f, texteX = -0.80f },
    };

    [MenuItem("Flipper/Échelle d'étoiles", priority = 6)]
    public static void MenuEchelleEtoiles()
    {
        var rapport = new StringBuilder("Flipper > Échelle d'étoiles :\n");
        var aVerifier = new StringBuilder();
        Undo.SetCurrentGroupName("Échelle d'étoiles");
        int groupe = Undo.GetCurrentGroup();
        ConstruireEchelle(rapport, aVerifier);
        Undo.CollapseUndoOperations(groupe);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        if (aVerifier.Length > 0) rapport.Append("\nAttention :\n").Append(aVerifier);
        Debug.Log(rapport.ToString());
    }

    static void ConstruireEchelle(StringBuilder rapport, StringBuilder aVerifier)
    {
        Physics.SyncTransforms();
        if (!SurfacePlateau(EchelleX, EchelleZ, out Vector3 origine, out Vector3 normale))
        {
            aVerifier.AppendLine("- Plateau introuvable au centre bas : échelle non posée.");
            return;
        }
        Vector3 hautTable = Vector3.ProjectOnPlane(Vector3.forward, normale).normalized;
        Quaternion rotation = Quaternion.LookRotation(hautTable, normale);

        // 1. Faire de la place : remonter les pastilles du centre au-dessus de l'échelle
        DeplacerBonusCentre(origine + hautTable * HauteurEchelle, hautTable, rapport, aVerifier);
        Physics.SyncTransforms();

        // 2. Ce qui gêne dans la zone de l'échelle (au-dessus du plateau, hors vitre)
        var genants = new HashSet<string>();
        for (float u = -LargeurEchelle / 2f; u <= LargeurEchelle / 2f + 0.01f; u += 0.275f)
            for (float v = 0f; v <= HauteurEchelle + 0.01f; v += 0.25f)
            {
                Vector3 p = origine + rotation * new Vector3(u, 0f, v);
                foreach (string n in ObjetsAuDessus(p)) genants.Add(n);
            }
        foreach (string n in genants)
            aVerifier.AppendLine($"- \"{n}\" est dans la zone de l'échelle (au-dessus du plateau) : à vérifier.");
        GameObject dejaPosee = TrouverParNom(NomEchelle);
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
        {
            if (l.type != LightType.Point) continue;
            if (dejaPosee != null && l.transform.IsChildOf(dejaPosee.transform)) continue;
            Vector3 local = Quaternion.Inverse(rotation) * (l.transform.position - origine);
            if (Mathf.Abs(local.x) < LargeurEchelle / 2f && local.z > -0.2f && local.z < HauteurEchelle + 0.2f)
                aVerifier.AppendLine($"- Lumière \"{l.transform.parent?.name}/{l.name}\" dans la zone de l'échelle.");
        }

        // 3. Modèle et matériaux
        Mesh mesh = MeshEtoileInsert();
        Material verre = MateriauCouleur($"{DossierMateriaux}/Echelle/Etoile_Verre.mat",
                                         new Color(0.35f, 0.12f, 0.04f), CouleurBraiseEchelle, 0.85f);
        Material metal = AssetDatabase.LoadAssetAtPath<Material>($"{DossierMateriaux}/Metal_Brosse.mat")
                         ?? MateriauChrome($"{DossierMateriaux}/Echelle/Etoile_Liseret.mat");
        TMP_FontAsset police = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CheminPoliceTmp) ?? TMP_Settings.defaultFontAsset;
        if (police == null)
            aVerifier.AppendLine("- Police TextMeshPro introuvable : importez Window > TextMeshPro > Import TMP Essential Resources, puis relancez.");

        // 4. L'échelle
        GameObject racine = TrouverParNom(NomEchelle);
        if (racine == null)
        {
            racine = new GameObject(NomEchelle);
            Undo.RegisterCreatedObjectUndo(racine, NomEchelle);
        }
        Undo.RecordObject(racine.transform, "Placer échelle");
        racine.transform.SetPositionAndRotation(origine + normale * 0.002f, rotation);
        racine.transform.localScale = Vector3.one;

        var etoiles = new List<EtoileEchelle>();
        foreach (PlaceEtoile p in PlacesEtoiles)
            etoiles.Add(ConstruireEtoile(racine.transform, p, mesh, verre, metal, police));

        EchelleScore echelle = ObtenirOuAjouter<EchelleScore>(racine);
        echelle.valeurs = PlacesEtoiles.Select(p => p.valeur).ToArray();
        echelle.etoiles = etoiles.ToArray();
        echelle.declencheur = EchelleScore.Declencheur.Rampe;
        if (echelle.sonEtoile == null) echelle.sonEtoile = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/son/Insert_Ding.wav");
        if (echelle.sonComplete == null) echelle.sonComplete = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/son/Bonus_Jingle.wav");
        EditorUtility.SetDirty(echelle);

        rapport.AppendLine($"- Échelle de {etoiles.Count} étoiles posée au centre bas ({string.Join(", ", echelle.valeurs)}), " +
                           "déclenchée par la rampe, bonus points x2 pendant 30 s.");
    }

    static EtoileEchelle ConstruireEtoile(Transform racine, PlaceEtoile p, Mesh mesh, Material verre, Material metal, TMP_FontAsset police)
    {
        string nom = $"Etoile_{p.valeur}";
        Transform t = racine.Find(nom);
        GameObject go;
        if (t != null) go = t.gameObject;
        else
        {
            go = new GameObject(nom);
            Undo.RegisterCreatedObjectUndo(go, nom);
            go.transform.SetParent(racine, false);
        }
        // Le mesh fait 1 de rayon : l'échelle en x/z donne la taille, y reste 1 (épaisseur 0.03)
        float rayon = p.taille / (2f * Mathf.Cos(18f * Mathf.Deg2Rad));   // largeur d'une étoile = 2 R cos 18°
        Undo.RecordObject(go.transform, "Placer étoile");
        go.transform.localPosition = new Vector3(p.x, 0f, p.z);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = new Vector3(rayon, 1f, rayon);

        ObtenirOuAjouter<MeshFilter>(go).sharedMesh = mesh;
        var rendu = ObtenirOuAjouter<MeshRenderer>(go);
        rendu.sharedMaterials = new[] { verre, metal };
        rendu.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Lumière sous l'étoile (éteinte : EtoileEchelle l'allume)
        Transform tl = go.transform.Find("Lumiere");
        if (tl == null)
        {
            tl = new GameObject("Lumiere").transform;
            Undo.RegisterCreatedObjectUndo(tl.gameObject, "Lumiere");
            tl.SetParent(go.transform, false);
        }
        Undo.RecordObject(tl, "Lumiere");
        tl.localPosition = new Vector3(0f, 0.15f, 0f);
        tl.localScale = new Vector3(1f / rayon, 1f, 1f / rayon);
        Light l = ObtenirOuAjouter<Light>(tl.gameObject);
        l.type = LightType.Point;
        l.color = CouleurBraiseEchelle;
        l.range = p.valeur >= 1000 ? 1.3f : 0.9f;
        l.intensity = 0f;
        l.enabled = false;
        l.shadows = LightShadows.None;

        // Valeur à plat sur le plateau, lisible depuis le bas de la table (enfant de l'échelle, pas de l'étoile :
        // il ne doit pas être étiré par l'échelle de l'étoile)
        string nomTexte = $"Texte_{p.valeur}";
        Transform tt = racine.Find(nomTexte);
        TextMeshPro texte;
        if (tt == null)
        {
            var gt = new GameObject(nomTexte);
            Undo.RegisterCreatedObjectUndo(gt, nomTexte);
            gt.transform.SetParent(racine, false);
            texte = gt.AddComponent<TextMeshPro>();
        }
        else texte = ObtenirOuAjouter<TextMeshPro>(tt.gameObject);
        Undo.RecordObject(texte.transform, "Texte");
        texte.transform.localPosition = new Vector3(p.texteX, 0.004f, p.z);
        texte.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // couché sur la table, haut du texte vers le haut de la table
        texte.transform.localScale = Vector3.one;
        if (police != null) texte.font = police;
        texte.text = p.valeur.ToString();
        texte.fontSize = p.valeur >= 1000 ? 3.2f : 2.4f;
        texte.fontStyle = FontStyles.Bold;
        texte.characterSpacing = -8f;          // serré : effet condensé
        texte.alignment = TextAlignmentOptions.Center;
        texte.textWrappingMode = TextWrappingModes.NoWrap;
        texte.overflowMode = TextOverflowModes.Overflow;
        texte.rectTransform.sizeDelta = new Vector2(0.9f, 0.4f);
        texte.color = new Color(CouleurBraiseEchelle.r, CouleurBraiseEchelle.g, CouleurBraiseEchelle.b, 0.35f);
        texte.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        EditorUtility.SetDirty(texte);

        EtoileEchelle e = ObtenirOuAjouter<EtoileEchelle>(go);
        e.couleur = CouleurBraiseEchelle;
        e.lumiere = l;
        e.texte = texte;
        e.intensiteLumiere = p.valeur >= 1000 ? 1.8f : 1.2f;
        EditorUtility.SetDirty(e);
        return e;
    }

    // ==================================================================== PASTILLES DU CENTRE

    // Remonte "Bonus_Centre" le long de la table pour que sa pastille la plus basse soit au-dessus de "limite",
    // au premier endroit où aucune pastille n'a d'objet au-dessus d'elle ou autour d'elle.
    static void DeplacerBonusCentre(Vector3 limite, Vector3 hautTable, StringBuilder rapport, StringBuilder aVerifier)
    {
        GameObject groupe = TrouverParNom("Bonus_Centre");
        if (groupe == null) return;
        var pastilles = groupe.GetComponentsInChildren<CibleBonus>(true).Select(c => c.transform).ToList();
        if (pastilles.Count == 0) return;

        const float marge = 0.55f;   // rayon d'une pastille + écart
        float plusBas = pastilles.Min(t => Vector3.Dot(t.position - limite, hautTable));
        float besoin = marge - plusBas;
        if (besoin <= 0f) return;    // déjà au-dessus de l'échelle

        Vector3 depart = groupe.transform.position;
        for (float essai = besoin; essai <= besoin + 6f; essai += 0.25f)
        {
            Vector3 decalage = hautTable * essai;
            bool libre = pastilles.All(t => PlaceLibre(t.position + decalage, groupe.transform, 0.45f));
            if (!libre) continue;
            Undo.RecordObject(groupe.transform, "Remonter pastilles");
            groupe.transform.position = depart + decalage;
            rapport.AppendLine($"- Pastilles \"Bonus_Centre\" remontées de {essai:F2} vers le haut de la table (place libre trouvée).");
            return;
        }
        aVerifier.AppendLine("- Pas de place libre trouvée pour remonter \"Bonus_Centre\" : pastilles laissées en place (elles chevauchent l'échelle).");
    }

    // Rien au-dessus (hors vitre) et aucun collider solide dans le rayon, à part le plateau et l'objet lui-même
    static bool PlaceLibre(Vector3 point, Transform ignorer, float rayon)
    {
        if (!SurfacePlateau(point.x, point.z, out Vector3 sol, out Vector3 n)) return false;
        if (ObjetsAuDessus(sol).Any()) return false;
        foreach (Collider c in Physics.OverlapSphere(sol + n * 0.3f, rayon, ~0, QueryTriggerInteraction.Ignore))
            if (!EstSurfaceOuVitre(c) && !c.transform.IsChildOf(ignorer)) return false;
        return true;
    }

    static IEnumerable<string> ObjetsAuDessus(Vector3 pointPlateau)
    {
        foreach (RaycastHit h in Physics.RaycastAll(pointPlateau + Vector3.up * 6f, Vector3.down, 6.05f, ~0, QueryTriggerInteraction.Ignore))
            if (!EstSurfaceOuVitre(h.collider) && h.point.y > pointPlateau.y + 0.01f)
                yield return h.collider.name;
    }

    static bool EstSurfaceOuVitre(Collider c) => c.name == "Playfield" || c.name == "Vitre" || c.name == "Bottom" || Bille.Est(c.gameObject);

    static bool SurfacePlateau(float x, float z, out Vector3 point, out Vector3 normale)
    {
        foreach (RaycastHit h in Physics.RaycastAll(new Vector3(x, 60f, z), Vector3.down, 120f, ~0, QueryTriggerInteraction.Ignore))
            if (h.collider.name == "Playfield")
            {
                point = h.point;
                normale = h.normal;
                return true;
            }
        point = default;
        normale = Vector3.up;
        return false;
    }

    // ==================================================================== MODÈLE

    // Étoile à 5 branches, rayon extérieur 1 (mis à la taille par l'échelle x/z), pointe vers +Z.
    // Sous-mesh 0 : dessus plat (verre émissif). Sous-mesh 1 : biseau tout autour (liseré métal).
    static Mesh MeshEtoileInsert()
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(CheminMeshEtoile);
        bool nouveau = mesh == null;
        if (nouveau) mesh = new Mesh { name = "Etoile_Insert" };
        mesh.Clear();

        const float epaisseur = 0.03f, bas = 0.004f, rapportInterieur = 0.42f, retrait = 0.86f;
        var contour = new Vector3[10];
        for (int i = 0; i < 10; i++)
        {
            float a = (90f + i * 36f) * Mathf.Deg2Rad;
            float r = i % 2 == 0 ? 1f : rapportInterieur;
            contour[i] = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
        }

        var v = new List<Vector3>();
        var uv = new List<Vector2>();
        var dessus = new List<int>();
        var biseau = new List<int>();
        void Sommet(Vector3 p) { v.Add(p); uv.Add(new Vector2(p.x * 0.5f + 0.5f, p.z * 0.5f + 0.5f)); }
        // Triangle orienté vers "dir" (Unity : sommets dans le sens horaire vus de face)
        void Triangle(List<int> liste, int a, int b, int c, Vector3 dir)
        {
            if (Vector3.Dot(Vector3.Cross(v[b] - v[a], v[c] - v[a]), dir) < 0f) (b, c) = (c, b);
            liste.Add(a); liste.Add(b); liste.Add(c);
        }

        // Dessus : éventail depuis le centre
        Sommet(new Vector3(0f, epaisseur, 0f));
        for (int i = 0; i < 10; i++) Sommet(contour[i] * retrait + Vector3.up * epaisseur);
        for (int i = 0; i < 10; i++) Triangle(dessus, 0, 1 + i, 1 + (i + 1) % 10, Vector3.up);

        // Biseau : un quad par côté (sommets séparés : arêtes nettes)
        for (int i = 0; i < 10; i++)
        {
            Vector3 h0 = contour[i] * retrait + Vector3.up * epaisseur, h1 = contour[(i + 1) % 10] * retrait + Vector3.up * epaisseur;
            Vector3 b0 = contour[i] + Vector3.up * bas, b1 = contour[(i + 1) % 10] + Vector3.up * bas;
            Vector3 exterieur = Vector3.Cross(Vector3.up, b1 - b0).normalized;
            if (Vector3.Dot(exterieur, (b0 + b1) * 0.5f) < 0f) exterieur = -exterieur;
            int k = v.Count;
            Sommet(h0); Sommet(h1); Sommet(b1); Sommet(b0);
            Triangle(biseau, k, k + 1, k + 2, exterieur + Vector3.up);
            Triangle(biseau, k, k + 2, k + 3, exterieur + Vector3.up);
        }

        mesh.SetVertices(v);
        mesh.SetUVs(0, uv);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(dessus, 0);
        mesh.SetTriangles(biseau, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();   // nécessaire au shader URP (sinon image cassée avec le bloom)
        mesh.RecalculateBounds();

        if (nouveau)
        {
            CreerDossier("Assets/Modeles");
            AssetDatabase.CreateAsset(mesh, CheminMeshEtoile);
        }
        else EditorUtility.SetDirty(mesh);
        return mesh;
    }
}
