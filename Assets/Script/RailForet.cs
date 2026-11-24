using System.Collections.Generic;
using UnityEngine;

// Rampe fine façon "rampe en fils", en bois, générée le long d'une courbe lisse.
// - l'entrée (partie basse) est pleine, avec un nez en V au bout pour qu'une bille
//   venant de dessous soit déviée sur le côté au lieu de rester bloquée ;
// - au-delà, la rampe est faite de rails fins sur pilotis : le dessous est ouvert,
//   la bille peut passer sous la rampe ;
// - la sortie se termine en l'air : la bille retombe sur la table.
//
// L'objet doit avoir la même position / rotation que la table (fond) :
// en local, y = hauteur au-dessus de la table, x / z = position sur la table.
// Garder la partie ouverte à plus de ~0.75 de haut pour que la bille passe dessous.
//
// Matériaux du MeshRenderer : 0 planche claire, 1 planche foncée, 2 rails / flancs, 3 poteaux.
// Le MeshCollider reçoit une forme simplifiée (piste lisse + bords), pas les rails visuels.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RailForet : MonoBehaviour
{
    [Tooltip("Points de passage (local) : x, hauteur de la piste au-dessus de la table, z. De l'entrée à la sortie.")]
    public Vector3[] points;

    [Header("Rails")]
    [Tooltip("Demi-écart des deux rails sur lesquels roule la bille.")]
    public float ecartRails = 0.17f;
    [Tooltip("Demi-écart des rails de guidage (doit rester > rayon de la bille, 0.29).")]
    public float ecartGuides = 0.36f;
    public float hauteurGuides = 0.3f;
    public float rayonRail = 0.035f;
    public float rayonLien = 0.022f;
    public float pasLiens = 0.45f;
    public float pasPoteaux = 1.6f;
    public float rayonPoteau = 0.05f;

    [Header("Entrée pleine")]
    [Tooltip("En dessous de cette hauteur, la rampe est pleine (fermée jusqu'à la table).")]
    public float hauteurFermee = 0.68f;
    public float largeurEntree = 1.5f;
    public float longueurEntree = 1.2f;
    public float longueurNez = 0.5f;

    [Tooltip("Distance entre deux coupes de la rampe (plus petit = plus lisse).")]
    public float pas = 0.1f;

    const int Claire = 0, Foncee = 1, Rail = 2, Poteau = 3;
    const float Sol = -0.05f; // juste sous la table

    // maillage visible
    List<Vector3> verts, normales;
    List<int>[] tris;
    // maillage de collision
    List<Vector3> cVerts;
    List<int> cTris;

    Mesh mesh, meshCollision;
    bool aRegenerer = true;

    void OnEnable() { aRegenerer = true; }
    void OnValidate() { aRegenerer = true; }

    void Update()
    {
        if (!aRegenerer) return;
        aRegenerer = false;
        Generer();
    }

    void OnDestroy()
    {
        Detruire(mesh);
        Detruire(meshCollision);
    }

    static void Detruire(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
    }

    // ---------- courbe ----------

    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    static float DistXZ(Vector3 a, Vector3 b)
    {
        return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }

    List<Vector3> Echantillonner()
    {
        var dense = new List<Vector3>();
        int n = points.Length;
        for (int i = 0; i < n - 1; i++)
        {
            Vector3 p0 = points[Mathf.Max(i - 1, 0)], p1 = points[i], p2 = points[i + 1], p3 = points[Mathf.Min(i + 2, n - 1)];
            for (int k = 0; k < 40; k++)
                dense.Add(CatmullRom(p0, p1, p2, p3, k / 40f));
        }
        dense.Add(points[n - 1]);

        var res = new List<Vector3> { dense[0] };
        float reste = 0f;
        for (int i = 1; i < dense.Count; i++)
        {
            Vector3 a = dense[i - 1], b = dense[i];
            float d = DistXZ(a, b);
            while (reste + d >= pas && d > 1e-6f)
            {
                a = Vector3.Lerp(a, b, (pas - reste) / d);
                d = DistXZ(a, b);
                res.Add(a);
                reste = 0f;
            }
            reste += d;
        }
        if ((res[res.Count - 1] - dense[dense.Count - 1]).sqrMagnitude > 1e-6f)
            res.Add(dense[dense.Count - 1]);
        return res;
    }

    // ---------- génération ----------

    void Generer()
    {
        if (points == null || points.Length < 2) return;

        verts = new List<Vector3>();
        normales = new List<Vector3>();
        tris = new List<int>[4];
        for (int i = 0; i < 4; i++) tris[i] = new List<int>();
        cVerts = new List<Vector3>();
        cTris = new List<int>();

        List<Vector3> c = Echantillonner();
        int n = c.Count;
        var av = new Vector3[n];     // direction d'avancée (horizontale)
        var cote = new Vector3[n];   // côté (horizontal)
        var demi = new float[n];     // demi-largeur entre les guides
        var abs = new float[n];      // abscisse le long de la rampe
        for (int i = 0; i < n; i++)
        {
            Vector3 t = c[Mathf.Min(i + 1, n - 1)] - c[Mathf.Max(i - 1, 0)];
            t.y = 0f;
            av[i] = t.normalized;
            cote[i] = new Vector3(av[i].z, 0f, -av[i].x);
            abs[i] = i == 0 ? 0f : abs[i - 1] + DistXZ(c[i], c[i - 1]);
            float k = Mathf.Clamp01(abs[i] / Mathf.Max(longueurEntree, 0.01f));
            k = k * k * (3f - 2f * k);
            demi[i] = Mathf.Lerp(largeurEntree * 0.5f, ecartGuides, k);
        }

        // fin de la partie pleine : premier point assez haut pour passer dessous
        int e = 0;
        while (e < n - 1 && c[e].y < hauteurFermee) e++;

        Vector3 up = Vector3.up;

        // ----- entrée pleine -----
        for (int i = 0; i < e; i++)
        {
            int j = i + 1;
            float hi = Mathf.Max(c[i].y, 0f), hj = Mathf.Max(c[j].y, 0f);
            Vector3 ci = new Vector3(c[i].x, c[i].y, c[i].z), cj = new Vector3(c[j].x, c[j].y, c[j].z);
            Vector3 li = cote[i] * demi[i], lj = cote[j] * demi[j];

            int planche = (i / 2) % 2 == 0 ? Claire : Foncee;
            Quad(ci - li, ci + li, cj + lj, cj - lj, planche, up, true);

            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 ai = ci + li * s, aj = cj + lj * s;
                Vector3 ai0 = new Vector3(ai.x, Sol, ai.z), aj0 = new Vector3(aj.x, Sol, aj.z);
                Vector3 hautI = ai + up * hauteurGuides, hautJ = aj + up * hauteurGuides;
                // flanc plein, de la table au haut du bord
                Quad(ai0, aj0, hautJ, hautI, Rail, (li + lj) * s, true);
                Quad(ai0, aj0, hautJ, hautI, Rail, -(li + lj) * s, true);
                // petit rondin sur le bord
                Tube(hautI, hautJ, rayonRail * 1.3f, 6, Rail);
            }
        }

        // nez en V sous la fin de la partie pleine
        if (e > 0)
        {
            Vector3 ce = c[e];
            Vector3 pointe = ce + av[e] * longueurNez;
            Vector3 pointe0 = new Vector3(pointe.x, Sol, pointe.z), pointeH = new Vector3(pointe.x, ce.y, pointe.z);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 a = ce + cote[e] * demi[e] * s;
                Vector3 a0 = new Vector3(a.x, Sol, a.z);
                Vector3 dehors = Vector3.Cross(up, pointe - a).normalized * -s;
                Quad(a0, pointe0, pointeH, a, Rail, dehors, true);
                Quad(a0, pointe0, pointeH, a, Rail, -dehors, true);
            }
        }

        // ----- partie ouverte : rails fins -----
        float prochainLien = abs[e], prochainPoteau = abs[e];
        for (int i = e; i < n; i++)
        {
            Vector3 ci = c[i], li = cote[i];

            if (i < n - 1)
            {
                int j = i + 1;
                Vector3 cj = c[j], lj = cote[j];
                float gi = demi[i], gj = demi[j];

                // rails visibles
                foreach (float s in new[] { -1f, 1f })
                {
                    Tube(ci + li * ecartRails * s, cj + lj * ecartRails * s, rayonRail, 6, Rail);
                    Tube(ci + li * gi * s + up * hauteurGuides, cj + lj * gj * s + up * hauteurGuides, rayonRail, 6, Rail);
                    Tube(ci + li * gi * s + up * hauteurGuides * 0.45f, cj + lj * gj * s + up * hauteurGuides * 0.45f, rayonRail * 0.8f, 5, Rail);
                }

                // collision : piste lisse sous la bille + deux bords verticaux
                float y = -(0.29f - Mathf.Sqrt(0.29f * 0.29f - ecartRails * ecartRails)) + rayonRail;
                Vector3 pi = ci + up * y, pj = cj + up * y;
                float lp = ecartRails + 0.04f;
                QuadCollision(pi - li * lp, pi + li * lp, pj + lj * lp, pj - lj * lp);
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 bi = ci + li * (gi - rayonRail) * s, bj = cj + lj * (gj - rayonRail) * s;
                    QuadCollision(bi + up * y, bj + up * y, bj + up * (hauteurGuides + rayonRail), bi + up * (hauteurGuides + rayonRail));
                }
            }

            // liens en U
            if (abs[i] >= prochainLien)
            {
                prochainLien += pasLiens;
                float g = demi[i];
                Vector3 bas = ci - up * (rayonRail + rayonLien);
                Tube(bas - li * g, bas + li * g, rayonLien, 5, Claire);
                foreach (float s in new[] { -1f, 1f })
                    Tube(bas + li * g * s, ci + li * g * s + up * hauteurGuides, rayonLien, 5, Claire);
            }

            // pilotis
            if (abs[i] >= prochainPoteau && ci.y > 0.3f)
            {
                prochainPoteau += pasPoteaux;
                float g = demi[i] + rayonPoteau;
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 haut = ci + li * g * s - up * (rayonRail + rayonLien * 2f);
                    Vector3 bas = new Vector3(haut.x, Sol, haut.z);
                    Tube(bas, haut, rayonPoteau, 6, Poteau);
                    TubeCollision(bas, haut, rayonPoteau, 6);
                }
            }
        }

        // ----- envoi des maillages -----
        if (mesh == null)
            mesh = new Mesh { name = "Rail forêt (généré)", hideFlags = HideFlags.DontSave, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetNormals(normales);
        mesh.subMeshCount = 4;
        for (int i = 0; i < 4; i++) mesh.SetTriangles(tris[i], i);
        mesh.RecalculateBounds();
        GetComponent<MeshFilter>().sharedMesh = mesh;

        MeshCollider col = GetComponent<MeshCollider>();
        if (col != null)
        {
            if (meshCollision == null)
                meshCollision = new Mesh { name = "Rail forêt (collision)", hideFlags = HideFlags.DontSave, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            meshCollision.Clear();
            meshCollision.SetVertices(cVerts);
            meshCollision.SetTriangles(cTris, 0);
            meshCollision.RecalculateNormals();
            meshCollision.RecalculateBounds();
            col.convex = false;
            col.sharedMesh = null;
            col.sharedMesh = meshCollision;
        }
    }

    // ---------- primitives ----------

    static void Repere(Vector3 dir, out Vector3 u, out Vector3 v)
    {
        Vector3 d = dir.normalized;
        Vector3 r = Mathf.Abs(Vector3.Dot(d, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
        u = Vector3.Cross(d, r).normalized;
        v = Vector3.Cross(d, u).normalized;
    }

    void Tube(Vector3 a, Vector3 b, float r, int seg, int mat)
    {
        if ((b - a).sqrMagnitude < 1e-8f) return;
        Repere(b - a, out Vector3 u, out Vector3 v);
        for (int k = 0; k < seg; k++)
        {
            float t0 = k * Mathf.PI * 2f / seg, t1 = (k + 1) * Mathf.PI * 2f / seg;
            Vector3 o0 = (u * Mathf.Cos(t0) + v * Mathf.Sin(t0)) * r;
            Vector3 o1 = (u * Mathf.Cos(t1) + v * Mathf.Sin(t1)) * r;
            Quad(a + o0, a + o1, b + o1, b + o0, mat, o0 + o1, false);
        }
    }

    void TubeCollision(Vector3 a, Vector3 b, float r, int seg)
    {
        Repere(b - a, out Vector3 u, out Vector3 v);
        for (int k = 0; k < seg; k++)
        {
            float t0 = k * Mathf.PI * 2f / seg, t1 = (k + 1) * Mathf.PI * 2f / seg;
            Vector3 o0 = (u * Mathf.Cos(t0) + v * Mathf.Sin(t0)) * r;
            Vector3 o1 = (u * Mathf.Cos(t1) + v * Mathf.Sin(t1)) * r;
            QuadCollision(a + o0, a + o1, b + o1, b + o0);
        }
    }

    void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int mat, Vector3 dir, bool collision)
    {
        Tri(a, b, c, mat, dir);
        Tri(a, c, d, mat, dir);
        if (collision) QuadCollision(a, b, c, d);
    }

    // Les MeshColliders non convexes testent les deux faces : l'orientation n'a pas d'importance.
    void QuadCollision(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        int i0 = cVerts.Count;
        cVerts.Add(a); cVerts.Add(b); cVerts.Add(c); cVerts.Add(d);
        cTris.Add(i0); cTris.Add(i0 + 1); cTris.Add(i0 + 2);
        cTris.Add(i0); cTris.Add(i0 + 2); cTris.Add(i0 + 3);
    }

    // Triangle à facette plate, tourné vers "dir".
    void Tri(Vector3 a, Vector3 b, Vector3 c, int mat, Vector3 dir)
    {
        Vector3 nrm = Vector3.Cross(b - a, c - a);
        if (nrm.sqrMagnitude < 1e-12f) return;
        nrm.Normalize();
        if (Vector3.Dot(nrm, dir) < 0f)
        {
            Vector3 t = b; b = c; c = t;
            nrm = -nrm;
        }
        int i0 = verts.Count;
        verts.Add(a); verts.Add(b); verts.Add(c);
        normales.Add(nrm); normales.Add(nrm); normales.Add(nrm);
        tris[mat].Add(i0); tris[mat].Add(i0 + 1); tris[mat].Add(i0 + 2);
    }
}
