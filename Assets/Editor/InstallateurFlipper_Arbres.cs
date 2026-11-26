using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Arbres en feu : décor + bumper (repousse la bille, 100 points, flammes qui jaillissent au choc).
// Menu "Flipper > Ajouter des arbres en feu" : crée le prefab Assets/Prefabs/ArbreEnFeu.prefab (s'il n'existe pas)
// et pose 3 arbres dans des endroits libres du plateau. Chaque appel en ajoute 3 de plus.
public static partial class InstallateurFlipper
{
    const string CheminPrefabArbre = "Assets/Prefabs/ArbreEnFeu.prefab";
    const int ArbresParAjout = 3;
    const float EcartArbres = 2.2f;

    [MenuItem("Flipper/Ajouter des arbres en feu", priority = 3)]
    public static void MenuAjouterArbres()
    {
        var rapport = new StringBuilder("Flipper > Ajouter des arbres en feu :\n");
        var aVerifier = new StringBuilder();
        Undo.SetCurrentGroupName("Ajouter des arbres en feu");
        int groupe = Undo.GetCurrentGroup();

        GameObject prefab = CreerPrefabArbre(rapport);
        PoserArbres(prefab, ArbresParAjout, rapport, aVerifier);

        Undo.CollapseUndoOperations(groupe);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        if (aVerifier.Length > 0) rapport.Append("\nAttention :\n").Append(aVerifier);
        Debug.Log(rapport.ToString());
    }

    // ==================================================================== PREFAB

    static GameObject CreerPrefabArbre(StringBuilder rapport)
    {
        var existant = AssetDatabase.LoadAssetAtPath<GameObject>(CheminPrefabArbre);
        if (existant != null) return existant;

        CreerDossier("Assets/Prefabs");
        CreerDossier("Assets/Modeles");

        // Cône low-poly (facettes) partagé par les 3 étages du feuillage
        Mesh cone = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Modeles/Cone_Arbre.asset");
        if (cone == null)
        {
            cone = MeshCone(9);
            AssetDatabase.CreateAsset(cone, "Assets/Modeles/Cone_Arbre.asset");
        }
        else if (cone.tangents.Length == 0)
        {
            cone.RecalculateTangents();
            EditorUtility.SetDirty(cone);
        }
        Material matTronc = MateriauCouleur($"{DossierMateriaux}/Arbre_Tronc.mat", new Color(0.16f, 0.09f, 0.05f), Color.black, 0.15f);
        // Feuillage carbonisé avec fissures de braise (mêmes textures que les murs, plus sombres).
        // Une émission avec une texture d'émission : une émission seule cassait l'image avec le bloom.
        Material matFeuillage = MateriauLit($"{DossierMateriaux}/Arbre_Feuillage.mat",
            Charger("BoisBrule_Albedo"), Charger("BoisBrule_Normal", true), Charger("BoisBrule_Emission"),
            new Color(1f, 0.42f, 0.12f) * 1.2f, 0f, 0.2f, new Vector2(2f, 2f), new Color(0.55f, 0.55f, 0.55f));
        Material matFlamme = MateriauFlamme();

        var racine = new GameObject("ArbreEnFeu");

        // Feuillage en premier : BumperEffect fait clignoter le premier rendu trouvé
        var feuillage = new GameObject("Feuillage");
        feuillage.transform.SetParent(racine.transform, false);
        float[] rayons = { 0.5f, 0.4f, 0.28f };
        for (int i = 0; i < rayons.Length; i++)
        {
            var etage = new GameObject($"Etage_{i + 1}");
            etage.transform.SetParent(feuillage.transform, false);
            etage.transform.localPosition = new Vector3(0f, 0.32f + i * 0.36f, 0f);
            etage.transform.localRotation = Quaternion.Euler(0f, i * 20f, 0f);
            etage.transform.localScale = new Vector3(rayons[i], 0.62f, rayons[i]);
            etage.AddComponent<MeshFilter>().sharedMesh = cone;
            etage.AddComponent<MeshRenderer>().sharedMaterial = matFeuillage;
        }

        // Tronc (cylindre sans collider : la collision est la capsule de la racine)
        GameObject tronc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(tronc.GetComponent<Collider>());
        tronc.name = "Tronc";
        tronc.transform.SetParent(racine.transform, false);
        tronc.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        tronc.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
        tronc.GetComponent<Renderer>().sharedMaterial = matTronc;

        // Flammes
        var goFlammes = new GameObject("Flammes");
        goFlammes.transform.SetParent(racine.transform, false);
        goFlammes.transform.localPosition = new Vector3(0f, 0.55f, 0f);
        ParticleSystem flammes = ConfigurerFlammes(goFlammes, matFlamme);

        // Lueur du feu (vacille, script ArbreEnFeu)
        var goLumiere = new GameObject("Lueur");
        goLumiere.transform.SetParent(racine.transform, false);
        goLumiere.transform.localPosition = new Vector3(0f, 1.7f, 0f);   // au-dessus : éclaire le plateau autour
        var lumiere = goLumiere.AddComponent<Light>();
        lumiere.type = LightType.Point;
        lumiere.range = 2.4f;
        lumiere.intensity = 2f;
        lumiere.color = new Color(1f, 0.5f, 0.15f);
        lumiere.shadows = LightShadows.None;

        // Collision + bumper
        var capsule = racine.AddComponent<CapsuleCollider>();
        capsule.direction = 1;
        capsule.center = new Vector3(0f, 0.75f, 0f);
        capsule.radius = 0.42f;
        capsule.height = 1.5f;
        capsule.sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>($"{DossierPhysique}/Caoutchouc.physicMaterial");

        var rb = racine.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        var bumper = racine.AddComponent<BumperEffectBrule>();
        bumper.points = 100;
        bumper.impulsion = 8f;
        bumper.visuel = feuillage.transform;
        bumper.couleurTouche = new Color(1f, 0.6f, 0.2f);
        bumper.multiplicateurEchelle = 1.15f;
        bumper.son = Son("Arbre_Feu");

        var arbre = racine.AddComponent<ArbreEnFeu>();
        arbre.flammes = flammes;
        arbre.lumiere = lumiere;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(racine, CheminPrefabArbre);
        Object.DestroyImmediate(racine);
        rapport.AppendLine($"- Prefab créé : {CheminPrefabArbre}");
        return prefab;
    }

