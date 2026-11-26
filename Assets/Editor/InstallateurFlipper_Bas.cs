using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Bas du plateau :
//  - "Flipper > Boucher les murets du bas" : si un muret du bas (rabatoire_bas_*) ne touche pas le mur
//    du côté (la bille passait par le trou), il est rallongé côté mur, son bout côté flipper ne bouge pas.
//  - "Flipper > 2nd chance" : zone invisible sur l'ampoule du couloir de droite ; au passage de la bille,
//    chance (au hasard) de gagner une bille en plus (script SecondeChance).
// Les deux sont aussi faits par Installer. On peut les relancer.
public static partial class InstallateurFlipper
{
    const string NomSecondeChance = "Seconde_Chance";

    [MenuItem("Flipper/Boucher les murets du bas", priority = 11)]
    public static void MenuBoucherMuretsBas()
    {
        var rapport = new StringBuilder("Flipper > Boucher les murets du bas :\n");
        var aVerifier = new StringBuilder();
        Undo.SetCurrentGroupName("Boucher les murets du bas");
        int groupe = Undo.GetCurrentGroup();
        BoucherMuretsBas(rapport, aVerifier);
        HabillerMurets(rapport, aVerifier);   // les modèles suivent la nouvelle longueur
        Undo.CollapseUndoOperations(groupe);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        if (aVerifier.Length > 0) rapport.Append("\nAttention :\n").Append(aVerifier);
        Debug.Log(rapport.ToString());
    }

    [MenuItem("Flipper/2nd chance", priority = 12)]
    public static void MenuSecondeChance()
    {
        var rapport = new StringBuilder("Flipper > 2nd chance :\n");
        var aVerifier = new StringBuilder();
        Undo.SetCurrentGroupName("2nd chance");
        int groupe = Undo.GetCurrentGroup();
        PoserSecondeChance(rapport, aVerifier);
        Undo.CollapseUndoOperations(groupe);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        if (aVerifier.Length > 0) rapport.Append("\nAttention :\n").Append(aVerifier);
        Debug.Log(rapport.ToString());
    }

    // Matériau TextMeshPro "néon" : face en couleur HDR (au-delà du blanc, le bloom la fait briller) + halo (Glow)
    static Material MateriauNeon(TMPro.TMP_FontAsset police)
    {
        string chemin = $"{DossierMateriaux}/Ampoules/Texte_Neon.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(chemin);
        bool nouveau = mat == null;
        if (nouveau) mat = new Material(police.material) { name = "Texte_Neon" };
        mat.SetColor("_FaceColor", new Color(1f, 0.75f, 0.45f) * 2.5f);
        mat.EnableKeyword("GLOW_ON");
        mat.SetColor("_GlowColor", new Color(1f, 0.45f, 0.1f, 0.9f));
        mat.SetFloat("_GlowOffset", 0f);
        mat.SetFloat("_GlowInner", 0.15f);
        mat.SetFloat("_GlowOuter", 0.6f);
        mat.SetFloat("_GlowPower", 0.85f);
        mat.SetFloat("_OutlineWidth", 0.12f);
        mat.SetColor("_OutlineColor", new Color(1f, 0.5f, 0.15f) * 1.5f);
        if (nouveau)
        {
            CreerDossier($"{DossierMateriaux}/Ampoules");
            AssetDatabase.CreateAsset(mat, chemin);
        }
        else EditorUtility.SetDirty(mat);
        return mat;
    }

    // ==================================================================== ENTRÉES DE LA RAMPE

