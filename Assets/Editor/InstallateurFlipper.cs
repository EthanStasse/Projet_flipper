using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Menu "Flipper > Installer" : met en place automatiquement dans la scène ouverte
//  - les collisions (plateau, murs, bumpers, poteaux, slingshot, flippers) + matériaux physiques,
//  - les flippers 3D branchés sur flipper_gauche / flipper_droite (script Flipper),
//  - la bille posée sur le plateau et le ressort placé derrière elle,
//  - le GameManager, la zone du lanceur, la zone de drain et les composants des bumpers,
//  - les rails (objets "*_Collision" : collider invisible + BoostRail) et la vitre au-dessus,
//  - les pastilles de bonus au sol et les textures des murs.
// On peut le relancer sans risque : il réutilise les objets déjà créés (même nom).
// Tout est annulable avec Ctrl+Z.
public static partial class InstallateurFlipper
{
    const string NomZoneLanceur = "Zone_Lanceur";
    const string NomZoneDrain = "Zone_Drain";
    const string NomDepartBille = "Depart_Bille";
    const string NomGameManager = "GameManager";
    const string DossierPhysique = "Assets/Physique";
    const int PointsRabatoires = 10;
    const int PointsSlingshot = 10;

    // Type de collider à mettre sur un mesh
    enum Categorie { Aucune, PlateauStatique, PlateauConvexe, CaoutchoucConvexe, RailInvisible }

    // ==================================================================== MENU : INSTALLER

    [MenuItem("Flipper/Installer", priority = 0)]
    public static void Installer()
    {
        var rapport = new StringBuilder("Flipper > Installer :\n");
        var aVerifier = new StringBuilder();

        Undo.SetCurrentGroupName("Installer le flipper");
        int groupeUndo = Undo.GetCurrentGroup();

        // ---------------------------------------------------------------- 1. La bille
        GameObject bille = TrouverBille();
        if (bille == null)
        {
            EditorUtility.DisplayDialog("Flipper", "Bille introuvable : il faut un objet avec le tag \"Ball\" ou nommé \"la_bille\".", "OK");
            return;
        }
        if (!bille.CompareTag(Bille.Tag))
        {
            Undo.RecordObject(bille, "Tag bille");
            bille.tag = Bille.Tag;
            rapport.AppendLine("- Tag \"Ball\" mis sur la bille.");
        }

        // ---------------------------------------------------------------- 2. Collisions partout
        AjouterCollisions(rapport);
        Rigidbody rbBille = bille.GetComponent<Rigidbody>();
        float diametre = DiametreBille(bille);
        float rayon = diametre * 0.5f;

        // ---------------------------------------------------------------- 3. Orientation de la table
        Physics.SyncTransforms();
        bool surPlateau = RaycastSousBille(bille, out RaycastHit sol);
        Vector3 normale = surPlateau ? sol.normal : NormaleFlippers();
        Vector3 hautTable = Vector3.ProjectOnPlane(Vector3.forward, normale).normalized;
        if (hautTable.sqrMagnitude < 0.01f) hautTable = Vector3.forward;
        Quaternion rotationTable = Quaternion.LookRotation(hautTable, normale);

        // ---------------------------------------------------------------- 4. Bille posée sur le plateau
        Vector3 ancienneBille = bille.transform.position;
        if (surPlateau)
        {
            // On ne déplace la bille que perpendiculairement à la table (sinon elle dérive à chaque installation)
            float hauteurActuelle = Vector3.Dot(ancienneBille - sol.point, normale);
            Vector3 posee = ancienneBille - normale * (hauteurActuelle - (rayon + 0.005f));
            if (Vector3.Distance(posee, ancienneBille) > 0.01f)
            {
                Undo.RecordObject(bille.transform, "Poser la bille");
                bille.transform.position = posee;
                rapport.AppendLine("- Bille posée à la surface du plateau.");
            }
        }
        else aVerifier.AppendLine("- Pas de plateau trouvé sous la bille : vérifiez qu'elle est bien au-dessus de la table.");

        // ---------------------------------------------------------------- 5. Flippers 3D branchés sur les scripts
        BrancherFlippers(normale, hautTable, rapport, aVerifier);

        // ---------------------------------------------------------------- 6. GameManager + point de départ
        GameManager gm = Object.FindAnyObjectByType<GameManager>();
        if (gm == null)
        {
            var go = new GameObject(NomGameManager);
            Undo.RegisterCreatedObjectUndo(go, "GameManager");
            gm = Undo.AddComponent<GameManager>(go);
            rapport.AppendLine("- GameManager créé.");
        }

        GameObject depart = TrouverOuCreer(NomDepartBille, bille.transform.position, bille.transform.rotation, out bool departCree);
        if (!departCree && Vector3.Distance(depart.transform.position, ancienneBille) < 1f)
        {
            Undo.RecordObject(depart.transform, "Depart_Bille");
            depart.transform.position = bille.transform.position;
        }

        Undo.RecordObject(gm, "GameManager");
        gm.bille = rbBille;
        gm.pointDeDepart = depart.transform;
        gm.billesParPartie = 3;
        gm.delaiRemiseEnJeu = 1f;
        if (gm.hauteurPerte > 3f) gm.hauteurPerte = 3f;   // bille tombée dans la caisse = perdue
        EditorUtility.SetDirty(gm);

        // ---------------------------------------------------------------- 7. Ressort placé derrière la bille
        GameObject ressort = TrouverParNom("ressort");
        if (ressort != null)
        {
            PlacerRessort(ressort, depart.transform.position, rayon, normale, hautTable, rapport);
            DegagerBilleDuRessort(bille, depart, ressort, rayon, hautTable, rapport);

            Rigidbody rbRessort = ObtenirOuAjouter<Rigidbody>(ressort);
            rbRessort.isKinematic = true;
            rbRessort.useGravity = false;
        }
        else aVerifier.AppendLine("- Objet \"ressort\" introuvable : ajoutez Script_ressort sur votre lanceur.");

        // ---------------------------------------------------------------- 8. Zone du lanceur
        GameObject zoneGo = TrouverOuCreer(NomZoneLanceur, depart.transform.position, rotationTable, out bool zoneCreee);
        BoxCollider boiteLanceur = ObtenirOuAjouter<BoxCollider>(zoneGo);
        boiteLanceur.isTrigger = true;
        if (zoneCreee || Vector3.Distance(zoneGo.transform.position, depart.transform.position) > 3f)
        {
            Undo.RecordObject(zoneGo.transform, "Zone_Lanceur");
            zoneGo.transform.SetPositionAndRotation(depart.transform.position, rotationTable);
            boiteLanceur.center = Vector3.zero;
            boiteLanceur.size = new Vector3(diametre * 1.8f, diametre * 2f, diametre * 4f);
            rapport.AppendLine("- Zone_Lanceur placée autour de la bille (flèche jaune = direction du tir).");
        }
        ZoneLanceur zoneLanceur = ObtenirOuAjouter<ZoneLanceur>(zoneGo);

        if (ressort != null)
        {
            Script_ressortBrule script = ObtenirOuAjouter<Script_ressortBrule>(ressort);
            script.zoneLanceur = zoneLanceur;
            script.bille = rbBille;
            if (script.audioSource == null)
                script.audioSource = ressort.GetComponent<AudioSource>();
            // Anciennes valeurs par défaut (trop faibles pour cette table) remplacées
            if (Mathf.Approximately(script.vitesseMin, 6f) && Mathf.Approximately(script.vitesseMax, 18f))
            {
                script.vitesseMin = 13f;
                script.vitesseMax = 20f;
            }
            EditorUtility.SetDirty(script);
        }

        // ---------------------------------------------------------------- 8 bis. Courbe en haut du couloir + entrées des rails + vitre
        ConstruireGuideHaut(bille, rayon, normale, hautTable, rapport, aVerifier);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(CheminModeleGuide) != null)
            PoserCourbesHaut(rapport, aVerifier);   // le modèle 3D remplace l'arc de cubes, + courbe gauche
        ConstruireEntreesRails(depart.transform.position - normale * rayon, normale, hautTable, rapport);
        ConfigurerAiguillages(rapport);
        ConstruireVitre(depart.transform.position - normale * rayon, diametre, normale, hautTable, rapport);

        // Flippers trop rapides : à 1800°/s la bille part à ~26 m/s et s'envole hors de la machine
        foreach (GameObject go in TousLesObjets())
        {
            FlipperBrule f = go.GetComponent<FlipperBrule>();
            if (f == null || f.vitesseMontee <= 1000f) continue;
            Undo.RecordObject(f, "Vitesse flipper");
            f.vitesseMontee = 900f;
            EditorUtility.SetDirty(f);
            rapport.AppendLine($"- {go.name} : vitesse de levée réduite à 900°/s.");
        }

