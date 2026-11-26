using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

//  - "Flipper > Bille en feu" : met le script BilleEnFeu sur la bille, avec ses flammes (particules qui
//    la suivent), sa lueur et la brillance de son matériau. Plus elle reste en vie, plus elle brûle.
//  - "Flipper > Pastilles autour de la rampe" : les 3 pastilles roses du milieu (Bonus_Centre) vont
//    à gauche, à droite et devant l'entrée de la rampe du milieu.
// Les deux sont aussi faits par Installer.
public static partial class InstallateurFlipper
{
    [MenuItem("Flipper/Bille en feu", priority = 16)]
    public static void MenuBilleEnFeu()
    {
        var rapport = new StringBuilder("Flipper > Bille en feu :\n");
        Undo.SetCurrentGroupName("Bille en feu");
        int groupe = Undo.GetCurrentGroup();
        InstallerBilleEnFeu(rapport);
        Undo.CollapseUndoOperations(groupe);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log(rapport.ToString());
    }

    [MenuItem("Flipper/Pastilles autour de la rampe", priority = 17)]
    public static void MenuPastillesRampe()
    {
        var rapport = new StringBuilder("Flipper > Pastilles autour de la rampe :\n");
        var aVerifier = new StringBuilder();
        Undo.SetCurrentGroupName("Pastilles autour de la rampe");
        int groupe = Undo.GetCurrentGroup();
        PastillesAutourRampe(rapport, aVerifier);
        Undo.CollapseUndoOperations(groupe);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        if (aVerifier.Length > 0) rapport.Append("\nAttention :\n").Append(aVerifier);
        Debug.Log(rapport.ToString());
    }

    // ==================================================================== BILLE EN FEU