    // Les pentes d'accès et entonnoirs au pied de la rampe (Entrees_Rampe_Collision) sont calculés à partir
    // de la rampe. Si la rampe a été déplacée, ils restent à l'ancienne place et arrêtent la bille :
    // ce menu les reconstruit à la place actuelle de la rampe (Installer le fait aussi).
    [MenuItem("Flipper/Refaire les entrées de la rampe", priority = 15)]
    public static void MenuEntreesRampe()
    {
        var rapport = new StringBuilder("Flipper > Refaire les entrées de la rampe :\n");
        Undo.SetCurrentGroupName("Entrées de la rampe");
        int groupe = Undo.GetCurrentGroup();
        GameObject bille = TrouverBille();
        GameObject depart = TrouverParNom("Depart_Bille");
        if (bille != null && depart != null && RaycastSousBille(bille, out RaycastHit sol))
        {
            Vector3 normale = sol.normal;
            Vector3 hautTable = Vector3.ProjectOnPlane(Vector3.forward, normale).normalized;
            float rayon = DiametreBille(bille) * 0.5f;
            ConstruireEntreesRails(depart.transform.position - normale * rayon, normale, hautTable, rapport);
        }
        else rapport.AppendLine("- Bille, point de départ ou plateau introuvable : rien changé.");
        Undo.CollapseUndoOperations(groupe);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log(rapport.ToString());
    }

    // ==================================================================== LANCE-PIERRES D'ORIGINE (Miscellaneous)

    // La bille s'arrêtait à gauche : le lance-pierre d'origine de gauche ("Miscellaneous") était enfoncé
    // sous le plateau, sa plaque en plastique (avec collision) faisait une marche à ras du sol.
    //  - chaque "Miscellaneous" est remis à la même hauteur au-dessus du plateau que celui de droite
    //    (bas de l'élastique posé sur le plateau) ;
    //  - les plaques en plastique n'ont plus de collision (au-dessus de la bille, comme sur un vrai flipper).
    [MenuItem("Flipper/Réparer la bille qui s'arrête à gauche", priority = 14)]
    public static void MenuReparerLancePierres()
    {
        var rapport = new StringBuilder("Flipper > Réparer la bille qui s'arrête à gauche :\n");
        var aVerifier = new StringBuilder();
        Undo.SetCurrentGroupName("Réparer lance-pierres");
        int groupe = Undo.GetCurrentGroup();
        ReparerLancePierres(rapport, aVerifier);
        Undo.CollapseUndoOperations(groupe);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        if (aVerifier.Length > 0) rapport.Append("\nAttention :\n").Append(aVerifier);
        Debug.Log(rapport.ToString());
    }