    static ParticleSystem ConfigurerFlammes(GameObject go, Material mat)
    {
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.8f, 0.35f), new Color(1f, 0.45f, 0.1f));
        main.gravityModifier = -0.15f;   // les flammes montent
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 250;

        var emission = ps.emission;
        emission.rateOverTime = 35f;

        var forme = ps.shape;
        forme.shapeType = ParticleSystemShapeType.Cone;
        forme.angle = 12f;
        forme.radius = 0.32f;
        forme.rotation = new Vector3(-90f, 0f, 0f);   // le cône émet vers le haut de l'arbre

        var couleur = ps.colorOverLifetime;
        couleur.enabled = true;
        var degrade = new Gradient();
        degrade.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.9f, 0.5f), 0f), new GradientColorKey(new Color(1f, 0.45f, 0.1f), 0.4f), new GradientColorKey(new Color(0.5f, 0.08f, 0.03f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.12f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) });
        couleur.color = degrade;

        var taille = ps.sizeOverLifetime;
        taille.enabled = true;
        taille.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.25f));

        var bruit = ps.noise;
        bruit.enabled = true;
        bruit.strength = 0.25f;
        bruit.frequency = 1.5f;

        var rendu = go.GetComponent<ParticleSystemRenderer>();
        rendu.renderMode = ParticleSystemRenderMode.Billboard;
        rendu.sharedMaterial = mat;
        rendu.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return ps;
    }

    // Cône de rayon 1 et hauteur 1 (base en y = 0), facettes nettes (style low-poly)
    static Mesh MeshCone(int cotes)
    {
        var sommets = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();
        Vector3 pointe = new Vector3(0f, 1f, 0f);
        for (int i = 0; i < cotes; i++)
        {
            float a0 = i * Mathf.PI * 2f / cotes, a1 = (i + 1) * Mathf.PI * 2f / cotes;
            Vector3 p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
            // côté
            int b = sommets.Count;
            sommets.AddRange(new[] { p0, pointe, p1 });
            uv.AddRange(new[] { new Vector2((float)i / cotes, 0f), new Vector2((i + 0.5f) / cotes, 1f), new Vector2((float)(i + 1) / cotes, 0f) });
            triangles.AddRange(new[] { b, b + 1, b + 2 });
            // dessous
            b = sommets.Count;
            sommets.AddRange(new[] { p0, p1, Vector3.zero });
            uv.AddRange(new[] { Vector2.zero, Vector2.right, Vector2.one * 0.5f });
            triangles.AddRange(new[] { b, b + 1, b + 2 });
        }
        var mesh = new Mesh { name = "Cone_Arbre" };
        mesh.SetVertices(sommets);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();   // sans tangentes, le shader URP produit des valeurs invalides (image cassée avec le bloom)
        mesh.RecalculateBounds();
        return mesh;
    }

    // Matériau URP Lit uni, avec émission optionnelle (braises)
    static Material MateriauCouleur(string chemin, Color couleur, Color emission, float lissage)
    {
        CreerDossier(System.IO.Path.GetDirectoryName(chemin).Replace('\\', '/'));
        var mat = AssetDatabase.LoadAssetAtPath<Material>(chemin);
        bool nouveau = mat == null;
        if (nouveau) mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", couleur);
        mat.SetFloat("_Smoothness", lissage);
        if (emission.maxColorComponent > 0f)
        {
            mat.SetColor("_EmissionColor", emission);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        if (nouveau) AssetDatabase.CreateAsset(mat, chemin);
        else EditorUtility.SetDirty(mat);
        return mat;
    }

    // Particules additives (les flammes s'additionnent et brillent)
    static Material MateriauFlamme()
    {
        string chemin = $"{DossierMateriaux}/Flamme.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(chemin);
        if (mat != null) return mat;

        string cheminTex = $"{DossierTextures}/Flamme.png";
        if (AssetImporter.GetAtPath(cheminTex) is TextureImporter imp && (!imp.alphaIsTransparency || imp.wrapMode != TextureWrapMode.Clamp))
        {
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
        }

        mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(cheminTex));
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Surface", 1f);   // transparent
        mat.SetFloat("_Blend", 2f);     // additif
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        AssetDatabase.CreateAsset(mat, chemin);
        return mat;
    }

    // ==================================================================== PLACEMENT

    // Pose "nombre" arbres dans des endroits libres, en partant du milieu du plateau.
    static void PoserArbres(GameObject prefab, int nombre, StringBuilder rapport, StringBuilder aVerifier)
    {
        GameObject bille = TrouverBille();
        if (bille == null || !RaycastSousBille(bille, out RaycastHit sol))
        {
            aVerifier.AppendLine("- Bille ou plateau introuvable : arbres non posés.");
            return;
        }
        Physics.SyncTransforms();
        Vector3 normale = sol.normal;
        Vector3 hautTable = Vector3.ProjectOnPlane(Vector3.forward, normale).normalized;
        Vector3 droite = Vector3.Cross(normale, hautTable);

        Vector3 centre = sol.point;
        GameObject plateau = TrouverParNom("Playfield");
        if (plateau != null && plateau.GetComponent<Renderer>() != null)
        {
            Vector3 c = plateau.GetComponent<Renderer>().bounds.center;
            centre = c - normale * Vector3.Dot(c - sol.point, normale);
        }

        // Arbres déjà présents : on garde l'écart avec eux
        var occupes = TousLesObjets().Where(go => go != null && go.GetComponent<ArbreEnFeu>() != null).Select(go => go.transform.position).ToList();
        int numero = occupes.Count;

        // Candidats autour du milieu du plateau (pas trop près des flippers ni du couloir du lanceur)
        var candidats = new List<(float score, Vector3 p)>();
        for (float v = -4f; v <= 7f; v += 0.5f)
            for (float u = -5f; u <= 4f; u += 0.5f)
                candidats.Add((u * u + (v - 1f) * (v - 1f), centre + droite * u + hautTable * v));

        int poses = 0;
        foreach (var (_, p) in candidats.OrderBy(c => c.score))
        {
            if (poses >= nombre) break;
            if (occupes.Any(o => Vector3.Distance(o, p) < EcartArbres)) continue;
            if (!LibrePourArbre(p, normale)) continue;

            var arbre = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(arbre, "Arbre en feu");
            arbre.name = $"ArbreEnFeu_{++numero}";
            arbre.transform.SetPositionAndRotation(p, Quaternion.LookRotation(hautTable, normale));
            occupes.Add(p);
            poses++;
        }
        rapport.AppendLine($"- {poses} arbre(s) en feu posé(s) (bumpers : 100 pts, ils repoussent la bille).");
        if (poses < nombre)
            aVerifier.AppendLine($"- Seulement {poses} place(s) libre(s) trouvée(s) pour les arbres.");
    }

    // Libre = rien autour (ni objet, ni insert, ni zone) sur la hauteur de l'arbre, et du plateau dessous.
    static bool LibrePourArbre(Vector3 p, Vector3 normale)
    {
        foreach (Collider c in Physics.OverlapCapsule(p + normale * 0.3f, p + normale * 1.9f, 0.85f, ~0, QueryTriggerInteraction.Collide))
        {
            if (c.name == "Playfield" || c.name == "Vitre" || Bille.Est(c.gameObject)) continue;
            return false;
        }
        return Physics.Raycast(p + normale * 0.3f, -normale, out RaycastHit sol, 0.6f, ~0, QueryTriggerInteraction.Ignore)
            && sol.collider.name == "Playfield";
    }
}