    static void InstallerBilleEnFeu(StringBuilder rapport)
    {
        GameObject bille = TrouverBille();
        if (bille == null) { rapport.AppendLine("- Bille introuvable."); return; }

        // La brillance passe par l'émission du matériau : on l'active (noire = aucun changement tant qu'elle est froide)
        var rendu = bille.GetComponent<Renderer>();
        if (rendu != null && rendu.sharedMaterial != null && !rendu.sharedMaterial.IsKeywordEnabled("_EMISSION"))
        {
            Undo.RecordObject(rendu.sharedMaterial, "Émission bille");
            rendu.sharedMaterial.EnableKeyword("_EMISSION");
            rendu.sharedMaterial.SetColor("_EmissionColor", Color.black);
            rendu.sharedMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(rendu.sharedMaterial);
        }

        // Flammes : particules en espace monde (elles restent derrière la bille et font une traînée de feu)
        Transform tf = bille.transform.Find("Feu_Bille");
        if (tf == null)
        {
            tf = new GameObject("Feu_Bille").transform;
            Undo.RegisterCreatedObjectUndo(tf.gameObject, tf.name);
            tf.SetParent(bille.transform, false);
        }
        var ps = ObtenirOuAjouter<ParticleSystem>(tf.gameObject);
        var main = ps.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-3.14f, 3.14f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.45f, 0.1f));
        main.gravityModifier = -0.1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;   // tailles en mètres, quelle que soit l'échelle de la bille
        main.maxParticles = 300;
        main.playOnAwake = true;
        var emission = ps.emission;
        emission.rateOverTime = 0f;   // BilleEnFeu règle le débit selon la chaleur
        var forme = ps.shape;
        forme.shapeType = ParticleSystemShapeType.Sphere;
        forme.radius = 0.12f;
        var couleur = ps.colorOverLifetime;
        couleur.enabled = true;
        var degrade = new Gradient();
        degrade.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.95f, 0.6f), 0f), new GradientColorKey(new Color(1f, 0.45f, 0.1f), 0.35f), new GradientColorKey(new Color(0.55f, 0.08f, 0.03f), 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.7f, 0.4f), new GradientAlphaKey(0f, 1f) });
        couleur.color = degrade;
        var taille = ps.sizeOverLifetime;
        taille.enabled = true;
        taille.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
        var bruit = ps.noise;
        bruit.enabled = true;
        bruit.strength = 0.3f;
        bruit.frequency = 1.5f;
        var rendu2 = tf.GetComponent<ParticleSystemRenderer>();
        rendu2.sharedMaterial = MateriauFlamme();
        rendu2.renderMode = ParticleSystemRenderMode.Billboard;
        rendu2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Lueur orange qui suit la bille
        Transform tl = bille.transform.Find("Lueur_Feu");
        if (tl == null)
        {
            tl = new GameObject("Lueur_Feu").transform;
            Undo.RegisterCreatedObjectUndo(tl.gameObject, tl.name);
            tl.SetParent(bille.transform, false);
        }
        var lueur = ObtenirOuAjouter<Light>(tl.gameObject);
        lueur.type = LightType.Point;
        lueur.color = new Color(1f, 0.5f, 0.15f);
        lueur.range = 2.2f;
        lueur.intensity = 0f;
        lueur.enabled = false;
        lueur.shadows = LightShadows.None;

        var feu = ObtenirOuAjouter<BilleEnFeu>(bille);
        feu.flammes = ps;
        feu.lueur = lueur;
        EditorUtility.SetDirty(feu);
        rapport.AppendLine($"- Bille en feu installée : elle commence à chauffer après {feu.debut:0} s en jeu, " +
                           $"feu complet après {feu.debut + feu.montee:0} s. Tout repart de zéro à chaque bille.");
    }

    // ==================================================================== PASTILLES AUTOUR DE LA RAMPE

    static void PastillesAutourRampe(StringBuilder rapport, StringBuilder aVerifier)
    {
        GameObject groupe = TrouverParNom("Bonus_Centre");
        Transform g = TrouverParNom("Entonnoir_1_G")?.transform, d = TrouverParNom("Entonnoir_1_D")?.transform;
        if (groupe == null || g == null || d == null)
        {
            aVerifier.AppendLine("- Bonus_Centre ou entrée de la rampe du milieu (Entonnoir_1_G/D) introuvable.");
            return;
        }
        var pastilles = groupe.GetComponentsInChildren<CibleBonus>(true).Select(c => c.transform).OrderBy(t => t.position.x).ToList();
        if (pastilles.Count < 3) { aVerifier.AppendLine("- Moins de 3 pastilles dans Bonus_Centre."); return; }
        Physics.SyncTransforms();

        Vector3 milieu = (g.position + d.position) * 0.5f;
        if (!SurfacePlateau(milieu.x, milieu.z, out Vector3 sol, out Vector3 normale)) return;
        Vector3 hautTable = Vector3.ProjectOnPlane(Vector3.forward, normale).normalized;
        Vector3 droite = Vector3.Cross(normale, hautTable);
        float demiEcart = Vector3.Dot(d.position - g.position, droite) * 0.5f;

        // Gauche, devant (vers les flippers, après l'entonnoir), droite ; si la place est prise, on s'écarte un peu
        var places = new (Transform pastille, Vector3 depart, Vector3 ecarter, string nom)[]
        {
            (pastilles[0], sol - droite * (demiEcart + 0.75f), -droite, "à gauche"),
            (pastilles[1], sol - hautTable * (LongueurEntonnoir * 0.5f + 0.9f), -hautTable, "devant"),
            (pastilles[2], sol + droite * (demiEcart + 0.75f), droite, "à droite"),
        };
        foreach (var p in places)
        {
            bool pose = false;
            for (float decalage = 0f; decalage <= 1.5f && !pose; decalage += 0.25f)
            {
                Vector3 cible = p.depart + p.ecarter * decalage;
                if (!SurfacePlateau(cible.x, cible.z, out Vector3 point, out Vector3 _)) continue;
                if (!PlaceLibre(point, groupe.transform, 0.4f)) continue;
                Undo.RecordObject(p.pastille, "Pastille");
                p.pastille.position = point;
                rapport.AppendLine($"- {p.pastille.name} : {p.nom} de l'entrée de la rampe ({point.x:F2}, {point.z:F2}).");
                pose = true;
            }
            if (!pose) aVerifier.AppendLine($"- {p.pastille.name} : pas de place libre {p.nom} de l'entrée de la rampe, laissée où elle était.");
        }
    }
}
