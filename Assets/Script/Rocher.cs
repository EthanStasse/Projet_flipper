using System.Collections.Generic;
using UnityEngine;

// Petit rocher low-poly généré par code (visible dans l'éditeur et en jeu).
// Si l'objet a un MeshCollider, il reçoit la forme du rocher (convexe) :
// la bille rebondit dessus et sa trajectoire est déviée.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Rocher : MonoBehaviour
{
    public float rayon = 0.35f;
    [Tooltip("Hauteur au-dessus de la table. Garder > 0.45 pour que la bille ne monte pas dessus.")]
    public float hauteur = 0.5f;
    [Tooltip("Change la forme du rocher.")]
    public int graine = 1;

    List<Vector3> verts;
    List<Vector3> normales;
    List<int> tris;
    Mesh mesh;
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
        if (mesh == null) return;
        if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
    }

    void Generer()
    {
        verts = new List<Vector3>();
        normales = new List<Vector3>();
        tris = new List<int>();

        const int lon = 7, lat = 4;
        System.Random rnd = new System.Random(graine);
        float[,] bruit = new float[lon, lat];
        for (int i = 0; i < lon; i++)
            for (int j = 0; j < lat; j++)
                bruit[i, j] = 0.78f + (float)rnd.NextDouble() * 0.4f;
        float rot = (float)rnd.NextDouble() * Mathf.PI * 2f;
        Vector3 sommet = new Vector3(((float)rnd.NextDouble() - 0.5f) * rayon * 0.3f, hauteur, ((float)rnd.NextDouble() - 0.5f) * rayon * 0.3f);
        Vector3 interieur = Vector3.up * hauteur * 0.35f;

        // anneaux du bas (légèrement enfoncé dans la table) vers le haut, puis un sommet
        System.Func<int, int, Vector3> pt = (i, j) =>
        {
            float a = rot + (i % lon) * Mathf.PI * 2f / lon;
            float b = Mathf.Lerp(-0.15f, 0.8f, (float)j / (lat - 1)) * Mathf.PI / 2f;
            float rr = rayon * bruit[i % lon, j];
            return new Vector3(Mathf.Cos(a) * Mathf.Cos(b) * rr, Mathf.Sin(b) * hauteur / Mathf.Sin(0.8f * Mathf.PI / 2f) * 0.92f, Mathf.Sin(a) * Mathf.Cos(b) * rr);
        };

        for (int i = 0; i < lon; i++)
        {
            for (int j = 0; j < lat - 1; j++)
            {
                Vector3 a = pt(i, j), b = pt(i + 1, j), c = pt(i + 1, j + 1), d = pt(i, j + 1);
                Tri(a, d, c, interieur);
                Tri(a, c, b, interieur);
            }
            Tri(pt(i, lat - 1), pt(i + 1, lat - 1), sommet, interieur);
        }

        if (mesh == null)
            mesh = new Mesh { name = "Rocher (généré)", hideFlags = HideFlags.DontSave };
        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetNormals(normales);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        GetComponent<MeshFilter>().sharedMesh = mesh;

        MeshCollider col = GetComponent<MeshCollider>();
        if (col != null)
        {
            col.convex = true;
            col.sharedMesh = null;
            col.sharedMesh = mesh;
        }
    }

    // Triangle à facette plate, tourné à l'opposé du point "interieur".
    void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 interieur)
    {
        Vector3 n = Vector3.Cross(b - a, c - a);
        if (n.sqrMagnitude < 1e-12f) return;
        n.Normalize();
        if (Vector3.Dot(n, (a + b + c) / 3f - interieur) < 0f)
        {
            Vector3 t = b; b = c; c = t;
            n = -n;
        }
        int i0 = verts.Count;
        verts.Add(a); verts.Add(b); verts.Add(c);
        normales.Add(n); normales.Add(n); normales.Add(n);
        tris.Add(i0); tris.Add(i0 + 1); tris.Add(i0 + 2);
    }
}