    static void ReparerLancePierres(StringBuilder rapport, StringBuilder aVerifier)
    {
        Physics.SyncTransforms();
        var hauteurs = new System.Collections.Generic.Dictionary<GameObject, (float bas, Vector3 normale)>();
        foreach (GameObject misc in SceneManager.GetActiveScene().GetRootGameObjects().Where(g => g.name.StartsWith("Miscellaneous")))
        {
            // Plaques en plastique : plus de collision
            foreach (Collider c in misc.GetComponentsInChildren<Collider>(true))
                if (c.name.ToLower().Contains("plastic"))
                {
                    rapport.AppendLine($"- {misc.name}/{c.name} : collision retirée (plaque au-dessus de la bille).");
                    Undo.DestroyObjectImmediate(c);
                }

            // Hauteur du bas de l'élastique au-dessus du plateau (il est tendu à hauteur de bille, pas posé au sol)
            Renderer elastique = misc.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name.ToLower().Contains("rubber"));
            if (elastique == null) continue;
            Bounds b = elastique.bounds;
            if (!SurfacePlateau(b.center.x, b.center.z, out Vector3 sol, out Vector3 normale)) continue;
            // Point le plus bas de l'élastique dans la direction de la normale du plateau
            Mesh mesh = elastique.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null) continue;
            float plusBas = float.MaxValue;
            foreach (Vector3 v in mesh.vertices)
                plusBas = Mathf.Min(plusBas, Vector3.Dot(elastique.transform.TransformPoint(v) - sol, normale));
            hauteurs[misc] = (plusBas, normale);
            rapport.AppendLine($"- {misc.name} : bas de l'élastique à {plusBas:F2} au-dessus du plateau.");
        }

        // Référence : le plus haut (celui de droite, qui marche) ; ceux qui sont plus bas sont remontés à sa hauteur
        if (hauteurs.Count < 2) return;
        float reference = hauteurs.Values.Max(h => h.bas);
        foreach (var paire in hauteurs)
        {
            float ecart = reference - paire.Value.bas;
            if (ecart < 0.02f) continue;
            Undo.RecordObject(paire.Key.transform, "Hauteur lance-pierre");
            paire.Key.transform.position += paire.Value.normale * ecart;
            rapport.AppendLine($"- {paire.Key.name} : remonté de {ecart:F2} (même hauteur que l'autre lance-pierre).");
        }
    }

    // Le lance-pierre d'origine de droite ("Miscellaneous (1)") devient le miroir exact de celui de gauche
    // ("Miscellaneous") par rapport à l'axe entre les flippers : position et rotation en miroir, et pièce
    // retournée (échelle X négative) puisque ce sont des pièces gauche / droite.
    [MenuItem("Flipper/Lance-pierre droit = miroir du gauche", priority = 21)]
    public static void MenuMiroirLancePierres()
    {
        var rapport = new StringBuilder("Flipper > Lance-pierre droit = miroir du gauche :\n");
        var aVerifier = new StringBuilder();
        Undo.SetCurrentGroupName("Miroir lance-pierres");
        int groupe = Undo.GetCurrentGroup();
        MiroirLancePierres(rapport, aVerifier);
        Undo.CollapseUndoOperations(groupe);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        if (aVerifier.Length > 0) rapport.Append("\nAttention :\n").Append(aVerifier);
        Debug.Log(rapport.ToString());
    }

    static void MiroirLancePierres(StringBuilder rapport, StringBuilder aVerifier)
    {
        GameObject gauche = TrouverParNom("Miscellaneous"), droite = TrouverParNom("Miscellaneous (1)");
        Transform fg = TrouverParNom("flipper_gauche")?.transform, fd = TrouverParNom("flipper_droite")?.transform;
        if (gauche == null || droite == null || fg == null || fd == null)
        {
            aVerifier.AppendLine("- \"Miscellaneous\", \"Miscellaneous (1)\" ou les flippers introuvables.");
            return;
        }
        float axe = (fg.position.x + fd.position.x) * 0.5f;
        Transform g = gauche.transform, d = droite.transform;
        Undo.RecordObject(d, "Miroir lance-pierre");
        Vector3 p = g.position;
        Quaternion q = g.rotation;
        d.SetPositionAndRotation(new Vector3(2f * axe - p.x, p.y, p.z), new Quaternion(q.x, -q.y, -q.z, q.w));
        Vector3 s = g.localScale;
        d.localScale = new Vector3(-s.x, s.y, s.z);   // retourné : une vraie pièce de droite
        rapport.AppendLine($"- {droite.name} = miroir de {gauche.name} : position ({d.position.x:F2}, {d.position.z:F2}), pièce retournée.");

        // Ce que le lance-pierre de droite touche à sa nouvelle place
        Physics.SyncTransforms();
        foreach (Collider c in droite.GetComponentsInChildren<Collider>())
        {
            Bounds b = c.bounds;
            var genes = Physics.OverlapBox(b.center, b.extents * 0.9f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
                .Where(o => !EstSurfaceOuVitre(o) && !o.transform.IsChildOf(droite.transform))
                .Select(o => o.name).Distinct().ToList();
            rapport.AppendLine($"- {c.name} : de X {b.min.x:F2} à {b.max.x:F2}, Z {b.min.z:F2} à {b.max.z:F2}.");
            if (genes.Count > 0) aVerifier.AppendLine($"- {c.name} touche peut-être : {string.Join(", ", genes)} (boîtes qui se chevauchent).");
        }
    }

    // ==================================================================== ARBRE CONTRE LE MUR GAUCHE

    // Après la courbe du haut à gauche, la bille descendait toujours collée au mur gauche jusqu'en bas.
    //  - Un muret en biais part du mur gauche vers le bas et le centre : la bille glisse dessus et part vers
    //    le milieu (l'angle s'ouvre vers le bas : elle ne peut pas s'y coincer).
    //  - Un arbre bumper (taille normale) juste en dessous, à plus d'une bille du muret, la renvoie dans le jeu.
    // (Un arbre seul contre le mur laissait une poche où la bille restait posée.)
    const string NomArbreMurGauche = "ArbreEnFeu_Mur_Gauche";
    const string NomMuretMurGauche = "rabatoire_mur_gauche";   // "rabatoire..." : points au choc, matériau du plateau
    static readonly Vector2 PlaceArbreMurGauche = new Vector2(-32.3f, -6.2f);   // (x, z)
    static readonly Vector2 MuretMurGaucheHaut = new Vector2(-34.6f, -3.6f);    // sur le plateau, contre le mur
    const float MuretMurGaucheDansLeMur = 0.35f;                                  // prolongé de ça dans le mur : pas de fente
    static readonly Vector2 MuretMurGaucheBas = new Vector2(-33.2f, -4.9f);

    [MenuItem("Flipper/Arbre contre le mur gauche", priority = 13)]
    public static void MenuArbreMurGauche()
    {
        var rapport = new StringBuilder("Flipper > Arbre contre le mur gauche :\n");
        var aVerifier = new StringBuilder();
        Undo.SetCurrentGroupName("Arbre contre le mur gauche");
        int groupe = Undo.GetCurrentGroup();
        PoserArbreMurGauche(rapport, aVerifier);
        Undo.CollapseUndoOperations(groupe);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        if (aVerifier.Length > 0) rapport.Append("\nAttention :\n").Append(aVerifier);
        Debug.Log(rapport.ToString());
    }

    static void PoserArbreMurGauche(StringBuilder rapport, StringBuilder aVerifier)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CheminPrefabArbre);
        if (prefab == null)
        {
            aVerifier.AppendLine($"- Prefab {CheminPrefabArbre} introuvable (Flipper > Ajouter des arbres en feu d'abord).");
            return;
        }
        GameObject arbre = TrouverParNom(NomArbreMurGauche);
        if (arbre != null) Undo.DestroyObjectImmediate(arbre);   // refait (pour la vérification de place)
        Physics.SyncTransforms();

        // Muret en biais (cube de collision invisible + modèle des rabatoires, via HabillerMurets)
        if (SurfacePlateau(MuretMurGaucheHaut.x, MuretMurGaucheHaut.y, out Vector3 a, out Vector3 na)
            && SurfacePlateau(MuretMurGaucheBas.x, MuretMurGaucheBas.y, out Vector3 b, out Vector3 _))
        {
            GameObject muret = TrouverParNom(NomMuretMurGauche);
            if (muret == null)
            {
                muret = GameObject.CreatePrimitive(PrimitiveType.Cube);
                muret.name = NomMuretMurGauche;
                Undo.RegisterCreatedObjectUndo(muret, NomMuretMurGauche);
                if (TrouverParNom("Collisions") is GameObject col) muret.transform.SetParent(col.transform, true);
                muret.GetComponent<Collider>().sharedMaterial = Materiau("Plateau", 0.1f, 0.3f, PhysicsMaterialCombine.Average);
                var s = Undo.AddComponent<ScoreOnHit>(muret);
                s.points = PointsRabatoires;
            }
            Vector3 dir = (b - a).normalized;
            a -= dir * MuretMurGaucheDansLeMur;   // le haut du muret rentre dans le mur gauche
            Undo.RecordObject(muret.transform, "Muret mur gauche");
            muret.transform.SetPositionAndRotation((a + b) * 0.5f + na * 0.5f, Quaternion.LookRotation(Vector3.Cross(dir, na), na));
            muret.transform.localScale = new Vector3(Vector3.Distance(a, b), 1f, 0.1f);
            rapport.AppendLine($"- Muret en biais contre le mur gauche, de ({a.x:F2}, {a.z:F2}) à ({b.x:F2}, {b.z:F2}) : la bille qui descend le long du mur part vers le centre.");
            HabillerMurets(rapport, aVerifier);
        }
        else aVerifier.AppendLine("- Plateau introuvable aux bouts du muret en biais : muret non posé.");
        if (!SurfacePlateau(PlaceArbreMurGauche.x, PlaceArbreMurGauche.y, out Vector3 point, out Vector3 normale))
        {
            aVerifier.AppendLine("- Plateau introuvable à l'endroit de l'arbre du mur gauche.");
            return;
        }
        // Rien d'autre que le mur sur sa place (rayon d'un arbre : 0.5)
        Transform caisse = TrouverParNom("PinballTable")?.transform;
        var genes = Physics.OverlapSphere(point + normale * 0.6f, 0.5f, ~0, QueryTriggerInteraction.Ignore)
            .Where(c => !EstSurfaceOuVitre(c) && (caisse == null || !c.transform.IsChildOf(caisse)))
            .Select(c => c.name).Distinct().ToList();
        if (genes.Count > 0) aVerifier.AppendLine($"- L'arbre du mur gauche touche : {string.Join(", ", genes)}.");

        arbre = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        arbre.name = NomArbreMurGauche;
        Undo.RegisterCreatedObjectUndo(arbre, NomArbreMurGauche);
        Vector3 hautTable = Vector3.ProjectOnPlane(Vector3.forward, normale).normalized;
        arbre.transform.SetPositionAndRotation(point, Quaternion.LookRotation(hautTable, normale));
        rapport.AppendLine($"- Arbre bumper posé contre le mur gauche ({point.x:F2}, {point.z:F2}) : il casse la descente le long du mur.");
    }

    // ==================================================================== MURETS DU BAS

    static void BoucherMuretsBas(StringBuilder rapport, StringBuilder aVerifier)
    {
        Transform fg = TrouverParNom("flipper_gauche")?.transform, fd = TrouverParNom("flipper_droite")?.transform;
        if (fg == null || fd == null) return;
        float axe = (fg.position.x + fd.position.x) * 0.5f;
        Physics.SyncTransforms();

        foreach (string cote in new[] { "droite", "gauche" })
        {
            GameObject muret = TrouverParNom($"rabatoire_bas_{cote}");
            if (muret == null) continue;
            Transform t = muret.transform;
            Vector3 axeLong = t.right;                 // la longueur du cube est sur son X
            float longueur = t.lossyScale.x;
            Vector3 bout1 = t.position + axeLong * longueur * 0.5f, bout2 = t.position - axeLong * longueur * 0.5f;
            bool bout1Exterieur = Mathf.Abs(bout1.x - axe) > Mathf.Abs(bout2.x - axe);
            Vector3 exterieur = bout1Exterieur ? bout1 : bout2, interieur = bout1Exterieur ? bout2 : bout1;
            Vector3 versMur = (exterieur - interieur).normalized;

            // À hauteur de bille, depuis un peu avant le bout, vers le mur
            Vector3 depart = exterieur - versMur * 0.05f + t.up * 0.25f;
            var touche = Physics.RaycastAll(depart, versMur, 3f, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => h.collider.gameObject != muret && !EstSurfaceOuVitre(h.collider))
                .OrderBy(h => h.distance).FirstOrDefault();
            if (touche.collider == null)
            {
                aVerifier.AppendLine($"- {muret.name} : aucun mur trouvé à moins de 3 au bout du muret.");
                continue;
            }
            float trou = touche.distance - 0.05f;
            if (trou <= 0.02f)
            {
                rapport.AppendLine($"- {muret.name} touche déjà \"{touche.collider.name}\" (écart {trou:F2}).");
                continue;
            }
            float nouvelle = longueur + trou + 0.08f;   // + un peu : il rentre dans le mur, plus de fente
            Undo.RecordObject(t, "Boucher muret");
            Vector3 echelle = t.localScale;
            echelle.x = nouvelle;
            t.localScale = echelle;
            t.position = interieur + versMur * nouvelle * 0.5f;
            rapport.AppendLine($"- {muret.name} : il restait un trou de {trou:F2} (≈ {trou * 4.6f:F1} cm) avant \"{touche.collider.name}\" : " +
                               $"rallongé de {longueur:F2} à {nouvelle:F2}, bout côté flipper inchangé.");
        }
    }

    // ==================================================================== 2ND CHANCE

    static void PoserSecondeChance(StringBuilder rapport, StringBuilder aVerifier)
    {
        GameObject lampe = TrouverParNom("Ampoule_Couloir_Droit");
        if (lampe == null)
        {
            aVerifier.AppendLine("- Ampoule \"Ampoule_Couloir_Droit\" introuvable (Flipper > Ampoules et lanceur d'abord).");
            return;
        }
        GameObject zone = TrouverParNom(NomSecondeChance);
        if (zone == null)
        {
            zone = new GameObject(NomSecondeChance);
            Undo.RegisterCreatedObjectUndo(zone, NomSecondeChance);
        }
        Undo.RecordObject(zone.transform, "2nd chance");
        zone.transform.SetPositionAndRotation(lampe.transform.position, lampe.transform.rotation);

        // Toute la largeur du couloir (entre le muret et la bordure, 1.15) sur 0.7 de long
        var box = ObtenirOuAjouter<BoxCollider>(zone);
        box.isTrigger = true;
        box.center = new Vector3(0f, 0.3f, 0f);
        box.size = new Vector3(1.05f, 0.6f, 0.7f);

        // Inscription "2ND CHANCE" à plat dans le couloir, juste au-dessus de l'ampoule, lisible de bas en haut
        Transform tt = zone.transform.Find("Texte_2nd_Chance");
        TMPro.TextMeshPro texte;
        if (tt == null)
        {
            var gt = new GameObject("Texte_2nd_Chance");
            Undo.RegisterCreatedObjectUndo(gt, gt.name);
            gt.transform.SetParent(zone.transform, false);
            texte = gt.AddComponent<TMPro.TextMeshPro>();
        }
        else texte = ObtenirOuAjouter<TMPro.TextMeshPro>(tt.gameObject);
        Vector3 n = zone.transform.up, haut = zone.transform.forward, droite = zone.transform.right;
        Undo.RecordObject(texte.transform, "Texte 2nd chance");
        texte.transform.SetPositionAndRotation(zone.transform.position + haut * 0.72f + n * 0.006f,
                                               Quaternion.LookRotation(-n, -droite));   // couché, se lit vers le haut de la table
        texte.transform.localScale = Vector3.one;
        var police = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(CheminPoliceTmp);
        if (police != null) texte.font = police;
        texte.text = "2ND CHANCE";
        texte.fontSize = 1.5f;
        texte.fontStyle = TMPro.FontStyles.Bold;
        texte.characterSpacing = -4f;
        texte.alignment = TMPro.TextAlignmentOptions.Center;
        texte.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        texte.rectTransform.sizeDelta = new Vector2(1f, 0.3f);
        texte.color = new Color(1f, 0.55f, 0.2f, 0.9f);
        texte.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // Effet néon : lettres plus claires que blanc (le bloom les fait briller) + halo autour
        if (police != null) texte.fontSharedMaterial = MateriauNeon(police);
        EditorUtility.SetDirty(texte);

        // Petite lumière orange sous le texte (éclaire le couloir autour)
        Transform tl = texte.transform.Find("Lumiere_Neon");
        if (tl == null)
        {
            tl = new GameObject("Lumiere_Neon").transform;
            Undo.RegisterCreatedObjectUndo(tl.gameObject, tl.name);
            tl.SetParent(texte.transform, false);
        }
        tl.position = texte.transform.position + n * 0.25f;
        Light neon = ObtenirOuAjouter<Light>(tl.gameObject);
        neon.type = LightType.Point;
        neon.range = 1.2f;
        neon.intensity = 1f;
        neon.color = new Color(1f, 0.55f, 0.2f);
        neon.shadows = LightShadows.None;

        var sc = ObtenirOuAjouter<SecondeChance>(zone);
        sc.ampoule = lampe.GetComponentInChildren<Ampoule>();
        sc.texte = texte;
        sc.lumiereTexte = neon;
        if (sc.sonGagne == null) sc.sonGagne = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/son/Bonus_Jingle.wav");
        if (sc.sonPassage == null) sc.sonPassage = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/son/Insert_Ding.wav");
        EditorUtility.SetDirty(sc);
        rapport.AppendLine($"- 2nd chance posée sur l'ampoule du couloir de droite : {sc.probabilite * 100f:0} % de chance " +
                           "de gagner une bille en plus à chaque passage (l'ampoule clignote en doré tant qu'elle est gagnée).");
    }
}
