using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class Vehicle : MonoBehaviour
{
    [Header("--- Debugging ---")]
    public bool showDebugRays = true;

    [Header("--- Layer Setup ---")]
    public LayerMask drivableLayers;

    [Header("--- Car Specs ---")]
    public float carMass = 1500f;
    public Transform centerOfMass;

    [Header("--- Arcade Power & Handling ---")]
    public float accelerationForce = 15000f;  
    public float brakeForce = 20000f;          
    public float steerAngle = 35f;
    
    [Range(0f, 1f)]
    public float tireGripFactor = 0.8f; 

    [Tooltip("X-axis: Slip Percentage (0-1), Y-axis: Friction Multiplier (0-1)")]
    public AnimationCurve gripCurve = AnimationCurve.Linear(0, 1, 0.5f, 0.2f); 
    
    public bool invertMotor = false;
    public float maxSpeedKph = 250f;

    [Header("--- Suspension ---")]
    public float suspensionRestDist = 1.2f;
    public float springStiffness = 35000f;
    public float damperStiffness = 2500f;

    [Header("--- Visuals ---")]
    public float suspensionSmoothing = 25f;
    public float wheelRadius = 0.4f;
    public float wheelWidth = 0.4f; 

    [Header("--- Wheels ---")]
    public List<WheelData> wheels = new List<WheelData>();

    private Rigidbody carRb;
    private float moveInput;
    private float steerInput;
    private bool brakeInput;

    private const float suspensionSafetyOffset = 1.0f;
    private static Material lineMaterial;

    [System.Serializable]
    public class WheelData
    {
        public string name = "Wheel";
        public Wheel SWheelScript;
        public bool isSteering;
        public bool isMotor;
        public Vector3 suspensionOffset = new Vector3(0, 0f, 0);

        [HideInInspector] public Vector3 initialLocalPos;
        [HideInInspector] public float currentRotation;
        [HideInInspector] public float currentExtension;
    }

    void Start()
    {
        carRb = GetComponent<Rigidbody>();
        carRb.mass = carMass;
        carRb.interpolation = RigidbodyInterpolation.Interpolate;
        carRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (centerOfMass) carRb.centerOfMass = centerOfMass.localPosition;
        else carRb.centerOfMass = new Vector3(0, -0.25f, 0);

        if (wheels.Count > 0 && wheels[0].SWheelScript != null)
        {
            var col = wheels[0].SWheelScript.GetComponentInChildren<Collider>();
            if (col) 
            {
                wheelRadius = col.bounds.extents.y;
                wheelWidth = col.bounds.size.x;
            }
        }

        foreach (var w in wheels)
        {
            if (w.SWheelScript)
            {
                w.initialLocalPos = w.SWheelScript.transform.localPosition;
                w.SWheelScript.Initialize(transform);
                w.currentExtension = suspensionRestDist;
            }
        }
    }

    void Update()
    {
        float rawMove = Input.GetAxis("Vertical");
        moveInput = invertMotor ? -rawMove : rawMove;
        steerInput = Input.GetAxis("Horizontal");
        brakeInput = Input.GetKey(KeyCode.Space);

        foreach (var w in wheels)
        {
            if (w.SWheelScript == null) continue;

            Vector3 localStart = w.initialLocalPos + w.suspensionOffset;
            Vector3 visualPos = transform.TransformPoint(localStart - Vector3.up * w.currentExtension);

            float currentSteer = w.isSteering ? steerInput * steerAngle : 0f;
            Quaternion steerRot = transform.rotation * Quaternion.Euler(0, currentSteer, 0);
            Quaternion spinRot = steerRot * Quaternion.Euler(w.currentRotation, 0, 0);

            w.SWheelScript.UpdateVisuals(visualPos, spinRot);
        }
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        float scaleFactor = transform.lossyScale.y; 

        foreach (var w in wheels)
        {
            if (w.SWheelScript == null) continue;

            Vector3 localMount = w.initialLocalPos + w.suspensionOffset;
            
            float currentSteer = w.isSteering ? steerInput * steerAngle : 0f;
            Quaternion steerRot = transform.rotation * Quaternion.Euler(0, currentSteer, 0);

            Vector3 safeOrigin = transform.TransformPoint(localMount + (Vector3.up * suspensionSafetyOffset));
            Vector3 downDir = -transform.up;
            float totalScanDistance = (suspensionRestDist + suspensionSafetyOffset + 0.5f) * scaleFactor;

            RaycastHit[] hits = w.SWheelScript.PerformMeshSweepMulti(safeOrigin, downDir, totalScanDistance, drivableLayers, true);
            
            List<RaycastHit> validContacts = new List<RaycastHit>();
            float minHitDistance = float.MaxValue; 

            for(int i=0; i < hits.Length; i++)
            {
                float trueHitDist = hits[i].distance - (suspensionSafetyOffset * scaleFactor);
                if(trueHitDist <= (suspensionRestDist * scaleFactor))
                {
                    validContacts.Add(hits[i]);
                    if(hits[i].distance < minHitDistance) minHitDistance = hits[i].distance;
                }
            }

            bool grounded = validContacts.Count > 0;

            if (grounded)
            {
                float rawDist = minHitDistance - (suspensionSafetyOffset * scaleFactor);
                float localTargetDist = rawDist / scaleFactor;
                localTargetDist = Mathf.Clamp(localTargetDist, 0.05f, suspensionRestDist);

                float targetSmoothing = suspensionSmoothing;
                if (localTargetDist < w.currentExtension) targetSmoothing *= 3f; 
                w.currentExtension = Mathf.Lerp(w.currentExtension, localTargetDist, dt * targetSmoothing);

                float forceShare = 1.0f / validContacts.Count;

                foreach(var hit in validContacts)
                {
                    ApplySuspensionForceOnly(w, hit, scaleFactor, forceShare);
                    ApplyArcadeTraction(w, hit, steerRot, dt, forceShare);
                }
                
                float localVelZ = transform.InverseTransformDirection(carRb.velocity).z;
                w.currentRotation += (localVelZ / wheelRadius) * Mathf.Rad2Deg * dt;
            }
            else
            {
                w.currentExtension = Mathf.Lerp(w.currentExtension, suspensionRestDist, dt * suspensionSmoothing);
                
                if (w.isMotor && moveInput != 0) 
                {
                    w.currentRotation += moveInput * 1000f * dt;
                }
            }
        }
    }

    void ApplySuspensionForceOnly(WheelData w, RaycastHit hit, float scaleFactor, float forceFactor)
    {
        float dist = hit.distance - (suspensionSafetyOffset * scaleFactor);
        float scaledRestDist = suspensionRestDist * scaleFactor;
        
        float compression = 1.0f - (dist / scaledRestDist);

        if (compression <= 0f) return;

        float springForce = compression * springStiffness;
        Vector3 velAtHit = carRb.GetPointVelocity(hit.point);
        float verticalSpeed = Vector3.Dot(transform.up, velAtHit);
        float dampForce = -verticalSpeed * damperStiffness;
        
        float totalForce = Mathf.Max(0, springForce + dampForce);
        
        Vector3 forceDir = transform.up;
        float rampFactor = Vector3.Dot(transform.up, hit.normal);
        if (rampFactor < 0.1f) rampFactor = 0.1f;

        carRb.AddForceAtPosition(forceDir * totalForce * forceFactor, hit.point);
    }

    void ApplyArcadeTraction(WheelData w, RaycastHit hit, Quaternion steerRot, float dt, float forceFactor)
    {
        Vector3 tireVelocity = carRb.GetPointVelocity(hit.point);
        Vector3 wheelFwd = steerRot * Vector3.forward; 
        Vector3 wheelRight = steerRot * Vector3.right;

        float steeringVel = Vector3.Dot(tireVelocity, wheelRight);
        float totalMag = tireVelocity.magnitude;
        float slipRatio = (totalMag > 0.1f) ? Mathf.Abs(steeringVel) / totalMag : 0f;
        float currentGrip = gripCurve.Evaluate(slipRatio) * tireGripFactor;

        float desiredVelChange = -steeringVel * currentGrip;
        float desiredAccel = desiredVelChange / dt;
        float lateralForce = (carMass / wheels.Count) * desiredAccel;

        carRb.AddForceAtPosition(wheelRight * lateralForce * forceFactor, hit.point);

        float currentFwdSpeed = Vector3.Dot(tireVelocity, wheelFwd);
        
        if (brakeInput)
        {
            float brakeDir = (currentFwdSpeed > 0) ? -1 : 1;
            carRb.AddForceAtPosition(wheelFwd * brakeDir * brakeForce * forceFactor, hit.point);
        }
        else if (w.isMotor && moveInput != 0)
        {
            if (Mathf.Abs(currentFwdSpeed) < (maxSpeedKph / 3.6f))
            {
                carRb.AddForceAtPosition(wheelFwd * moveInput * accelerationForce * forceFactor, hit.point);
            }
        }
    }

    // --- RUNTIME GIZMOS START ---
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

    // Replaces OnDrawGizmos with OnRenderObject (Works in Builds)
    private void OnRenderObject()
    {
        if (!showDebugRays) return;
        
        CreateLineMaterial();
        lineMaterial.SetPass(0);

        GL.Begin(GL.LINES);

        if (carRb)
        {
            GL.Color(Color.cyan);
            GLDrawWireSphere(transform.TransformPoint(carRb.centerOfMass), 0.1f);
        }

        float scale = transform.lossyScale.y;

        foreach (var w in wheels)
        {
            Vector3 mountPoint = transform.TransformPoint(w.initialLocalPos + w.suspensionOffset);
            Vector3 maxExtensionPoint = mountPoint + (-transform.up * suspensionRestDist * scale);
            Vector3 currentWheelPos = mountPoint + (-transform.up * w.currentExtension);

            float compression = 1.0f - (w.currentExtension / (suspensionRestDist * scale));
            compression = Mathf.Clamp01(compression);

            Color strutColor = Color.Lerp(Color.blue, Color.green, compression); 
            if (compression > 0.8f) strutColor = Color.Lerp(Color.green, Color.red, (compression - 0.8f) * 5f); 

            // Anchors
            GL.Color(Color.white);
            GLDrawWireSphere(mountPoint, 0.05f);
            
            // Max Travel Ghost
            GL.Color(new Color(1, 1, 1, 0.15f)); 
            GL.Vertex(mountPoint); GL.Vertex(maxExtensionPoint);
            GLDrawWireCube(maxExtensionPoint, Vector3.one * 0.1f);

            // Active Strut
            GL.Color(strutColor);
            GL.Vertex(mountPoint); GL.Vertex(currentWheelPos);
            // Thick line trick
            Vector3 camRight = Camera.current != null ? Camera.current.transform.right : Vector3.right;
            Vector3 offset = camRight * 0.03f;
            GL.Vertex(mountPoint + offset); GL.Vertex(currentWheelPos + offset);

            GLDrawWireSphere(currentWheelPos, 0.1f);

            // Compression Meter
            float sideDir = (w.initialLocalPos.x >= 0) ? 1f : -1f;
            Vector3 outwardDir = transform.right * sideDir;
            Vector3 barStart = currentWheelPos + (outwardDir * 0.6f * scale);
            Vector3 barUp = transform.up * 0.5f * scale;
            
            GL.Color(Color.black);
            GL.Vertex(barStart); GL.Vertex(barStart + barUp);

            GL.Color(strutColor);
            GL.Vertex(barStart); GL.Vertex(barStart + (barUp * compression));
            GLDrawWireCube(barStart + (barUp * compression), Vector3.one * 0.05f);
        }

        GL.End();
    }

    // --- GL HELPER FUNCTIONS ---
    void GLDrawWireSphere(Vector3 center, float radius)
    {
        // Approximation: 3 circles
        GLDrawCircle(center, Vector3.up, radius);
        GLDrawCircle(center, Vector3.right, radius);
        GLDrawCircle(center, Vector3.forward, radius);
    }

    void GLDrawWireCube(Vector3 center, Vector3 size)
    {
        Vector3 half = size * 0.5f;
        Vector3[] p = new Vector3[8];
        p[0] = center + new Vector3(-half.x, -half.y, -half.z);
        p[1] = center + new Vector3( half.x, -half.y, -half.z);
        p[2] = center + new Vector3( half.x,  half.y, -half.z);
        p[3] = center + new Vector3(-half.x,  half.y, -half.z);
        p[4] = center + new Vector3(-half.x, -half.y,  half.z);
        p[5] = center + new Vector3( half.x, -half.y,  half.z);
        p[6] = center + new Vector3( half.x,  half.y,  half.z);
        p[7] = center + new Vector3(-half.x,  half.y,  half.z);

        // Bottom
        GL.Vertex(p[0]); GL.Vertex(p[1]);
        GL.Vertex(p[1]); GL.Vertex(p[2]);
        GL.Vertex(p[2]); GL.Vertex(p[3]);
        GL.Vertex(p[3]); GL.Vertex(p[0]);
        // Top
        GL.Vertex(p[4]); GL.Vertex(p[5]);
        GL.Vertex(p[5]); GL.Vertex(p[6]);
        GL.Vertex(p[6]); GL.Vertex(p[7]);
        GL.Vertex(p[7]); GL.Vertex(p[4]);
        // Sides
        GL.Vertex(p[0]); GL.Vertex(p[4]);
        GL.Vertex(p[1]); GL.Vertex(p[5]);
        GL.Vertex(p[2]); GL.Vertex(p[6]);
        GL.Vertex(p[3]); GL.Vertex(p[7]);
    }

    void GLDrawCircle(Vector3 center, Vector3 normal, float radius)
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
}