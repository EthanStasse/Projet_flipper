using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Sons du flipper (générés par Outils/generer_sons.py, dans Assets/son).
// Menu "Flipper > Brancher les sons" (aussi fait par Flipper > Installer) : assigne chaque son à son objet.
// Un son déjà choisi à la main n'est pas remplacé (sauf le faux fichier MP3 du ressort, qui n'est pas un vrai son).
public static partial class InstallateurFlipper
{
    const string DossierSons = "Assets/son";

    [MenuItem("Flipper/Brancher les sons", priority = 4)]
    public static void MenuBrancherSons()
    {
        var rapport = new StringBuilder("Flipper > Brancher les sons :\n");
        Undo.SetCurrentGroupName("Brancher les sons");
        int groupe = Undo.GetCurrentGroup();
        BrancherSons(rapport);
        Undo.CollapseUndoOperations(groupe);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log(rapport.ToString());
    }

    static AudioClip Son(string nom) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{DossierSons}/{nom}.wav");

    // Vrai si le clip manque ou n'est pas un vrai son (ex. pointeur Git LFS de quelques octets)
    static bool SonAbsent(AudioClip clip)
    {
        if (clip == null) return true;
        string chemin = AssetDatabase.GetAssetPath(clip);
        return !string.IsNullOrEmpty(chemin) && new System.IO.FileInfo(chemin).Length < 1024;
    }

    static void BrancherSons(StringBuilder rapport)
    {
        int nb = 0;
        foreach (GameObject go in TousLesObjets())
        {
            if (go == null) continue;

            var ressort = go.GetComponent<Script_ressortBrule>();
            if (ressort != null && SonAbsent(ressort.sonRessort))
            {
                Undo.RecordObject(ressort, "Son");
                ressort.sonRessort = Son("Ressort_Boing"); Signaler(ressort);
                if (ressort.audioSource == null) ressort.audioSource = SourceAudio2D(go);
                nb++;
            }

            var flipper = go.GetComponent<FlipperBrule>();
            if (flipper != null && SonAbsent(flipper.sonFlipper))
            {
                Undo.RecordObject(flipper, "Son");
                flipper.sonFlipper = Son("Flipper_Clac"); Signaler(flipper);
                if (flipper.audioSource == null) flipper.audioSource = SourceAudio2D(go);
                nb++;
            }

            var bumper = go.GetComponent<BumperEffectBrule>();
            if (bumper != null && SonAbsent(bumper.son))
            {
                Undo.RecordObject(bumper, "Son");
                bumper.son = Son(go.GetComponent<ArbreEnFeu>() != null ? "Arbre_Feu" : "Bumper_Pop"); Signaler(bumper);
                nb++;
            }

            var boost = go.GetComponent<BoostRail>();
            if (boost != null && SonAbsent(boost.son))
            {
                Undo.RecordObject(boost, "Son");
                boost.son = Son("Rail_Boost"); Signaler(boost);
                nb++;
            }

            var cible = go.GetComponent<CibleBonus>();
            if (cible != null && SonAbsent(cible.son))
            {
                Undo.RecordObject(cible, "Son");
                cible.son = Son("Insert_Ding"); Signaler(cible);
                nb++;
            }

            var groupeBonus = go.GetComponent<GroupeBonus>();
            if (groupeBonus != null && SonAbsent(groupeBonus.son))
            {
                Undo.RecordObject(groupeBonus, "Son");
                groupeBonus.son = Son("Bonus_Jingle"); Signaler(groupeBonus);
                nb++;
            }

            var gm = go.GetComponent<GameManager>();
            if (gm != null && SonAbsent(gm.sonBillePerdue))
            {
                Undo.RecordObject(gm, "Son");
                gm.sonBillePerdue = Son("Bille_Perdue"); Signaler(gm);
                nb++;
            }
        }
        if (nb > 0) rapport.AppendLine($"- Sons : {nb} objet(s) sonorisé(s) (ressort, flippers, bumpers, arbres, rail, inserts, bonus, drain).");

        // Le prefab de l'arbre en feu lui-même (pour les arbres posés plus tard)
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CheminPrefabArbre);
        if (prefab != null && SonAbsent(prefab.GetComponent<BumperEffectBrule>()?.son))
        {
            GameObject contenu = PrefabUtility.LoadPrefabContents(CheminPrefabArbre);
            contenu.GetComponent<BumperEffectBrule>().son = Son("Arbre_Feu");
            PrefabUtility.SaveAsPrefabAsset(contenu, CheminPrefabArbre);
            PrefabUtility.UnloadPrefabContents(contenu);
        }
    }

    // Sur une instance de prefab, une modification faite par script doit être signalée pour être sauvegardée
    static void Signaler(Component c)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(c))
            PrefabUtility.RecordPrefabInstancePropertyModifications(c);
    }

    // AudioSource 2D (même volume partout sur la table)
    static AudioSource SourceAudio2D(GameObject go)
    {
        var source = go.GetComponent<AudioSource>();
        if (source != null) return source;
        source = Undo.AddComponent<AudioSource>(go);
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        return source;
    }
}
