using UnityEngine;

namespace EcclesiaFighters
{
    /// <summary>
    /// Dynamic 2.5D fighting camera.
    /// Smoothly tracks the midpoint between Fighter A and Fighter B along World X,
    /// subtly follows jumps on Y, zooms Z-distance dynamically based on fighter separation,
    /// clamps camera X inside the stage bounds, and enforces maxFighterSeparation.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class FighterCamera : MonoBehaviour
    {
        [Header("Targets")]
        public Transform fighterA;
        public Transform fighterB;

        [Header("Framing & Dynamic Zoom")]
        public float minDistance          = 5.2f;
        public float maxDistance          = 9.2f;
        public float minSeparationForZoom = 2.0f;
        public float maxFighterSeparation = 10.5f;
        public float baseHeight           = 1.70f;
        [Range(0f, 1f)] public float verticalTrackingWeight = 0.35f;
        public float pitchDegrees         = 4.0f;

        [Header("Smoothing")]
        public float horizontalSmoothTime = 0.14f;
        public float verticalSmoothTime   = 0.18f;
        public float zoomSmoothTime       = 0.16f;

        [Header("Stage Bounds (Driven by ChurchStageController)")]
        public float minCameraX      = -5.5f;
        public float maxCameraX      =  5.5f;
        public float floorY          = -0.465f;
        public float stageLeftWallX  = -9.5f;
        public float stageRightWallX =  9.5f;
        public float heightOffset    =  0f;
        public float distanceOffset  =  0f;

        float velX, velY, velZ;

        public void SetTargets(Transform a, Transform b, bool snapImmediate = true)
        {
            fighterA = a;
            fighterB = b;
            if (snapImmediate) SnapToTargets();
        }

        public void ApplyStagePreset(ChurchStageController.StagePreset preset)
        {
            floorY          = preset.floorY;
            stageLeftWallX  = preset.leftWallX;
            stageRightWallX = preset.rightWallX;
            minCameraX      = Mathf.Min(preset.minCameraX, preset.maxCameraX);
            maxCameraX      = Mathf.Max(preset.minCameraX, preset.maxCameraX);
            heightOffset    = preset.cameraHeightOffset;
            distanceOffset  = preset.cameraDistanceOffset;
        }

        public void SnapToTargets()
        {
            EnforceMaxSeparation();
            Vector3 target = ComputeTargetPosition();
            transform.position = target;
            transform.rotation = Quaternion.Euler(pitchDegrees, 0f, 0f);
            velX = velY = velZ = 0f;
        }

        void LateUpdate()
        {
            if (!fighterA || !fighterB) return;

            EnforceMaxSeparation();

            Vector3 target = ComputeTargetPosition();
            Vector3 cur    = transform.position;

            float nx = Mathf.SmoothDamp(cur.x, target.x, ref velX, Mathf.Max(0.01f, horizontalSmoothTime));
            float ny = Mathf.SmoothDamp(cur.y, target.y, ref velY, Mathf.Max(0.01f, verticalSmoothTime));
            float nz = Mathf.SmoothDamp(cur.z, target.z, ref velZ, Mathf.Max(0.01f, zoomSmoothTime));

            transform.position = new Vector3(nx, ny, nz);
            transform.rotation = Quaternion.Euler(pitchDegrees, 0f, 0f);
        }

        void EnforceMaxSeparation()
        {
            if (!fighterA || !fighterB || maxFighterSeparation <= 0.5f) return;

            float ax = fighterA.position.x;
            float bx = fighterB.position.x;
            float dx = bx - ax;
            if (Mathf.Abs(dx) <= maxFighterSeparation) return;

            float mid     = 0.5f * (ax + bx);
            float halfSep = maxFighterSeparation * 0.5f;
            float sign    = dx >= 0f ? 1f : -1f;

            float newAX = mid - sign * halfSep;
            float newBX = mid + sign * halfSep;

            float wallMin = stageLeftWallX + 0.45f;
            float wallMax = stageRightWallX - 0.45f;
            if (wallMin < wallMax)
            {
                if (newAX < wallMin)      { float s = wallMin - newAX; newAX = wallMin; newBX = Mathf.Min(wallMax, newBX + s); }
                else if (newAX > wallMax) { float s = newAX - wallMax; newAX = wallMax; newBX = Mathf.Max(wallMin, newBX - s); }

                if (newBX < wallMin)      { float s = wallMin - newBX; newBX = wallMin; newAX = Mathf.Min(wallMax, newAX + s); }
                else if (newBX > wallMax) { float s = newBX - wallMax; newBX = wallMax; newAX = Mathf.Max(wallMin, newAX - s); }
            }

            Vector3 posA = fighterA.position; posA.x = newAX; fighterA.position = posA;
            Vector3 posB = fighterB.position; posB.x = newBX; fighterB.position = posB;
        }

        Vector3 ComputeTargetPosition()
        {
            if (!fighterA || !fighterB)
            {
                float fallbackX = Mathf.Clamp(0f, minCameraX, maxCameraX);
                float fallbackZ = -Mathf.Max(2.5f, minDistance + distanceOffset);
                return new Vector3(fallbackX, floorY + baseHeight + heightOffset, fallbackZ);
            }

            float midX     = 0.5f * (fighterA.position.x + fighterB.position.x);
            float clampedX = Mathf.Clamp(midX, minCameraX, maxCameraX);

            float avgY         = 0.5f * (fighterA.position.y + fighterB.position.y);
            float standingMidY = floorY + 0.965f;
            float jumpOffset   = Mathf.Max(0f, avgY - standingMidY) * verticalTrackingWeight;
            float targetY      = floorY + baseHeight + heightOffset + jumpOffset;

            float sep        = Mathf.Abs(fighterA.position.x - fighterB.position.x);
            float zoomT      = Mathf.InverseLerp(minSeparationForZoom, Mathf.Max(minSeparationForZoom + 0.1f, maxFighterSeparation), sep);
            float targetDist = Mathf.Lerp(minDistance, maxDistance, zoomT) + distanceOffset;

            return new Vector3(clampedX, targetY, -Mathf.Max(2.5f, targetDist));
        }
    }
}