        // ---------------------------------------------------------------- 9. Zone de drain
        Transform gauche = TrouverParNom("flipper_gauche")?.transform;
        Transform droite = TrouverParNom("flipper_droite")?.transform;
        Vector3 posDrain;
        float largeurDrain;
        if (gauche != null && droite != null)
        {
            Vector3 milieu = (gauche.position + droite.position) * 0.5f;
            largeurDrain = Vector3.Distance(gauche.position, droite.position) + 8f;
            posDrain = milieu - hautTable * 1.8f;
        }
        else
        {
            largeurDrain = 8f;
            posDrain = depart.transform.position - hautTable * 3f;
            aVerifier.AppendLine("- flipper_gauche / flipper_droite introuvables : Zone_Drain à placer à la main.");
        }

        GameObject drain = TrouverOuCreer(NomZoneDrain, posDrain, rotationTable, out bool drainCree);
        BoxCollider boiteDrain = ObtenirOuAjouter<BoxCollider>(drain);
        boiteDrain.isTrigger = true;
        if (drainCree || Vector3.Distance(drain.transform.position, posDrain) > 6f)
        {
            Undo.RecordObject(drain.transform, "Zone_Drain");
            drain.transform.SetPositionAndRotation(posDrain, rotationTable);
            boiteDrain.center = Vector3.zero;
            boiteDrain.size = new Vector3(largeurDrain, 2f, 1.6f);
            rapport.AppendLine("- Zone_Drain placée sous les flippers.");
        }
        ObtenirOuAjouter<RetourDepartBrule>(drain);

        // ---------------------------------------------------------------- 10. Bumpers
        int nbBumpers = 0;
        foreach (GameObject bumper in TrouverRacines("bumper"))
        {
            // Rigidbody cinématique sur la racine : le choc est reçu par BumperEffect
            // même si les colliders sont sur des objets enfants.
            Rigidbody rbBumper = ObtenirOuAjouter<Rigidbody>(bumper);
            rbBumper.isKinematic = true;
            rbBumper.useGravity = false;

            BumperEffectBrule effet = ObtenirOuAjouter<BumperEffectBrule>(bumper);
            effet.points = 100;

            // BumperEffect compte déjà les points : pas de ScoreOnHit en plus (sinon points en double)
            foreach (ScoreOnHit doublon in bumper.GetComponentsInChildren<ScoreOnHit>(true))
                Undo.DestroyObjectImmediate(doublon);

            nbBumpers++;
        }
        if (nbBumpers > 0) rapport.AppendLine($"- {nbBumpers} bumper(s) : BumperEffect (100 pts).");
        else aVerifier.AppendLine("- Aucun bumper trouvé (objets dont le nom commence par \"Bumper\").");

        // ---------------------------------------------------------------- 11. Points sur les rabatoires et le slingshot
        foreach (GameObject go in TousLesObjets())
        {
            string nom = go.name.ToLower();
            int points;
            if (nom.StartsWith("rabatoire")) points = PointsRabatoires;
            else if (nom.Contains("rubber")) points = PointsSlingshot;
            else continue;

            if (go.GetComponent<Collider>() == null || go.GetComponent<ScoreOnHit>() != null)
                continue;
            ScoreOnHit s = Undo.AddComponent<ScoreOnHit>(go);
            s.points = points;
            rapport.AppendLine($"- ScoreOnHit ({points} pts) ajouté sur \"{go.name}\".");
        }

        // ---------------------------------------------------------------- 12. Pastilles de bonus au sol
        CreerGroupesBonus(depart.transform.position - normale * rayon, normale, hautTable, rapport, aVerifier);
        HabillerTousLesInserts(rapport);
        ConfigurerLueurs(rapport);

        // ---------------------------------------------------------------- 12 bis. Porte à sens unique en haut du couloir du lanceur
        ConstruirePorteLanceur(depart.transform.position, rayon, normale, hautTable, rapport, aVerifier);

        // ---------------------------------------------------------------- 13. Textures des murs + modèles 3D des murets
        AppliquerTexturesMurs(rapport, aVerifier);
        HabillerMurets(rapport, aVerifier);
        HabillerLanceur(rapport, aVerifier);
        PoserAmpoules(rapport, aVerifier);
        ConstruireEchelle(rapport, aVerifier);
        ConstruireHautPlateau(rapport, aVerifier);
        BoucherMuretsBas(rapport, aVerifier);
        HabillerMurets(rapport, aVerifier);
        PoserSecondeChance(rapport, aVerifier);
        PoserArbreMurGauche(rapport, aVerifier);
        InstallerBilleEnFeu(rapport);
        PastillesAutourRampe(rapport, aVerifier);
        ConstruireGardesRampe(rapport, aVerifier);
        PoserScoreBackglass(rapport, aVerifier);
        ConfigurerCameras(rapport, aVerifier);

        // ---------------------------------------------------------------- 14. Sons
        BrancherSons(rapport);

        // ---------------------------------------------------------------- Fin
        Undo.CollapseUndoOperations(groupeUndo);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.objects = new Object[] { zoneGo, drain };

        rapport.AppendLine("\nÀ faire : vérifier Zone_Lanceur (vert), Zone_Drain (rouge) et le ressort, puis Ctrl+S.");
        if (aVerifier.Length > 0) rapport.Append("\nAttention :\n").Append(aVerifier);
        Debug.Log(rapport.ToString());

