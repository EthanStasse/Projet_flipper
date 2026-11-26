using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Ampoules du plateau + nouveau modèle du lanceur (ressort).
// Menu "Flipper > Ampoules et lanceur" (aussi fait par Installer). On peut le relancer : il replace tout.
//
// Ampoules : modèles de Assets/flipper parts/Ampoules, rangées sous l'objet "Ampoules".
//   Chaque ampoule est posée sur la surface (trouvée par un rayon vers le bas), penchée comme la table,
//   avec une Point Light et le script Ampoule (fixe, clignote, chenillard ou vacille). Pas de collider.
// Lanceur : le modèle Assets/flipper parts/Lanceur/Ressort_Modele.fbx devient l'enfant de "ressort"
//   (il recule avec lui). La capsule reste, invisible : elle garde la collision et Script_ressort.
public static partial class InstallateurFlipper
{
    const string DossierAmpoules = "Assets/flipper parts/Ampoules";
    const string CheminModeleRessort = "Assets/flipper parts/Lanceur/Ressort_Modele.fbx";
    const string NomGroupeAmpoules = "Ampoules";

    static readonly Color CouleurAmpoule = new Color(1f, 0.75f, 0.4f);
    static readonly Color CouleurAmpouleRouge = new Color(1f, 0.25f, 0.1f);

    enum Surface { Plateau, Bumper }

    struct PlaceAmpoule
    {
        public string nom, modele;
        public float x, z;          // position sur la table (Y trouvé par rayon)
        public Surface surface;
        public string bumper;       // pour Surface.Bumper : nom de l'objet bumper
        public Ampoule.Mode mode;
        public bool rouge;
        public float periode;
    }

    static readonly PlaceAmpoule[] PlacesAmpoules =
    {
        // Dans les lance-pierres, sous le plastique rouge des murets
        new PlaceAmpoule { nom = "Ampoule_LancePierre_Droit",  modele = "Ampoule_Simple", x = -23.83f, z = -18.22f, mode = Ampoule.Mode.Vacille, rouge = true },
        new PlaceAmpoule { nom = "Ampoule_LancePierre_Gauche", modele = "Ampoule_Simple", x = -32.10f, z = -18.22f, mode = Ampoule.Mode.Vacille, rouge = true },
        // Couloirs de retour vers les flippers
        new PlaceAmpoule { nom = "Ampoule_Couloir_Droit",  modele = "Ampoule_Insert", x = -22.47f, z = -17.0f, mode = Ampoule.Mode.Fixe },
        new PlaceAmpoule { nom = "Ampoule_Couloir_Gauche", modele = "Ampoule_Insert", x = -33.46f, z = -17.0f, mode = Ampoule.Mode.Fixe },
        // "Rejouer" entre les flippers
        new PlaceAmpoule { nom = "Ampoule_Rejouer", modele = "Ampoule_Insert", x = -27.97f, z = -17.3f, mode = Ampoule.Mode.Clignote, periode = 1.2f },
        // Rangée de 3 dans le couloir du lanceur (chenillard qui monte)
        new PlaceAmpoule { nom = "Ampoule_Lanceur", modele = "Ampoule_Rangee", x = -21.24f, z = -11f, mode = Ampoule.Mode.Chenillard, periode = 0.9f },
        // Sur le chapeau des 3 bumpers
        new PlaceAmpoule { nom = "Ampoule_Bumper_1", modele = "Ampoule_Bumper", surface = Surface.Bumper, bumper = "Bumpers",     mode = Ampoule.Mode.Vacille },
        new PlaceAmpoule { nom = "Ampoule_Bumper_2", modele = "Ampoule_Bumper", surface = Surface.Bumper, bumper = "Bumpers (1)", mode = Ampoule.Mode.Vacille },
        new PlaceAmpoule { nom = "Ampoule_Bumper_3", modele = "Ampoule_Bumper", surface = Surface.Bumper, bumper = "Bumpers (2)", mode = Ampoule.Mode.Vacille },
    };

    [MenuItem("Flipper/Ampoules et lanceur", priority = 5)]
    public static void MenuAmpoulesEtLanceur()
    {
        var rapport = new StringBuilder("Flipper > Ampoules et lanceur :\n");
        var aVerifier = new StringBuilder();
        Undo.SetCurrentGroupName("Ampoules et lanceur");
        int groupe = Undo.GetCurrentGroup();
        HabillerLanceur(rapport, aVerifier);
        PoserAmpoules(rapport, aVerifier);
        Undo.CollapseUndoOperations(groupe);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        if (aVerifier.Length > 0) rapport.Append("\nAttention :\n").Append(aVerifier);
        Debug.Log(rapport.ToString());
    }

    // ==================================================================== LANCEUR

