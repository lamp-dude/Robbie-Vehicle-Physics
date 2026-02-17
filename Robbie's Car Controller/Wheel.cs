using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Wheel : MonoBehaviour
{
    private Rigidbody rb;
    private Vehicle parentPhysics;
    private static Material lineMaterial;

    private struct SweepDebugData
    {
        public Vector3 startPos;
        public Quaternion rotation;
        public Vector3 direction;
        public RaycastHit[] hits; 
        public bool didHit;
        public bool active;
    }

    private SweepDebugData floorDebug;
    private SweepDebugData wallDebug; 

    public void Initialize(Transform carRoot)
    {
        rb = GetComponent<Rigidbody>();
        parentPhysics = carRoot.GetComponent<Vehicle>();

        rb.isKinematic = true;
        rb.useGravity = false;

        Collider[] wheelColliders = GetComponentsInChildren<Collider>();
        Collider[] carColliders = carRoot.GetComponentsInChildren<Collider>();

        foreach (var col in wheelColliders)
        {
            if (col is MeshCollider meshCol) meshCol.convex = true;
            col.isTrigger = true;

            foreach (var carCol in carColliders)
            {
                if (carCol != col) Physics.IgnoreCollision(col, carCol);
            }
        }
    }

    public RaycastHit[] PerformMeshSweepMulti(Vector3 startPos, Vector3 direction, float distance, LayerMask layers, bool isFloorSweep)
    {
        rb.position = startPos;
        rb.rotation = transform.rotation;

        RaycastHit[] hits = rb.SweepTestAll(direction, distance, QueryTriggerInteraction.Ignore);
        bool didHit = hits.Length > 0;

        SweepDebugData data = new SweepDebugData
        {
            startPos = startPos,
            rotation = transform.rotation,
            direction = direction,
            hits = hits,
            didHit = didHit,
            active = true
        };

        if (isFloorSweep) floorDebug = data;
        else wallDebug = data;

        return hits;
    }

    public void UpdateVisuals(Vector3 targetPos, Quaternion targetRot)
    {
        transform.position = targetPos;
        transform.rotation = targetRot;
    }

    private void CreateLineMaterial()
    {
        if (!lineMaterial)
        {
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            lineMaterial = new Material(shader);
            lineMaterial.hideFlags = HideFlags.HideAndDontSave;
            lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            lineMaterial.SetInt("_ZWrite", 0);
        }
    }

    private void OnRenderObject()
    {
        if (parentPhysics == null || !parentPhysics.showDebugRays) return;

        CreateLineMaterial();
        lineMaterial.SetPass(0);
        GL.Begin(GL.LINES);

        DrawSweepGL(floorDebug);
        
        GL.End();
    }

    private void DrawSweepGL(SweepDebugData data)
    {
        if (!data.active || !data.didHit) return;

        float scaleFactor = parentPhysics.transform.lossyScale.y;
        float safetyOffset = 1.0f; 
        float activeRange = parentPhysics.suspensionRestDist * scaleFactor;

        foreach(var hit in data.hits)
        {
            Vector3 hitPos = data.startPos + (data.direction.normalized * hit.distance);
            float distFromMount = hit.distance - (safetyOffset * scaleFactor);

            if (distFromMount <= activeRange)
            {
                // Green Ghost Wheel
                GLDrawWheelOutlineAt(hitPos, data.rotation, new Color(0, 1, 0, 0.4f));

                // Hit Point
                GL.Color(Color.green);
                GLDrawWireSphere(hit.point, 0.05f);
                
                // Normal
                GL.Color(Color.blue);
                GL.Vertex(hit.point); 
                GL.Vertex(hit.point + hit.normal * 0.5f);
            }
        }
    }

    private void GLDrawWheelOutlineAt(Vector3 pos, Quaternion rot, Color color)
    {
        float radius = parentPhysics.wheelRadius;
        float width = parentPhysics.wheelWidth;

        // Manually apply rotation matrix logic for GL drawing
        Vector3 right = rot * Vector3.right;
        Vector3 up = rot * Vector3.up;
        Vector3 fwd = rot * Vector3.forward;

        Vector3 leftRimCenter = pos - right * width * 0.5f;
        Vector3 rightRimCenter = pos + right * width * 0.5f;

        GL.Color(color);

        // Draw Rims
        GLDrawCircle(leftRimCenter, right, radius);
        GLDrawCircle(rightRimCenter, right, radius);

        // Draw Connecting Lines
        GL.Vertex(leftRimCenter + up * radius); GL.Vertex(rightRimCenter + up * radius);
        GL.Vertex(leftRimCenter - up * radius); GL.Vertex(rightRimCenter - up * radius);
        GL.Vertex(leftRimCenter + fwd * radius); GL.Vertex(rightRimCenter + fwd * radius);
        GL.Vertex(leftRimCenter - fwd * radius); GL.Vertex(rightRimCenter - fwd * radius);
    }

    private void GLDrawCircle(Vector3 center, Vector3 normal, float radius)
    {
        int segments = 24;
        float angleStep = 360f / segments;
        Vector3 prev = center + (Quaternion.LookRotation(normal) * Vector3.up * radius);

        for (int i = 1; i <= segments; i++)
        {
            Vector3 next = center + (Quaternion.AngleAxis(angleStep * i, normal) * Quaternion.LookRotation(normal) * Vector3.up * radius);
            GL.Vertex(prev); GL.Vertex(next);
            prev = next;
        }
    }

    private void GLDrawWireSphere(Vector3 center, float radius)
    {
        GLDrawCircle(center, Vector3.up, radius);
        GLDrawCircle(center, Vector3.right, radius);
        GLDrawCircle(center, Vector3.forward, radius);
    }
}