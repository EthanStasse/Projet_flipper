using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Les arbres en feu remplacent les poteaux en métal ("Metal Posts") : chaque arbre de la scène va
// sur un poteau (le plus proche encore libre), réduit à 60 % pour tenir dans le groupe de poteaux,
// et les poteaux sont retirés. Flammes et lueur sont réduites avec l'arbre.
// Menu "Flipper > Arbres à la place des poteaux".
public static partial class InstallateurFlipper
{
    const float EchelleArbrePoteau = 0.6f;
    const float PorteeLueurArbre = 2.4f;   // portée de la lueur dans le prefab (taille normale)

    [MenuItem("Flipper/Arbres à la place des poteaux", priority = 9)]
    public static void MenuArbresPoteaux()
    {
        var rapport = new StringBuilder("Flipper > Arbres à la place des poteaux :\n");
        var aVerifier = new StringBuilder();
        Undo.SetCurrentGroupName("Arbres à la place des poteaux");
        int groupe = Undo.GetCurrentGroup();
        ArbresALaPlaceDesPoteaux(rapport, aVerifier);
        Undo.CollapseUndoOperations(groupe);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        if (aVerifier.Length > 0) rapport.Append("\nAttention :\n").Append(aVerifier);
        Debug.Log(rapport.ToString());
    }

    static void ArbresALaPlaceDesPoteaux(StringBuilder rapport, StringBuilder aVerifier)
    {
        var racines = SceneManager.GetActiveScene().GetRootGameObjects();
        var poteaux = racines.Where(g => g.name.StartsWith("Metal Posts")).ToList();
        var arbres = racines.Where(g => g.GetComponent<ArbreEnFeu>() != null).ToList();
        if (poteaux.Count == 0)
        {
            aVerifier.AppendLine("- Aucun poteau \"Metal Posts\" dans la scène (déjà remplacés ?).");
            return;
        }
        if (arbres.Count < poteaux.Count)
            aVerifier.AppendLine($"- {arbres.Count} arbre(s) pour {poteaux.Count} poteaux : les poteaux en trop restent en métal.");

        // Vraie place de chaque poteau = centre de son modèle 3D (le groupe parent peut être décalé), posée sur le plateau
        var places = new List<(GameObject poteau, Vector3 point, Vector3 normale)>();
        foreach (GameObject p in poteaux)
        {
            Renderer[] rendus = p.GetComponentsInChildren<Renderer>(true);
            if (rendus.Length == 0) continue;
            Bounds b = rendus[0].bounds;
            foreach (Renderer r in rendus) b.Encapsulate(r.bounds);
            if (!SurfacePlateau(b.center.x, b.center.z, out Vector3 point, out Vector3 normale))
            {
                aVerifier.AppendLine($"- {p.name} : plateau introuvable dessous, laissé en place.");
                continue;
            }
            places.Add((p, point, normale));
        }

        // Chaque poteau prend l'arbre libre le plus proche
        var libres = new List<GameObject>(arbres);
        var poses = new List<GameObject>();
        foreach (var place in places.OrderBy(pl => pl.point.z))
        {
            if (libres.Count == 0) break;
            GameObject arbre = libres.OrderBy(a => (a.transform.position - place.point).sqrMagnitude).First();
            libres.Remove(arbre);

            Vector3 hautTable = Vector3.ProjectOnPlane(Vector3.forward, place.normale).normalized;
            Undo.RecordObject(arbre.transform, "Arbre sur poteau");
            Vector3 echelleOrigine = PrefabUtility.GetCorrespondingObjectFromSource(arbre)?.transform.localScale ?? Vector3.one;
            arbre.transform.SetPositionAndRotation(place.point, Quaternion.LookRotation(hautTable, place.normale));
            arbre.transform.localScale = echelleOrigine * EchelleArbrePoteau;

            // Les flammes suivent la taille de l'arbre, la lueur porte moins loin
            foreach (ParticleSystem ps in arbre.GetComponentsInChildren<ParticleSystem>(true))
            {
                Undo.RecordObject(ps, "Flammes");
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
            foreach (Light l in arbre.GetComponentsInChildren<Light>(true))
            {
                Undo.RecordObject(l, "Lueur");
                l.range = PorteeLueurArbre * EchelleArbrePoteau;
            }

            rapport.AppendLine($"- {arbre.name} → place de {place.poteau.name} ({place.point.x:F2}, {place.point.z:F2}).");
            Undo.DestroyObjectImmediate(place.poteau);
            poses.Add(arbre);
        }

        // Ce que les arbres touchent maintenant (hors plateau, vitre et autres arbres posés)
        Physics.SyncTransforms();
        foreach (GameObject arbre in poses)
        {
            var capsule = arbre.GetComponent<CapsuleCollider>();
            if (capsule == null) continue;
            Vector3 centre = arbre.transform.TransformPoint(capsule.center);
            float rayon = capsule.radius * EchelleArbrePoteau;
            var genes = Physics.OverlapSphere(centre, rayon, ~0, QueryTriggerInteraction.Ignore)
                .Where(c => !EstSurfaceOuVitre(c) && !c.transform.IsChildOf(arbre.transform))
                .Select(c => c.name).Distinct().ToList();
            if (genes.Count > 0)
                aVerifier.AppendLine($"- {arbre.name} touche : {string.Join(", ", genes)}.");
        }
        rapport.AppendLine($"- {poses.Count} arbre(s) posé(s) à la place des poteaux, réduits à {EchelleArbrePoteau * 100f:0} %.");
    }
}
