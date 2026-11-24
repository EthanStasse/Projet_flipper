using System.Collections.Generic;
using UnityEngine;

// Sapin low-poly généré par code (visible dans l'éditeur et en jeu).
// Matériaux du MeshRenderer : 0 = feuillage, 1 = tronc.
// La collision avec la bille se fait sur le tronc (CapsuleCollider à ajouter sur le même objet) ;
// les branches commencent au-dessus de la bille, elle peut passer dessous.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Sapin : MonoBehaviour
{
    public float hauteur = 1.6f;
    public float rayonFeuillage = 0.6f;
    public float hauteurTronc = 0.62f;
    public float rayonTronc = 0.16f;
    [Range(2, 5)] public int etages = 3;
    [Range(5, 12)] public int cotes = 8;
    public float rotation = 0f;

    List<Vector3> verts;
    List<Vector3> normales;
    List<int>[] tris;
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
        tris = new[] { new List<int>(), new List<int>() };

        // tronc
        Cylindre(Vector3.zero, rayonTronc * 1.15f, rayonTronc * 0.85f, hauteurTronc + 0.1f, 7, 1);

        // étages de feuillage, du plus large au plus fin
        float hFeuillage = hauteur - hauteurTronc;
        for (int e = 0; e < etages; e++)
        {
            float t = (float)e / etages;
            float bas = hauteurTronc + hFeuillage * t * 0.75f;
            float haut = e == etages - 1 ? hauteur : bas + hFeuillage * (1f - t * 0.75f) * 0.6f;
            float r = rayonFeuillage * (1f - t * 0.7f);
            Cone(Vector3.up * bas, r, haut - bas, rotation + e * 20f, 0);
        }

        if (mesh == null)
            mesh = new Mesh { name = "Sapin (généré)", hideFlags = HideFlags.DontSave };
        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetNormals(normales);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(tris[0], 0);
        mesh.SetTriangles(tris[1], 1);
        mesh.RecalculateBounds();
        GetComponent<MeshFilter>().sharedMesh = mesh;
    }

    void Cone(Vector3 basCentre, float r, float h, float rotDeg, int mat)
    {
        Vector3 sommet = basCentre + Vector3.up * h;
        Vector3 interieur = basCentre + Vector3.up * h * 0.3f;
        Vector3 dessous = basCentre + Vector3.up * h * 0.12f;
        float rot = rotDeg * Mathf.Deg2Rad;
        for (int i = 0; i < cotes; i++)
        {
            // bord légèrement tombant pour un effet de branches
            Vector3 a = Anneau(basCentre, r, rot + i * Mathf.PI * 2f / cotes) + Vector3.down * h * 0.08f;
            Vector3 b = Anneau(basCentre, r, rot + (i + 1) * Mathf.PI * 2f / cotes) + Vector3.down * h * 0.08f;
            Tri(a, sommet, b, mat, interieur);
            Tri(dessous, a, b, mat, dessous + Vector3.up);
        }
    }

    void Cylindre(Vector3 bas, float rBas, float rHaut, float h, int seg, int mat)
    {
        Vector3 haut = bas + Vector3.up * h;
        Vector3 milieu = bas + Vector3.up * h * 0.5f;
        for (int i = 0; i < seg; i++)
        {
            float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
            Vector3 b0 = Anneau(bas, rBas, a0), b1 = Anneau(bas, rBas, a1);
            Vector3 h0 = Anneau(haut, rHaut, a0), h1 = Anneau(haut, rHaut, a1);
            Tri(b0, h0, h1, mat, milieu);
            Tri(b0, h1, b1, mat, milieu);
        }
    }

    static Vector3 Anneau(Vector3 c, float r, float a)
    {
        return c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
    }

    // Triangle à facette plate, tourné à l'opposé du point "interieur".
    void Tri(Vector3 a, Vector3 b, Vector3 c, int mat, Vector3 interieur)
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
        tris[mat].Add(i0); tris[mat].Add(i0 + 1); tris[mat].Add(i0 + 2);
    }
}