    static void HabillerLanceur(StringBuilder rapport, StringBuilder aVerifier)
    {
        GameObject ressort = TrouverParNom("ressort");
        if (ressort == null)
        {
            aVerifier.AppendLine("- Objet \"ressort\" introuvable.");
            return;
        }

        Material metal = AssetDatabase.LoadAssetAtPath<Material>($"{DossierMateriaux}/Metal_Brosse.mat");
        Material bois = AssetDatabase.LoadAssetAtPath<Material>($"{DossierMateriaux}/Murets/Muret_Bois.mat");
        Material braise = AssetDatabase.LoadAssetAtPath<Material>($"{DossierMateriaux}/Murets/Muret_Braise.mat");
        var mats = new Dictionary<string, Material>
        {
            { "Caoutchouc", MateriauCouleur($"{DossierMateriaux}/Lanceur/Lanceur_Caoutchouc.mat", new Color(0.05f, 0.05f, 0.05f), Color.black, 0.3f) },
            { "Ressort", MateriauChrome($"{DossierMateriaux}/Lanceur/Lanceur_Ressort.mat") },
        };
        if (metal != null) mats["Metal_Tige"] = metal;
        if (bois != null) mats["Bois_Pommeau"] = bois;
        if (braise != null) mats["Braise"] = braise;
        RemplacerMateriauxFbx(CheminModeleRessort, mats);   // avant de charger : le réimport remplace l'asset
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CheminModeleRessort);
        if (fbx == null)
        {
            aVerifier.AppendLine($"- Modèle {CheminModeleRessort} introuvable.");
            return;
        }

        // La capsule reste (collision + script) mais ne se voit plus
        var rendu = ressort.GetComponent<Renderer>();
        if (rendu != null && rendu.enabled)
        {
            Undo.RecordObject(rendu, "Cacher ressort");
            rendu.enabled = false;
        }