        EditorUtility.DisplayDialog("Flipper",
            "Installation terminée.\n\n" +
            "1. Vérifiez Zone_Lanceur (boîte verte, flèche jaune = direction du tir) et le ressort derrière la bille.\n" +
            "2. Vérifiez Zone_Drain (boîte rouge, sous les flippers).\n" +
            "3. Sauvegardez la scène (Ctrl+S).\n\n" +
            (aVerifier.Length > 0 ? "Attention :\n" + aVerifier + "\n" : "") +
            "Détails dans la Console.", "OK");
    }

    // ==================================================================== MENU : COLLISIONS

    [MenuItem("Flipper/Ajouter les collisions", priority = 1)]
    public static void MenuAjouterCollisions()
    {
        var rapport = new StringBuilder("Flipper > Ajouter les collisions :\n");
        Undo.SetCurrentGroupName("Ajouter les collisions");
        int groupe = Undo.GetCurrentGroup();
        AjouterCollisions(rapport);
        Undo.CollapseUndoOperations(groupe);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log(rapport.ToString());
    }

    // Ajoute les colliders et les matériaux physiques sur tout ce que la bille doit toucher.
    static void AjouterCollisions(StringBuilder rapport)
    {
        PhysicsMaterial matBille = Materiau("Bille", 0.2f, 0.3f, PhysicsMaterialCombine.Average);
        PhysicsMaterial matPlateau = Materiau("Plateau", 0.1f, 0.3f, PhysicsMaterialCombine.Average);
        PhysicsMaterial matCaoutchouc = Materiau("Caoutchouc", 0.1f, 0.7f, PhysicsMaterialCombine.Maximum);
        PhysicsMaterial matRail = MateriauRail();

        // Les meshes FBX doivent être en Read/Write pour les MeshCollider (sinon problème dans un build)
        var chemins = new HashSet<string>();
        foreach (GameObject go in TousLesObjets())
        {
            var mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            Categorie cat = Classer(go);
            if (cat == Categorie.Aucune) continue;
            string chemin = AssetDatabase.GetAssetPath(mf.sharedMesh);
            if (AssetImporter.GetAtPath(chemin) is ModelImporter imp && !imp.isReadable)
                chemins.Add(chemin);
        }
        if (chemins.Count > 0)
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string chemin in chemins)
                {
                    var imp = (ModelImporter)AssetImporter.GetAtPath(chemin);
                    imp.isReadable = true;
                    imp.SaveAndReimport();
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            rapport.AppendLine($"- Read/Write activé sur {chemins.Count} modèle(s) FBX.");
        }

        int nbAjoutes = 0;
        foreach (GameObject go in TousLesObjets())
        {
            var mf = go.GetComponent<MeshFilter>();
            Categorie cat = Classer(go);

            if (cat != Categorie.Aucune && mf != null && mf.sharedMesh != null)
            {
                PhysicsMaterial mat = cat == Categorie.CaoutchoucConvexe ? matCaoutchouc
                                    : cat == Categorie.RailInvisible ? matRail : matPlateau;
                Collider col = go.GetComponent<Collider>();

                if (col == null)
                {
                    // MeshCollider = forme exacte du modèle (la surface de jeu est exactement celle qu'on voit)
                    var mc = Undo.AddComponent<MeshCollider>(go);
                    mc.sharedMesh = mf.sharedMesh;
                    mc.convex = EstConvexe(cat);
                    col = mc;
                    col.sharedMaterial = mat;
                    nbAjoutes++;
                }
                else
                {
                    Undo.RecordObject(col, "Collider");
                    if (col is MeshCollider mc)
                    {
                        if (mc.sharedMesh == null) mc.sharedMesh = mf.sharedMesh;
                        mc.convex = EstConvexe(cat);
                    }
                    if (col.sharedMaterial == null) col.sharedMaterial = mat;
                }

                if (cat == Categorie.RailInvisible)
                {
                    col.sharedMaterial = matRail;   // sans frottement : la bille ne freine pas contre les parois
                    // Mesh de collision du rail : invisible, et il accélère la bille (speed boost)
                    var rendu = go.GetComponent<Renderer>();
                    if (rendu != null && rendu.enabled)
                    {
                        Undo.RecordObject(rendu, "Cacher collision rail");
                        rendu.enabled = false;
                    }
                    BoostRail boost = go.GetComponent<BoostRail>();
                    if (boost == null)
                    {
                        Undo.AddComponent<BoostRail>(go);
                        rapport.AppendLine($"- Rail \"{go.name}\" : collision invisible + BoostRail (speed boost).");
                    }
                    else if (Mathf.Approximately(boost.vitesseMin, 10f) && Mathf.Approximately(boost.acceleration, 30f))
                    {
                        // Anciennes valeurs (première version) : remplacées par les valeurs testées
                        Undo.RecordObject(boost, "BoostRail");
                        boost.vitesseMin = 11f;
                        boost.vitesseMax = 13f;
                        boost.acceleration = 40f;
                        boost.adherence = 40f;
                        rapport.AppendLine($"- Rail \"{go.name}\" : réglages du boost mis à jour (11-13 m/s).");
                    }
                }
                continue;
            }

            // Objets qui ont déjà leur collider : on leur donne juste un matériau physique
            string nom = go.name.ToLower();
            Collider existant = go.GetComponent<Collider>();
            if (existant == null || existant.isTrigger || existant.sharedMaterial != null) continue;

            PhysicsMaterial m = null;
            if (Bille.Est(go)) m = matBille;
            else if (nom == "ressort" || nom.StartsWith("bordure") || nom.StartsWith("rabatoire") || nom == "batte" || nom.StartsWith("cran"))
                m = matPlateau;
            if (m != null)
            {
                Undo.RecordObject(existant, "Matériau physique");
                existant.sharedMaterial = m;
            }
        }
        rapport.AppendLine($"- {nbAjoutes} collider(s) ajouté(s) (plateau, murs, bumpers, poteaux, slingshot, flippers).");

        // Bille : détection continue pour ne pas traverser les murs à grande vitesse
        GameObject bille = TrouverBille();
        if (bille != null)
        {
            Rigidbody rb = ObtenirOuAjouter<Rigidbody>(bille);
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            if (bille.GetComponent<Collider>() == null)
                Undo.AddComponent<SphereCollider>(bille).sharedMaterial = matBille;
        }
    }

    // Décide quel collider mettre sur un objet qui a un mesh (d'après son nom et celui de ses parents).
    static Categorie Classer(GameObject go)
    {
        string nom = go.name.ToLower();
        // Rail : le mesh "..._Collision" sert de collision (invisible), le mesh visible n'en a pas besoin
        if (nom.EndsWith("_collision"))
            return Categorie.RailInvisible;
        if (nom == "aiguillage")
            return Categorie.PlateauConvexe;
        for (Transform t = go.transform; t != null; t = t.parent)
        {
            string n = t.name.ToLower();
            if (n == "pinballtable")
                return Categorie.PlateauStatique;
            if (n.StartsWith("bumper"))
                return nom.Contains("screw") ? Categorie.Aucune : Categorie.CaoutchoucConvexe;
            if (n.StartsWith("metal posts"))
                return Categorie.CaoutchoucConvexe;
            if (n.StartsWith("miscellaneous"))
                // Les plastiques des lance-pierres sont au-dessus de la bille : jamais de collision
                return nom.Contains("rubber") ? Categorie.CaoutchoucConvexe
                     : nom.Contains("plastic") ? Categorie.Aucune : Categorie.PlateauConvexe;
            if (n.StartsWith("flippers"))     // modèles 3D des flippers
                return Categorie.PlateauConvexe;
        }
        return Categorie.Aucune;
    }

    static bool EstConvexe(Categorie cat) => cat == Categorie.PlateauConvexe || cat == Categorie.CaoutchoucConvexe;

    // Intérieur des rails : ni frottement ni rebond (le couloir est à peine plus large que la bille)
    static PhysicsMaterial MateriauRail() =>
        Materiau("Rail", 0f, 0f, PhysicsMaterialCombine.Minimum, PhysicsMaterialCombine.Minimum);

    static PhysicsMaterial Materiau(string nom, float friction, float rebond, PhysicsMaterialCombine combineRebond,
                                    PhysicsMaterialCombine combineFriction = PhysicsMaterialCombine.Average)
    {
        if (!AssetDatabase.IsValidFolder(DossierPhysique))
            AssetDatabase.CreateFolder("Assets", "Physique");

        string chemin = $"{DossierPhysique}/{nom}.physicMaterial";
        var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(chemin);
        if (mat != null) return mat;

        mat = new PhysicsMaterial(nom)
        {
            dynamicFriction = friction,
            staticFriction = friction,
            bounciness = rebond,
            frictionCombine = combineFriction,
            bounceCombine = combineRebond
        };
        AssetDatabase.CreateAsset(mat, chemin);
        return mat;
    }

    // ==================================================================== FLIPPERS

    // Met chaque modèle 3D "Flippers" sous son pivot flipper_gauche / flipper_droite (script Flipper),
    // le pivot étant placé à l'origine du modèle (l'axe de rotation du flipper).
    static void BrancherFlippers(Vector3 normale, Vector3 hautTable, StringBuilder rapport, StringBuilder aVerifier)
    {
        List<GameObject> modeles = TousLesObjets()
            .Where(go => go.name.ToLower().StartsWith("flippers")
                      && go.GetComponent<MeshFilter>() != null
                      && go.GetComponentInParent<FlipperBrule>(true) == null)
            .ToList();

        if (modeles.Count == 0) return;   // déjà branchés (ou pas de modèles)
        if (modeles.Count != 2)
        {
            aVerifier.AppendLine($"- {modeles.Count} modèle(s) \"Flippers\" trouvé(s) au lieu de 2 : flippers non branchés automatiquement.");
            return;
        }

        // Gauche / droite vus par le joueur (qui regarde vers le haut de la table)
        Vector3 droiteJoueur = Vector3.Cross(normale, hautTable);
        modeles.Sort((a, b) => Vector3.Dot(a.transform.position, droiteJoueur).CompareTo(Vector3.Dot(b.transform.position, droiteJoueur)));

        Quaternion rotationPivot = Quaternion.LookRotation(hautTable, normale);
        BrancherUnFlipper("flipper_gauche", true, modeles[0], rotationPivot, rapport);
        BrancherUnFlipper("flipper_droite", false, modeles[1], rotationPivot, rapport);
    }

    static void BrancherUnFlipper(string nomPivot, bool estGauche, GameObject modele, Quaternion rotationPivot, StringBuilder rapport)
    {
        GameObject pivot = TrouverParNom(nomPivot);
        if (pivot == null)
        {
            pivot = new GameObject(nomPivot);
            Undo.RegisterCreatedObjectUndo(pivot, nomPivot);
        }

        // Les anciens enfants (ancienne "batte") sont désactivés, pas supprimés
        foreach (Transform enfant in pivot.transform)
        {
            if (!enfant.gameObject.activeSelf) continue;
            Undo.RecordObject(enfant.gameObject, "Désactiver ancienne batte");
            enfant.gameObject.SetActive(false);
            rapport.AppendLine($"- Ancien enfant \"{enfant.name}\" de {nomPivot} désactivé.");
        }

        Undo.RecordObject(pivot.transform, "Pivot flipper");
        pivot.transform.SetParent(null, true);
        pivot.transform.SetPositionAndRotation(modele.transform.position, rotationPivot);
        pivot.transform.localScale = Vector3.one;

        Undo.SetTransformParent(modele.transform, pivot.transform, "Brancher flipper");

        Rigidbody rb = ObtenirOuAjouter<Rigidbody>(pivot);
        rb.isKinematic = true;
        rb.useGravity = false;

        FlipperBrule flipper = ObtenirOuAjouter<FlipperBrule>(pivot);
        flipper.estGauche = estGauche;
        EditorUtility.SetDirty(flipper);

        rapport.AppendLine($"- Modèle \"{modele.name}\" branché sur {nomPivot} (pivot = origine du modèle).");
    }

    // ==================================================================== RESSORT

    // Si la bille (ou son point de départ) chevauche le ressort, on la replace juste devant le bout du ressort.
    static void DegagerBilleDuRessort(GameObject bille, GameObject depart, GameObject ressort, float rayon, Vector3 hautTable, StringBuilder rapport)
    {
        Physics.SyncTransforms();
        Collider colRessort = ressort.GetComponent<Collider>();
        if (colRessort == null) return;

        // Bout du ressort côté bille : point le plus "haut de la table" de son collider
        Bounds b = colRessort.bounds;
        float boutRessort = float.MinValue;
        for (int i = 0; i < 8; i++)
        {
            Vector3 coin = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
            Vector3 p = colRessort.ClosestPoint(coin + hautTable * 10f);   // point du ressort le plus loin vers le haut
            boutRessort = Mathf.Max(boutRessort, Vector3.Dot(p, hautTable));
        }

        foreach (GameObject go in new[] { bille, depart })
        {
            float ecart = Vector3.Dot(go.transform.position, hautTable) - boutRessort;
            if (ecart >= rayon) continue;
            Undo.RecordObject(go.transform, "Dégager la bille");
            go.transform.position += hautTable * (rayon + 0.02f - ecart);
            rapport.AppendLine($"- {go.name} était dans le ressort : replacé juste devant.");
        }
    }

    // Couche le ressort dans le couloir, juste derrière la bille, s'il en est loin.
    static void PlacerRessort(GameObject ressort, Vector3 positionBille, float rayon, Vector3 normale, Vector3 hautTable, StringBuilder rapport)
    {
        var rendu = ressort.GetComponent<Renderer>();
        Bounds limites = rendu != null ? rendu.bounds : new Bounds(ressort.transform.position, Vector3.zero);
        if (limites.SqrDistance(positionBille) < 1f)
            return; // déjà contre la bille : on ne touche pas

        var mf = ressort.GetComponent<MeshFilter>();
        float demiLongueur = mf != null && mf.sharedMesh != null
            ? mf.sharedMesh.bounds.extents.y * Mathf.Abs(ressort.transform.lossyScale.y)
            : 0.5f;

        Undo.RecordObject(ressort.transform, "Placer le ressort");
        // Axe Y du ressort (sa longueur) dans l'axe du couloir
        ressort.transform.rotation = Quaternion.LookRotation(-normale, hautTable);
        ressort.transform.position = positionBille - hautTable * (rayon + demiLongueur + 0.02f);
        rapport.AppendLine("- Ressort couché dans le couloir, juste derrière la bille.");
    }

    // ==================================================================== GUIDE COURBE

    const string NomGuideHaut = "Guide_Haut_Droite";
    const string NomMurFond = "Mur_Fond";
    const float RayonGuide = 2.5f;
    const int SegmentsGuide = 16;
    const string MarqueGuideV2 = "Segment_16";   // présent = arc déjà à la bonne version (16 segments sans frottement)

    // En haut de la table :
    //  - Mur_Fond : mur invisible au bout de la surface de jeu (bouche le trou entre le plateau et le mur du fond),
    //  - Guide_Haut_Droite : arc de cercle en haut du couloir du lanceur, qui renvoie la bille vers le plateau.
    // Arc et mur sont sans frottement (matériau Rail) : la bille garde son élan et part vers la gauche,
    // au lieu de perdre sa vitesse dans le coin et de retomber.
    // Les segments de l'arc sont des cubes : vous pouvez les remplacer par votre propre modèle.
    static void ConstruireGuideHaut(GameObject bille, float rayon, Vector3 normale, Vector3 hautTable, StringBuilder rapport, StringBuilder aVerifier)
    {
        // Ancien arc (8 segments avec frottement) : on le supprime AVANT de mesurer le couloir
        GameObject ancienGuide = TrouverParNom(NomGuideHaut);
        if (ancienGuide != null && ancienGuide.transform.Find(MarqueGuideV2) == null && !EstGuideModele(ancienGuide))
            Undo.DestroyObjectImmediate(ancienGuide);
        Physics.SyncTransforms();
        Vector3 depart = bille.transform.position;
        Vector3 droite = Vector3.Cross(normale, hautTable);
        Vector3 surface = depart - normale * rayon;   // point de la surface de jeu sous la bille

        // Bout de la surface de jeu : on avance dans le couloir tant qu'il y a un sol sous la bille
        float distFond = -1f;
        for (float d = 0f; d < 60f; d += 0.1f)
        {
            Vector3 p = surface + hautTable * d;
            bool sol = RaycastHorsBille(p + normale * 0.5f, -normale, 1f, out RaycastHit _);
            bool mur = RaycastHorsBille(p + normale * rayon, hautTable, 0.1f + rayon, out RaycastHit _);
            if (!sol || mur) { distFond = d; break; }
        }
        if (!RaycastHorsBille(depart, droite, 5f, out RaycastHit murDroit) || distFond < 1f)
        {
            aVerifier.AppendLine("- Bout du plateau ou mur droit du couloir introuvable : courbe du haut non créée.");
            return;
        }
        float distDroite = Vector3.Dot(murDroit.point - depart, droite);

        PhysicsMaterial mat = MateriauRail();   // sans frottement

        GameObject murExistant = TrouverParNom(NomMurFond);
        if (murExistant != null)
        {
            var col = murExistant.GetComponent<Collider>();
            if (col != null && col.sharedMaterial != mat)
            {
                Undo.RecordObject(col, "Mur_Fond");
                col.sharedMaterial = mat;
            }
        }
        else
        {
            var mur = new GameObject(NomMurFond);
            Undo.RegisterCreatedObjectUndo(mur, NomMurFond);
            mur.transform.SetPositionAndRotation(
                surface + hautTable * (distFond + 0.25f) + normale * 0.5f - droite * 8f,
                Quaternion.LookRotation(hautTable, normale));
            var box = mur.AddComponent<BoxCollider>();
            box.size = new Vector3(30f, 3f, 0.5f);   // large : il dépasse des côtés de la caisse, sans effet
            if (mat != null) box.sharedMaterial = mat;
            rapport.AppendLine("- Mur_Fond créé (invisible) : bouche le trou en haut du plateau.");
        }

        if (TrouverParNom(NomGuideHaut) != null) return;   // déjà construit

        // Centre de l'arc : tangent au mur droit du couloir et au mur du fond
        Vector3 centre = surface + droite * (distDroite - RayonGuide) + hautTable * (distFond - RayonGuide);

        var parent = new GameObject(NomGuideHaut);
        Undo.RegisterCreatedObjectUndo(parent, NomGuideHaut);
        parent.transform.position = centre;

        const float epaisseur = 0.2f, hauteur = 1f;
        float pas = 90f / SegmentsGuide;
        float longueur = 2f * (RayonGuide + epaisseur * 0.5f) * Mathf.Sin(pas * 0.5f * Mathf.Deg2Rad) + 0.05f;

        for (int i = 0; i < SegmentsGuide; i++)
        {
            float angle = (i + 0.5f) * pas * Mathf.Deg2Rad;      // 0° = côté couloir, 90° = côté mur du fond
            Vector3 radial = Mathf.Cos(angle) * droite + Mathf.Sin(angle) * hautTable;
            Vector3 tangente = -Mathf.Sin(angle) * droite + Mathf.Cos(angle) * hautTable;

            GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seg.name = $"Segment_{i + 1}";
            Undo.RegisterCreatedObjectUndo(seg, seg.name);
            seg.transform.SetParent(parent.transform, false);
            seg.transform.SetPositionAndRotation(
                centre + radial * (RayonGuide + epaisseur * 0.5f) + normale * (hauteur * 0.5f),
                Quaternion.LookRotation(tangente, normale));
            seg.transform.localScale = new Vector3(epaisseur, hauteur, longueur);
            if (mat != null) seg.GetComponent<Collider>().sharedMaterial = mat;
        }
        rapport.AppendLine("- Guide_Haut_Droite créé : courbe en haut du couloir qui renvoie la bille sur le plateau.");
    }

    // Vitre invisible au-dessus du plateau (comme la vitre d'un vrai flipper) : la bille ne peut pas s'envoler.
    // ==================================================================== ENTRÉES DES RAILS

    const float LongueurRampeAcces = 0.8f;
    const float LongueurEntonnoir = 1.2f;
    const float EvasementEntonnoir = 0.45f;

    // Pour chaque bout de rail posé au niveau du plateau :
    //  - une rampe d'accès invisible (le fond du rail commence un peu au-dessus du plateau : sans elle la bille "saute"),
    //  - un entonnoir invisible en V (le couloir est à peine plus large que la bille) si la bille peut arriver par là.
    static void ConstruireEntreesRails(Vector3 surface, Vector3 normale, Vector3 hautTable, StringBuilder rapport)
    {
        Physics.SyncTransforms();
        Vector3 droite = Vector3.Cross(normale, hautTable);
        PhysicsMaterial mat = MateriauRail();

        foreach (GameObject go in TousLesObjets())
        {
            if (go == null) continue;   // détruit pendant la boucle (anciennes pièces générées)
            if (Classer(go) != Categorie.RailInvisible) continue;
            var mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;

            // Pièces générées : on les reconstruit à chaque fois (au cas où le rail a bougé)
            string nomParent = $"Entrees_{go.name}";
            GameObject ancien = TrouverParNom(nomParent);
            if (ancien != null) Undo.DestroyObjectImmediate(ancien);

            // Sommets du rail tout près du plateau = bouts du rail au sol
            var bas = new List<Vector3>();
            foreach (Vector3 v in mf.sharedMesh.vertices)
            {
                Vector3 w = go.transform.TransformPoint(v);
                if (Vector3.Dot(w - surface, normale) < 0.15f) bas.Add(w);
            }
            if (bas.Count == 0) continue;

            var parent = new GameObject(nomParent);
            Undo.RegisterCreatedObjectUndo(parent, nomParent);
            int nb = 0;

            // Regroupe les sommets proches de proche en proche (un groupe = un bout de rail)
            while (bas.Count > 0)
            {
                var groupe = new List<Vector3> { bas[0] };
                bas.RemoveAt(0);
                for (int i = 0; i < groupe.Count; i++)
                {
                    Vector3 p0 = groupe[i];
                    groupe.AddRange(bas.Where(p => (p - p0).sqrMagnitude < 0.64f));
                    bas.RemoveAll(p => (p - p0).sqrMagnitude < 0.64f);
                }

                float uMin = groupe.Min(p => Vector3.Dot(p, droite)), uMax = groupe.Max(p => Vector3.Dot(p, droite));
                float vBout = groupe.Min(p => Vector3.Dot(p, hautTable));
                float hFond = groupe.Min(p => Vector3.Dot(p - surface, normale));
                Vector3 c = groupe.Aggregate(Vector3.zero, (a, p) => a + p) / groupe.Count;
                // Point du plateau au milieu de la bouche du rail
                Vector3 bouche = c + droite * ((uMin + uMax) * 0.5f - Vector3.Dot(c, droite))
                                   + hautTable * (vBout - Vector3.Dot(c, hautTable));
                bouche -= normale * Vector3.Dot(bouche - surface, normale);
                float largeur = uMax - uMin;
                nb++;

                // Entonnoir seulement si la bille peut arriver devant la bouche (sinon il gênerait le jeu)
                bool entonnoir = EmplacementLibre(bouche - hautTable * (LongueurEntonnoir + 0.3f), normale);

                // Rampe d'accès : du plateau jusqu'au fond du rail. Avec un entonnoir, elle couvre tout l'entonnoir
                // (sinon ses bords font une petite marche quand la bille arrive en biais).
                float longueurAcces = entonnoir ? LongueurEntonnoir : LongueurRampeAcces;
                float largeurAcces = entonnoir ? largeur + 2f * EvasementEntonnoir + 0.1f : largeur;
                Vector3 montee = (hautTable * longueurAcces + normale * hFond).normalized;
                Quaternion rot = Quaternion.LookRotation(montee, normale);
                var rampe = new GameObject($"Rampe_Acces_{nb}");
                Undo.RegisterCreatedObjectUndo(rampe, rampe.name);
                rampe.transform.SetParent(parent.transform, false);
                float longueur = Mathf.Sqrt(longueurAcces * longueurAcces + hFond * hFond);
                Vector3 milieuDessus = bouche - hautTable * (longueurAcces * 0.5f) + normale * (hFond * 0.5f);
                rampe.transform.SetPositionAndRotation(milieuDessus - (rot * Vector3.up) * 0.05f, rot);
                var boxRampe = rampe.AddComponent<BoxCollider>();
                boxRampe.size = new Vector3(largeurAcces, 0.1f, longueur + 0.02f);
                boxRampe.sharedMaterial = mat;

                if (!entonnoir) continue;

                foreach (float cote in new[] { -1f, 1f })
                {
                    Vector3 debut = bouche + droite * (cote * largeur * 0.5f);
                    Vector3 fin = debut - hautTable * LongueurEntonnoir + droite * (cote * EvasementEntonnoir);
                    var paroi = new GameObject($"Entonnoir_{nb}_{(cote < 0 ? "G" : "D")}");
                    Undo.RegisterCreatedObjectUndo(paroi, paroi.name);
                    paroi.transform.SetParent(parent.transform, false);
                    paroi.transform.SetPositionAndRotation((debut + fin) * 0.5f + normale * 0.3f, Quaternion.LookRotation(debut - fin, normale));
                    var boxParoi = paroi.AddComponent<BoxCollider>();
                    boxParoi.size = new Vector3(0.06f, 0.6f, (debut - fin).magnitude + 0.03f);
                    boxParoi.sharedMaterial = mat;
                }
            }
            rapport.AppendLine($"- {nomParent} : {nb} bout(s) de rail au sol équipé(s) (rampe d'accès + entonnoir).");
        }
    }

    // Aiguillage : la lame ne doit pas bloquer la bille (le couloir central est à peine plus large que la bille,
    // une lame au milieu le bouche). Son collider devient une zone de détection : la bille qui la traverse
    // est poussée vers la gauche ou vers la droite, en alternance (script Aiguillage).
    // Supprime aussi les anciens "Plafond" de rail (essai abandonné : ils coinçaient la bille dans les montées raides).
    static void ConfigurerAiguillages(StringBuilder rapport)
    {
        foreach (GameObject go in TousLesObjets())
        {
            if (go == null) continue;
            if (go.name == "Plafond" && go.transform.parent != null && Classer(go.transform.parent.gameObject) == Categorie.RailInvisible)
            {
                Undo.DestroyObjectImmediate(go);
                continue;
            }
            if (go.name.ToLower() != "aiguillage") continue;

            Rigidbody rb = ObtenirOuAjouter<Rigidbody>(go);
            rb.isKinematic = true;
            rb.useGravity = false;
            foreach (Collider col in go.GetComponents<Collider>())
            {
                Undo.RecordObject(col, "Aiguillage");
                col.isTrigger = true;
            }
            Aiguillage aiguillage = go.GetComponent<Aiguillage>();
            if (aiguillage == null)
            {
                Undo.AddComponent<Aiguillage>(go);
                rapport.AppendLine($"- {go.name} : aiguillage (une bille à gauche, la suivante à droite).");
            }
            else if (aiguillage.pousseeLaterale < 6f)
            {
                // Ancienne valeur (première version, trop faible pour décaler la bille)
                Undo.RecordObject(aiguillage, "Aiguillage");
                aiguillage.pousseeLaterale = 6f;
            }
        }
    }

    // Le dessous de la vitre est au moins à 2 diamètres de bille au-dessus du plateau,
    // et au-dessus du plus haut rail (+ une marge) pour que la bille puisse rouler dans les rails.
    static void ConstruireVitre(Vector3 surface, float diametre, Vector3 normale, Vector3 hautTable, StringBuilder rapport)
    {
        const string nom = "Vitre";
        const float epaisseur = 0.5f;

        float hautRails = HauteurMaxRails(surface, normale);
        float hauteur = Mathf.Max(diametre * 2f, hautRails + 0.3f);

        GameObject vitre = TrouverParNom(nom);
        if (vitre != null)
        {
            // Vitre existante : on la remonte seulement si elle est trop basse (on ne la descend jamais)
            float actuelle = Vector3.Dot(vitre.transform.position - surface, normale) - epaisseur * 0.5f;
            if (actuelle + 0.01f < hauteur)
            {
                Undo.RecordObject(vitre.transform, "Monter la vitre");
                vitre.transform.position += normale * (hauteur - actuelle);
                rapport.AppendLine($"- Vitre remontée à {hauteur:F2} au-dessus du plateau (au-dessus des rails).");
            }
            return;
        }

        // Centre : milieu du plateau s'il existe, sinon autour du point de départ
        Vector3 milieu = surface;
        GameObject plateau = TrouverParNom("Playfield");
        if (plateau != null && plateau.GetComponent<Renderer>() != null)
        {
            Vector3 c = plateau.GetComponent<Renderer>().bounds.center;
            milieu = c - normale * Vector3.Dot(c - surface, normale);   // projeté sur le plan de jeu
        }

        vitre = new GameObject(nom);
        Undo.RegisterCreatedObjectUndo(vitre, nom);
        vitre.transform.SetPositionAndRotation(
            milieu + normale * (hauteur + epaisseur * 0.5f),
            Quaternion.LookRotation(hautTable, normale));
        var box = vitre.AddComponent<BoxCollider>();
        box.size = new Vector3(30f, epaisseur, 40f);   // plus grande que la table : sans effet en dehors
        PhysicsMaterial mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>($"{DossierPhysique}/Plateau.physicMaterial");
        if (mat != null) box.sharedMaterial = mat;
        rapport.AppendLine($"- Vitre créée (invisible) à {hauteur:F2} au-dessus du plateau : la bille ne peut plus s'envoler.");
    }

    // Point le plus haut des rails (meshes "*_Collision") au-dessus du plan de jeu.
    static float HauteurMaxRails(Vector3 surface, Vector3 normale)
    {
        float max = 0f;
        foreach (GameObject go in TousLesObjets())
        {
            if (Classer(go) != Categorie.RailInvisible) continue;
            var mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
            foreach (Vector3 v in mf.sharedMesh.vertices)
                max = Mathf.Max(max, Vector3.Dot(go.transform.TransformPoint(v) - surface, normale));
        }
        return max;
    }

    static bool RaycastHorsBille(Vector3 origine, Vector3 direction, float distance, out RaycastHit resultat)
    {
        resultat = default;
        foreach (RaycastHit hit in Physics.RaycastAll(origine, direction, distance, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
        {
            if (Bille.Est(hit.collider.gameObject)) continue;
            if (hit.collider.attachedRigidbody != null && !hit.collider.attachedRigidbody.isKinematic) continue;
            resultat = hit;
            return true;
        }
        return false;
    }

    // ==================================================================== PASTILLES DE BONUS

    const string DossierMateriaux = "Assets/Materiaux";
    const string DossierTextures = "Assets/Textures";
    const float EcartPastilles = 1.3f;

    // Deux groupes de 3 pastilles (rollovers) placés automatiquement dans des zones libres du plateau :
    //  - Bonus_Haut : en haut du plateau (la bille y passe en sortant du lanceur),
    //  - Bonus_Centre : au milieu du plateau.
    static void CreerGroupesBonus(Vector3 surface, Vector3 normale, Vector3 hautTable, StringBuilder rapport, StringBuilder aVerifier)
    {
        Physics.SyncTransforms();
        Vector3 droite = Vector3.Cross(normale, hautTable);

        // Repère : milieu du plateau (u = gauche/droite, v = bas/haut de la table)
        Vector3 centre = surface;
        GameObject plateau = TrouverParNom("Playfield");
        if (plateau != null && plateau.GetComponent<Renderer>() != null)
        {
            Vector3 c = plateau.GetComponent<Renderer>().bounds.center;
            centre = c - normale * Vector3.Dot(c - surface, normale);
        }
        // Haut du plateau : face avant de Mur_Fond si elle existe
        float vHaut = 10f;
        GameObject murFond = TrouverParNom(NomMurFond);
        if (murFond != null)
            vHaut = Vector3.Dot(murFond.transform.position - centre, hautTable) - 0.25f;

        CreerGroupe("Bonus_Haut", 5000, centre, droite, normale, hautTable, vHaut - 1.6f, vHaut - 5f, rapport, aVerifier);
        CreerGroupe("Bonus_Centre", 3000, centre, droite, normale, hautTable, 1f, -4f, rapport, aVerifier);
    }

    // Cherche une rangée de 3 emplacements libres entre vDebut et vFin (en descendant), puis crée le groupe.
    static void CreerGroupe(string nom, int bonus, Vector3 centre, Vector3 droite, Vector3 normale, Vector3 hautTable,
                            float vDebut, float vFin, StringBuilder rapport, StringBuilder aVerifier)
    {
        if (TrouverParNom(nom) != null) return;

        for (float v = vDebut; v >= vFin; v -= 0.25f)
        {
            foreach (float u in new[] { 0f, -0.5f, 0.5f, -1f, 1f, -1.5f, 1.5f, -2f, 2f, -2.5f, 2.5f, -3f })
            {
                Vector3 milieu = centre + droite * u + hautTable * v;
                Vector3[] points =
                {
                    milieu - droite * EcartPastilles,
                    milieu,
                    milieu + droite * EcartPastilles
                };
                if (!points.All(p => EmplacementLibre(p, normale))) continue;

                var groupe = new GameObject(nom);
                Undo.RegisterCreatedObjectUndo(groupe, nom);
                groupe.transform.SetPositionAndRotation(milieu, Quaternion.LookRotation(hautTable, normale));
                var g = groupe.AddComponent<GroupeBonus>();
                g.bonusComplet = bonus;

                for (int i = 0; i < points.Length; i++)
                    CreerPastille($"Pastille_{i + 1}", groupe.transform, points[i]);

                rapport.AppendLine($"- {nom} : 3 pastilles de bonus (250 pts chacune, {bonus} quand les 3 sont allumées).");
                return;
            }
        }
        aVerifier.AppendLine($"- {nom} : pas de place libre trouvée sur le plateau, groupe non créé.");
    }

    static void CreerPastille(string nom, Transform parent, Vector3 position)
    {
        var cible = new GameObject(nom);
        Undo.RegisterCreatedObjectUndo(cible, nom);
        cible.transform.SetParent(parent, false);
        cible.transform.SetPositionAndRotation(position, parent.rotation);

        var zone = cible.AddComponent<BoxCollider>();
        zone.isTrigger = true;
        HabillerInsert(cible.AddComponent<CibleBonus>());
    }

    // ==================================================================== INSERTS (style lentilles de flipper)

    // Groupe du centre : flèches violettes (pointe rouge) ; autres groupes : ronds jaunes "BONUS".
    static bool EstFleche(CibleBonus cible)
    {
        Transform groupe = cible.transform.parent;
        return groupe != null && groupe.name == "Bonus_Centre";
    }

    static void HabillerTousLesInserts(StringBuilder rapport)
    {
        int nb = 0;
        foreach (GameObject go in TousLesObjets())
        {
            if (go == null) continue;
            CibleBonus cible = go.GetComponent<CibleBonus>();
            if (cible == null) continue;
            if (HabillerInsert(cible)) nb++;
            if (cible.intensiteAllumee > 2f)   // ancienne valeur : l'insert allumé virait au blanc avec le bloom
            {
                Undo.RecordObject(cible, "Insert");
                cible.intensiteAllumee = 1.4f;
            }
        }
        if (nb > 0) rapport.AppendLine($"- {nb} insert(s) de bonus au style flipper (lentille + lumière).");
    }

    // Remplace l'ancien disque par une lentille plate découpée (flèche ou rond) + une lumière.
    // Renvoie vrai si quelque chose a changé.
    static bool HabillerInsert(CibleBonus cible)
    {
        Transform t = cible.transform;
        if (t.Find("Insert") != null && t.Find("Lumiere") != null) return false;

        Transform ancien = t.Find("Disque");
        if (ancien != null)
        {
            // Si l'ancien disque a été déplacé à la main, la pastille entière (détection + insert) va à cet endroit
            if (ancien.localPosition.sqrMagnitude > 0.05f * 0.05f)
            {
                Undo.RecordObject(t, "Insert");
                t.position = ancien.position - t.up * Vector3.Dot(ancien.position - t.position, t.up);
            }
            Undo.DestroyObjectImmediate(ancien.gameObject);
        }

        bool fleche = EstFleche(cible);
        Color couleur = fleche ? new Color(0.75f, 0.45f, 1f) : new Color(1f, 0.8f, 0.25f);

        Undo.RecordObject(cible, "Insert");
        cible.couleurAllumee = couleur;

        // Zone de détection adaptée à la forme
        var zone = cible.GetComponent<BoxCollider>();
        if (zone != null)
        {
            Undo.RecordObject(zone, "Insert");
            zone.center = new Vector3(0f, 0.35f, 0f);
            zone.size = fleche ? new Vector3(0.6f, 0.7f, 1.0f) : new Vector3(0.7f, 0.7f, 0.7f);
        }

        // Lentille : quad posé à plat (sans collider : la bille roule dessus), pointe vers le haut de la table
        if (t.Find("Insert") == null)
        {
            GameObject lentille = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(lentille.GetComponent<Collider>());
            lentille.name = "Insert";
            Undo.RegisterCreatedObjectUndo(lentille, "Insert");
            lentille.transform.SetParent(t, false);
            lentille.transform.localPosition = new Vector3(0f, 0.012f, 0f);
            lentille.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            lentille.transform.localScale = fleche ? new Vector3(0.5f, 1.0f, 1f) : new Vector3(0.62f, 0.62f, 1f);
            var r = lentille.GetComponent<MeshRenderer>();
            r.sharedMaterial = MateriauInsert(fleche ? "Insert_Fleche" : "Insert_Rond");
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // Lumière juste au-dessus de l'insert : éclaire le plateau autour quand il est allumé
        if (t.Find("Lumiere") == null)
        {
            var go = new GameObject("Lumiere");
            Undo.RegisterCreatedObjectUndo(go, "Lumiere");
            go.transform.SetParent(t, false);
            go.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 1.4f;
            l.intensity = 0f;
            l.color = couleur;
            l.shadows = LightShadows.None;
        }
        EditorUtility.SetDirty(cible);
        return true;
    }

    // Matériau URP Lit découpé (alpha) et lumineux, à partir des textures Insert_*.png
    static Material MateriauInsert(string nom)
    {
        string cheminTex = $"{DossierTextures}/{nom}.png";
        if (AssetImporter.GetAtPath(cheminTex) is TextureImporter imp &&
            (!imp.alphaIsTransparency || imp.wrapMode != TextureWrapMode.Clamp))
        {
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
        }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(cheminTex);
        Material mat = MateriauLit($"{DossierMateriaux}/{nom}.mat", tex, null, tex, Color.white, 0f, 0.85f, Vector2.one, Color.white);
        mat.SetFloat("_AlphaClip", 1f);
        mat.SetFloat("_Cutoff", 0.5f);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Lueurs (bloom) : les inserts allumés et les braises des murs brillent, comme sur un vrai flipper.
    static void ConfigurerLueurs(StringBuilder rapport)
    {
        if (Object.FindAnyObjectByType<UnityEngine.Rendering.Volume>() == null)
        {
            var profil = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>("Assets/Settings/SampleSceneProfile.asset");
            if (profil != null)
            {
                var go = new GameObject("Volume_Lueurs");
                Undo.RegisterCreatedObjectUndo(go, "Volume_Lueurs");
                var volume = go.AddComponent<UnityEngine.Rendering.Volume>();
                volume.isGlobal = true;
                volume.sharedProfile = profil;
                rapport.AppendLine("- Volume_Lueurs créé (bloom : les inserts et les braises brillent).");
            }
        }
        foreach (Camera cam in Object.FindObjectsByType<Camera>())
        {
            var donnees = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (donnees == null || donnees.renderPostProcessing) continue;
            Undo.RecordObject(donnees, "Post-traitement");
            donnees.renderPostProcessing = true;
            rapport.AppendLine($"- Caméra {cam.name} : post-traitement activé (nécessaire pour le bloom).");
        }
    }

    // ==================================================================== PORTE DU LANCEUR

    const float AnglePorte = 25f;

    // Porte à sens unique en haut du couloir du lanceur : la bille passe en montant (OneWayGate),
    // mais en redescendant elle glisse le long de la porte (inclinée) et retombe sur le plateau.
    static void ConstruirePorteLanceur(Vector3 centreBille, float rayon, Vector3 normale, Vector3 hautTable, StringBuilder rapport, StringBuilder aVerifier)
    {
        const string nom = "Porte_Lanceur";
        if (TrouverParNom(nom) != null) return;
        Physics.SyncTransforms();
        Vector3 droite = Vector3.Cross(normale, hautTable);

        // Haut du mur gauche du couloir : on remonte le couloir tant qu'il y a un mur à gauche
        float vFin = -1f;
        for (float d = 0f; d < 40f; d += 0.05f)
        {
            if (!RaycastHorsBille(centreBille + hautTable * d, -droite, 0.8f, out RaycastHit _)) { vFin = d; break; }
        }
        Vector3 repere = centreBille + hautTable * (vFin - 0.4f);
        if (vFin < 1f ||
            !RaycastHorsBille(repere, -droite, 1.5f, out RaycastHit murG) ||
            !RaycastHorsBille(repere, droite, 1.5f, out RaycastHit murD))
        {
            aVerifier.AppendLine("- Couloir du lanceur : bout du mur gauche introuvable, porte non créée.");
            return;
        }

        float uG = Vector3.Dot(murG.point - centreBille, droite), uD = Vector3.Dot(murD.point - centreBille, droite);
        Vector3 surface = centreBille - normale * rayon;
        // Bout gauche collé au bout du mur gauche, bout droit plus haut : la bille qui redescend glisse vers le plateau
        Vector3 pG = surface + droite * uG + hautTable * (vFin - 0.05f);
        Vector3 pD = surface + droite * uD + hautTable * (vFin - 0.05f + (uD - uG) * Mathf.Tan(AnglePorte * Mathf.Deg2Rad));
        Vector3 direction = (pD - pG).normalized;
        float longueur = (pD - pG).magnitude;

        var porte = new GameObject(nom);
        Undo.RegisterCreatedObjectUndo(porte, nom);
        porte.transform.SetPositionAndRotation((pG + pD) * 0.5f + normale * 0.45f, Quaternion.LookRotation(Vector3.Cross(direction, normale), normale));

        var solide = porte.AddComponent<BoxCollider>();
        solide.size = new Vector3(longueur + 0.1f, 0.9f, 0.06f);
        solide.sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>($"{DossierPhysique}/Plateau.physicMaterial");
        var zone = porte.AddComponent<BoxCollider>();
        zone.isTrigger = true;
        zone.size = new Vector3(longueur, 0.9f, 1.4f);

        var sens = porte.AddComponent<OneWayGate>();
        sens.solidCollider = solide;
        sens.allowedDirection = Vector3.forward;   // vers le haut du couloir

        // Petite lame métallique visible
        GameObject lame = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(lame.GetComponent<Collider>());
        lame.name = "Lame";
        lame.transform.SetParent(porte.transform, false);
        lame.transform.localPosition = new Vector3(0f, -0.15f, 0f);
        lame.transform.localScale = new Vector3(longueur, 0.6f, 0.03f);
        Material metal = AssetDatabase.LoadAssetAtPath<Material>($"{DossierMateriaux}/Metal_Brosse.mat");
        if (metal != null) lame.GetComponent<Renderer>().sharedMaterial = metal;

        rapport.AppendLine("- Porte_Lanceur : la bille sort du couloir du lanceur mais ne peut plus y retomber.");
    }

    // Libre = rien (hors plateau, vitre et triggers) dans le volume où passe la bille.
    static bool EmplacementLibre(Vector3 p, Vector3 normale)
    {
        foreach (Collider c in Physics.OverlapCapsule(p + normale * 0.35f, p + normale * 1.0f, 0.45f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (c.name == "Playfield" || c.name == "Vitre" || Bille.Est(c.gameObject)) continue;
            return false;
        }
        // Il faut aussi du plateau sous la pastille
        return Physics.Raycast(p + normale * 0.3f, -normale, out RaycastHit sol, 0.6f, ~0, QueryTriggerInteraction.Ignore)
            && sol.collider.name == "Playfield";
    }

    // ==================================================================== TEXTURES DES MURS

    [MenuItem("Flipper/Textures des murs", priority = 2)]
    public static void MenuTexturesMurs()
    {
        var rapport = new StringBuilder("Flipper > Textures des murs :\n");
        var aVerifier = new StringBuilder();
        Undo.SetCurrentGroupName("Textures des murs");
        int groupe = Undo.GetCurrentGroup();
        AppliquerTexturesMurs(rapport, aVerifier);
        Undo.CollapseUndoOperations(groupe);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log(rapport.Append(aVerifier).ToString());
    }

    // Bois carbonisé avec braises (caisse, murs du plateau) et métal brossé (rails latéraux, pieds).
    // Les meshes de la table ont des UV à environ 24 unités par tuile : tiling 8 = une tuile tous les 3 mètres.
    static void AppliquerTexturesMurs(StringBuilder rapport, StringBuilder aVerifier)
    {
        Texture2D boisA = Charger("BoisBrule_Albedo"), boisN = Charger("BoisBrule_Normal", true), boisE = Charger("BoisBrule_Emission");
        Texture2D metalA = Charger("MetalBrosse_Albedo"), metalN = Charger("MetalBrosse_Normal", true);
        if (boisA == null || metalA == null)
        {
            aVerifier.AppendLine($"- Textures introuvables dans {DossierTextures} : murs non texturés.");
            return;
        }

        Color braise = new Color(1f, 0.42f, 0.12f) * 1.6f;
        Material bois = MateriauLit($"{DossierMateriaux}/Mur_BoisBrule.mat", boisA, boisN, boisE, braise, 0f, 0.25f, new Vector2(8, 8), Color.white);
        Material interieur = MateriauLit($"{DossierMateriaux}/Mur_Interieur.mat", boisA, boisN, boisE, braise * 0.4f, 0f, 0.15f, new Vector2(8, 8), new Color(0.6f, 0.6f, 0.6f));
        Material metal = MateriauLit($"{DossierMateriaux}/Metal_Brosse.mat", metalA, metalN, null, Color.black, 0.85f, 0.55f, new Vector2(8, 8), Color.white);

        // 1. Pièces de la table (FBX) : on remplace leurs matériaux unis par nom
        var remplacements = new Dictionary<string, Material>
        {
            { "Cabinet", bois }, { "Cabinet_Inside", interieur }, { "Metal", metal }
        };
        int nbTable = 0;
        GameObject table = TrouverParNom("PinballTable");
        if (table != null)
        {
            foreach (Renderer r in table.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                bool change = false;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null && remplacements.TryGetValue(mats[i].name, out Material nouveau))
                    {
                        mats[i] = nouveau;
                        change = true;
                    }
                if (!change) continue;
                Undo.RecordObject(r, "Texture mur");
                r.sharedMaterials = mats;
                nbTable++;
            }
        }

        // 2. Murs du plateau faits de cubes (bordures, rabatoires) encore avec le matériau blanc par défaut :
        //    un matériau par objet, avec un tiling adapté à sa taille (UV d'un cube = 0..1 par face)
        int nbCubes = 0;
        foreach (GameObject go in TousLesObjets())
        {
            string nom = go.name.ToLower();
            if (!nom.StartsWith("bordure") && !nom.StartsWith("rabatoire")) continue;
            var r = go.GetComponent<Renderer>();
            if (r == null || !EstMateriauParDefaut(r.sharedMaterial)) continue;

            Vector3 e = go.transform.lossyScale;
            float longueur = Mathf.Max(Mathf.Abs(e.x), Mathf.Abs(e.z));
            Vector2 tiling = new Vector2(Mathf.Max(0.3f, longueur / 3f), Mathf.Max(0.3f, Mathf.Abs(e.y) / 3f));
            string nomFichier = string.Concat(go.name.Split(System.IO.Path.GetInvalidFileNameChars())).Replace(' ', '_');
            Material m = MateriauLit($"{DossierMateriaux}/Murs/Mur_{nomFichier}.mat", boisA, boisN, boisE, braise, 0f, 0.25f, tiling, Color.white);
            Undo.RecordObject(r, "Texture mur");
            r.sharedMaterial = m;
            nbCubes++;
        }
        rapport.AppendLine($"- Textures : {nbTable} pièce(s) de la table + {nbCubes} mur(s) du plateau (bois carbonisé / métal brossé).");
    }

    static bool EstMateriauParDefaut(Material m)
    {
        if (m == null) return true;
        string chemin = AssetDatabase.GetAssetPath(m);
        return m.name == "Lit" && (chemin.StartsWith("Packages/") || string.IsNullOrEmpty(chemin))
            || m.name == "Default-Material";
    }

    static Texture2D Charger(string nom, bool normalMap = false)
    {
        string chemin = $"{DossierTextures}/{nom}.png";
        if (normalMap && AssetImporter.GetAtPath(chemin) is TextureImporter imp && imp.textureType != TextureImporterType.NormalMap)
        {
            imp.textureType = TextureImporterType.NormalMap;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(chemin);
    }

    // Crée (ou met à jour) un matériau URP Lit.
    static Material MateriauLit(string chemin, Texture2D albedo, Texture2D normal, Texture2D emission,
                                Color couleurEmission, float metal, float lissage, Vector2 tiling, Color teinte)
    {
        string dossier = System.IO.Path.GetDirectoryName(chemin).Replace('\\', '/');
        CreerDossier(dossier);

        var mat = AssetDatabase.LoadAssetAtPath<Material>(chemin);
        bool nouveau = mat == null;
        if (nouveau) mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));

        mat.SetTexture("_BaseMap", albedo);
        mat.SetColor("_BaseColor", teinte);
        mat.SetTextureScale("_BaseMap", tiling);
        if (normal != null)
        {
            mat.SetTexture("_BumpMap", normal);
            mat.SetFloat("_BumpScale", 1f);
            mat.EnableKeyword("_NORMALMAP");
        }
        if (emission != null)
        {
            mat.SetTexture("_EmissionMap", emission);
            mat.SetColor("_EmissionColor", couleurEmission);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        mat.SetFloat("_Metallic", metal);
        mat.SetFloat("_Smoothness", lissage);

        if (nouveau) AssetDatabase.CreateAsset(mat, chemin);
        else EditorUtility.SetDirty(mat);
        return mat;
    }

    static void CreerDossier(string chemin)
    {
        if (AssetDatabase.IsValidFolder(chemin)) return;
        string parent = System.IO.Path.GetDirectoryName(chemin).Replace('\\', '/');
        CreerDossier(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(chemin));
    }

    // ==================================================================== MENU : POINTS

    // Ajoute ScoreOnHit sur les objets sélectionnés (cibles, poteaux, rampes...).
    [MenuItem("Flipper/Ajouter des points à la sélection (ScoreOnHit)", priority = 20)]
    public static void AjouterPointsSelection()
    {
        foreach (GameObject go in Selection.gameObjects)
        {
            if (go.GetComponent<ScoreOnHit>() != null) continue;
            Undo.AddComponent<ScoreOnHit>(go);
            if (go.GetComponentInChildren<Collider>() == null)
                Debug.LogWarning($"\"{go.name}\" n'a pas de collider : ScoreOnHit ne détectera pas la bille.", go);
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    [MenuItem("Flipper/Ajouter des points à la sélection (ScoreOnHit)", true)]
    static bool ValiderAjouterPoints() => Selection.gameObjects.Length > 0;

    // ==================================================================== outils

    static IEnumerable<GameObject> TousLesObjets()
    {
        var liste = new List<GameObject>();
        foreach (GameObject racine in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (Transform t in racine.GetComponentsInChildren<Transform>(true))
                liste.Add(t.gameObject);
        return liste;
    }

    static GameObject TrouverParNom(string nom)
    {
        foreach (GameObject go in TousLesObjets())
            if (go.name == nom) return go;
        return null;
    }

    static GameObject TrouverBille()
    {
        foreach (GameObject go in TousLesObjets())
            if (go.CompareTag(Bille.Tag)) return go;
        return TrouverParNom("la_bille");
    }

    // Objets dont le nom commence par le préfixe (on garde le plus haut dans la hiérarchie)
    static List<GameObject> TrouverRacines(string prefixe)
    {
        var liste = new List<GameObject>();
        foreach (GameObject go in TousLesObjets())
        {
            if (!go.name.ToLower().StartsWith(prefixe)) continue;
            Transform parent = go.transform.parent;
            if (parent != null && parent.name.ToLower().StartsWith(prefixe)) continue;
            liste.Add(go);
        }
        return liste;
    }

    static GameObject TrouverOuCreer(string nom, Vector3 position, Quaternion rotation, out bool cree)
    {
        GameObject go = TrouverParNom(nom);
        cree = go == null;
        if (cree)
        {
            go = new GameObject(nom);
            go.transform.SetPositionAndRotation(position, rotation);
            Undo.RegisterCreatedObjectUndo(go, nom);
        }
        return go;
    }

    static T ObtenirOuAjouter<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        if (c == null) return Undo.AddComponent<T>(go);
        Undo.RecordObject(c, typeof(T).Name);
        return c;
    }

    static float DiametreBille(GameObject bille)
    {
        var sphere = bille.GetComponent<SphereCollider>();
        Vector3 e = bille.transform.lossyScale;
        float echelle = Mathf.Max(Mathf.Abs(e.x), Mathf.Abs(e.y), Mathf.Abs(e.z));
        return sphere != null ? sphere.radius * 2f * echelle : echelle;
    }

    // Rayon vers le bas sous la bille : renvoie le plateau (le plus proche, hors bille et triggers).
    static bool RaycastSousBille(GameObject bille, out RaycastHit resultat)
    {
        resultat = default;
        var touches = Physics.RaycastAll(bille.transform.position + Vector3.up * 2f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in touches.OrderBy(h => h.distance))
        {
            if (hit.collider.attachedRigidbody != null && Bille.Est(hit.collider.attachedRigidbody.gameObject)) continue;
            if (Bille.Est(hit.collider.gameObject)) continue;
            if (hit.collider.attachedRigidbody != null && !hit.collider.attachedRigidbody.isKinematic) continue;
            if (hit.collider.GetComponentInParent<Script_ressortBrule>() != null) continue;
            if (hit.collider.name == "Vitre") continue;   // la vitre est au-dessus de la bille, pas dessous
            resultat = hit;
            return true;
        }
        return false;
    }

    // Normale de secours : l'axe de rotation des flippers.
    static Vector3 NormaleFlippers()
    {
        Vector3 somme = Vector3.zero;
        foreach (GameObject go in TousLesObjets())
            if (go.GetComponent<FlipperBrule>() != null)
                somme += go.transform.up;
        return somme.sqrMagnitude > 0.01f ? somme.normalized : Vector3.up;
    }
}