        const string nom = "Modele_Ressort";
        Transform existant = ressort.transform.Find(nom);
        GameObject modele;
        if (existant != null) modele = existant.gameObject;
        else
        {
            modele = (GameObject)PrefabUtility.InstantiatePrefab(fbx, ressort.transform);
            modele.name = nom;
            Undo.RegisterCreatedObjectUndo(modele, nom);
        }
        // La capsule est étirée (0.2 x 0.8 x 0.2) : l'enfant reçoit l'échelle inverse pour garder sa vraie taille.
        // Sans rotation propre, l'échelle inverse ne déforme rien. Pointe du modèle en +Y = vers la bille.
        Vector3 e = ressort.transform.localScale;
        Undo.RecordObject(modele.transform, "Placer ressort");
        modele.transform.localPosition = Vector3.zero;
        modele.transform.localRotation = Quaternion.identity;
        modele.transform.localScale = new Vector3(1f / e.x, 1f / e.y, 1f / e.z);
        rapport.AppendLine("- Lanceur : nouveau modèle posé dans \"ressort\" (il recule avec lui ; capsule cachée, collision gardée).");
    }

    // ==================================================================== AMPOULES

    static void PoserAmpoules(StringBuilder rapport, StringBuilder aVerifier)
    {
        Material verre = MateriauVerreAmpoule($"{DossierMateriaux}/Ampoules/Ampoule_Verre.mat", CouleurAmpoule);
        Material verreRouge = MateriauVerreAmpoule($"{DossierMateriaux}/Ampoules/Ampoule_Verre_Rouge.mat", CouleurAmpouleRouge);
        Material metal = AssetDatabase.LoadAssetAtPath<Material>($"{DossierMateriaux}/Metal_Brosse.mat");
        var mats = new Dictionary<string, Material>
        {
            { "Verre_Ampoule", verre },
            { "Reflecteur", MateriauChrome($"{DossierMateriaux}/Ampoules/Ampoule_Reflecteur.mat") },
        };
        if (metal != null) mats["Metal_Douille"] = metal;

        Physics.SyncTransforms();
        Transform groupe = Groupe(NomGroupeAmpoules, null);
        int nb = 0;
        foreach (PlaceAmpoule p in PlacesAmpoules)
        {
            string chemin = $"{DossierAmpoules}/{p.modele}.fbx";
            RemplacerMateriauxFbx(chemin, mats);   // avant de charger : le réimport remplace l'asset
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(chemin);
            if (fbx == null)
            {
                aVerifier.AppendLine($"- Modèle {chemin} introuvable : {p.nom} non posée.");
                continue;
            }

            if (!TrouverSurfaceAmpoule(p, out Vector3 point, out Vector3 normale, aVerifier))
                continue;

            Transform existant = groupe.Find(p.nom);
            GameObject go;
            if (existant != null) go = existant.gameObject;
            else
            {
                go = (GameObject)PrefabUtility.InstantiatePrefab(fbx, groupe);
                go.name = p.nom;
                Undo.RegisterCreatedObjectUndo(go, p.nom);
            }
            // Debout sur la surface, le "devant" du modèle (+Z) vers le haut de la table
            Vector3 hautTable = Vector3.ProjectOnPlane(Vector3.forward, normale).normalized;
            Undo.RecordObject(go.transform, "Placer ampoule");
            go.transform.SetPositionAndRotation(point, Quaternion.LookRotation(hautTable, normale));
            go.transform.localScale = Vector3.one;

            // Chaque morceau de verre (1, ou 3 pour la rangée) reçoit sa lumière et son script
            var rendus = go.GetComponentsInChildren<Renderer>(true)
                           .OrderBy(r => go.transform.InverseTransformPoint(r.bounds.center).z).ToArray();   // du bas vers le haut
            for (int i = 0; i < rendus.Length; i++)
            {
                Renderer r = rendus[i];
                if (p.rouge)
                {
                    Material[] m = r.sharedMaterials;
                    for (int j = 0; j < m.Length; j++) if (m[j] == verre) m[j] = verreRouge;
                    Undo.RecordObject(r, "Ampoule rouge");
                    r.sharedMaterials = m;
                }
                ConfigurerAmpoule(r.gameObject, p, rendus.Length > 1 ? (float)i / rendus.Length : 0f);
            }
            nb++;
        }
        rapport.AppendLine($"- {nb} ampoule(s) posée(s) sous \"{NomGroupeAmpoules}\" (lance-pierres, couloirs, rejouer, lanceur, bumpers).");
    }

    static bool TrouverSurfaceAmpoule(PlaceAmpoule p, out Vector3 point, out Vector3 normale, StringBuilder aVerifier)
    {
        point = default;
        normale = Vector3.up;
        Vector3 depart;
        float hautDepart = 60f;   // au-dessus de tout (la vitre est ignorée)
        System.Func<Collider, bool> accepte;
        if (p.surface == Surface.Bumper)
        {
            GameObject bumper = TrouverParNom(p.bumper);
            Renderer chapeau = bumper?.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name.StartsWith("Bumper Cap"));
            if (chapeau == null)
            {
                aVerifier.AppendLine($"- {p.nom} : chapeau du bumper \"{p.bumper}\" introuvable.");
                return false;
            }
            depart = chapeau.bounds.center;
            // Juste au-dessus du chapeau : en haut de la table, le fronton de la machine surplombe les bumpers
            hautDepart = chapeau.bounds.max.y + 1f;
            accepte = c => c.transform.IsChildOf(bumper.transform) && c.name.StartsWith("Bumper Cap");
        }
        else
        {
            depart = new Vector3(p.x, 0f, p.z);
            accepte = c => c.name == "Playfield";
        }

        var hits = Physics.RaycastAll(new Vector3(depart.x, hautDepart, depart.z), Vector3.down, hautDepart + 60f, ~0, QueryTriggerInteraction.Ignore)
                          .OrderBy(h => h.distance).ToArray();
        // Ce qu'il y a au-dessus de la surface voulue (hors vitre) : un mur qui gênerait l'ampoule
        foreach (RaycastHit h in hits)
        {
            if (accepte(h.collider))
            {
                point = h.point;
                normale = h.normal;
                return true;
            }
            if (h.collider.name != "Vitre")
            {
                aVerifier.AppendLine($"- {p.nom} : \"{h.collider.name}\" est au-dessus de cet endroit, ampoule non posée.");
                return false;
            }
        }
        aVerifier.AppendLine($"- {p.nom} : surface introuvable sous ({depart.x:F2}, {depart.z:F2}).");
        return false;
    }

    static void ConfigurerAmpoule(GameObject go, PlaceAmpoule p, float decalage)
    {
        Color couleur = p.rouge ? CouleurAmpouleRouge : CouleurAmpoule;
        Ampoule a = ObtenirOuAjouter<Ampoule>(go);
        a.mode = p.mode;
        a.couleur = couleur;
        a.periode = p.periode > 0f ? p.periode : 1f;
        a.decalage = 1f - decalage;   // le chenillard monte : celle du bas s'allume d'abord
        a.intensiteLumiere = p.modele == "Ampoule_Bumper" ? 3f : 2f;
        EditorUtility.SetDirty(a);

        // Lumière juste au-dessus du verre (hauteur du modèle + un peu)
        Transform t = go.transform.Find("Lumiere");
        Light l;
        if (t == null)
        {
            var lgo = new GameObject("Lumiere");
            Undo.RegisterCreatedObjectUndo(lgo, "Lumiere");
            lgo.transform.SetParent(go.transform, false);
            l = lgo.AddComponent<Light>();
        }
        else l = ObtenirOuAjouter<Light>(t.gameObject);
        float haut = go.GetComponent<Renderer>().bounds.size.y;
        Undo.RecordObject(l.transform, "Lumiere");
        l.transform.localPosition = Vector3.up * (haut + 0.12f);
        l.type = LightType.Point;
        l.color = couleur;
        l.range = p.modele == "Ampoule_Bumper" ? 1.8f : 1.3f;
        l.intensity = a.intensiteLumiere;
        l.shadows = LightShadows.None;
        EditorUtility.SetDirty(l);
    }

    // ==================================================================== MATÉRIAUX

    static Material MateriauVerreAmpoule(string chemin, Color couleur)
    {
        Material m = MateriauCouleur(chemin, Color.Lerp(couleur, Color.white, 0.5f), couleur * 3f, 0.9f);
        return m;
    }

    static Material MateriauChrome(string chemin)
    {
        Material m = MateriauCouleur(chemin, new Color(0.85f, 0.85f, 0.88f), Color.black, 0.9f);
        m.SetFloat("_Metallic", 1f);
        EditorUtility.SetDirty(m);
        return m;
    }
}
