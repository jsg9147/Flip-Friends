using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlatformController : RaycastController
{
    public Color previewColor = Color.red;
    [Header("Platform Settings")]
    public LayerMask passengerMask;
    public Vector3[] localWaypoints;
    public float speed;
    public bool cyclic;
    public bool isRepeat;
    public float originalPositionDelay;
    public float waitTime;
    [Range(0, 2)] public float easeAmount;

    private Vector3[] globalWaypoints;
    private int fromWaypointIndex;
    private float percentBetweenWaypoints;
    private List<PassengerMovement> passengerMovement;
    private readonly Dictionary<Transform, Controller2D> passengerDictionary = new Dictionary<Transform, Controller2D>();

    protected bool isWaitingAtStart;
    protected float nextMoveTime;
    protected bool isCompleted;

    public override void Start()
    {
        base.Start();
        InitializeGlobalWaypoints();
    }

    private void InitializeGlobalWaypoints()
    {
        globalWaypoints = new Vector3[localWaypoints.Length];
        for (int i = 0; i < localWaypoints.Length; i++)
        {
            globalWaypoints[i] = localWaypoints[i] + transform.position;
        }
    }

    public virtual void FixedUpdate()
    {
        if (!isServer) return;
        UpdateRaycastOrigins();
        MovePlatform();
        RpcSyncPosition(transform.position);
    }

    private void MovePlatform()
    {
        Vector3 velocity = CalculatePlatformMovement();
        CalculatePassengerMovement(velocity);
        MovePassengers(true);
        transform.Translate(velocity, Space.World);
        MovePassengers(false);
    }

    [ClientRpc]
    public void RpcSyncPosition(Vector3 position)
    {
        transform.position = position;
    }

    private float Ease(float t)
    {
        float a = easeAmount + 1;
        return Mathf.Pow(t, a) / (Mathf.Pow(t, a) + Mathf.Pow(1 - t, a));
    }

    private Vector3 CalculatePlatformMovement()
    {
        if (Time.time < nextMoveTime) return Vector3.zero;

        fromWaypointIndex %= globalWaypoints.Length;
        int toWaypointIndex = (fromWaypointIndex + 1) % globalWaypoints.Length;

        percentBetweenWaypoints = Mathf.Clamp01(percentBetweenWaypoints + Time.deltaTime * speed);
        float easedPercent = Ease(percentBetweenWaypoints);
        Vector3 nextPosition = Vector3.Lerp(globalWaypoints[fromWaypointIndex], globalWaypoints[toWaypointIndex], easedPercent);

        if (percentBetweenWaypoints >= 1)
        {
            OnWaypointReached(toWaypointIndex);
        }

        return nextPosition - transform.position;
    }

    private void OnWaypointReached(int toWaypointIndex)
    {
        percentBetweenWaypoints = 0;
        fromWaypointIndex++;

        if (fromWaypointIndex >= globalWaypoints.Length - 1)
        {
            HandleCompletion();
        }

        nextMoveTime = Time.time + waitTime;
    }

    private void HandleCompletion()
    {
        fromWaypointIndex = 0;

        if (cyclic)
        {
            System.Array.Reverse(globalWaypoints);
        }
        else if (!isRepeat)
        {
            isCompleted = true;
            StartCoroutine(ReturnToOriginalPositionAfterDelay());
        }
    }

    private IEnumerator ReturnToOriginalPositionAfterDelay()
    {
        yield return new WaitForSeconds(originalPositionDelay);
        transform.position = globalWaypoints[0];
        isWaitingAtStart = true;
    }

    private void MovePassengers(bool beforeMovePlatform)
    {
        foreach (PassengerMovement passenger in passengerMovement)
        {
            if (!passengerDictionary.TryGetValue(passenger.transform, out Controller2D controller))
            {
                controller = passenger.transform.GetComponent<Controller2D>();
                passengerDictionary[passenger.transform] = controller;
            }
            if (passenger.moveBeforePlatform == beforeMovePlatform)
            {
                controller.Move(passenger.velocity, passenger.standingOnPlatform);
            }
        }
    }

    private void CalculatePassengerMovement(Vector3 velocity)
    {
        passengerMovement = new List<PassengerMovement>();
        HashSet<Transform> movedPassengers = new HashSet<Transform>();

        HandleVerticalMovement(velocity, movedPassengers);
        HandleHorizontalMovement(velocity, movedPassengers);
        HandleTopMovement(velocity, movedPassengers);
    }

    private void HandleVerticalMovement(Vector3 velocity, HashSet<Transform> movedPassengers)
    {
        if (velocity.y == 0) return;

        float directionY = Mathf.Sign(velocity.y);
        float rayLength = Mathf.Abs(velocity.y) + skinWidth;

        for (int i = 0; i < verticalRayCount; i++)
        {
            Vector2 rayOrigin = (directionY == -1) ? raycastOrigins.bottomLeft : raycastOrigins.topLeft;
            rayOrigin += Vector2.right * (verticalRaySpacing * i);

            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.up * directionY, rayLength, passengerMask);

            if (hit && !movedPassengers.Contains(hit.transform))
            {
                movedPassengers.Add(hit.transform);
                passengerMovement.Add(CreatePassengerMovement(hit, velocity, directionY));
            }
        }
    }

    private void HandleHorizontalMovement(Vector3 velocity, HashSet<Transform> movedPassengers)
    {
        if (velocity.x == 0) return;

        float directionX = Mathf.Sign(velocity.x);
        float rayLength = Mathf.Abs(velocity.x) + skinWidth;

        for (int i = 0; i < horizontalRayCount; i++)
        {
            Vector2 rayOrigin = (directionX == -1) ? raycastOrigins.bottomLeft : raycastOrigins.bottomRight;
            rayOrigin += Vector2.up * (horizontalRaySpacing * i);

            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.right * directionX, rayLength, passengerMask);

            if (hit && !movedPassengers.Contains(hit.transform))
            {
                movedPassengers.Add(hit.transform);
                passengerMovement.Add(CreatePassengerMovement(hit, velocity, directionX));
            }
        }
    }

    private void HandleTopMovement(Vector3 velocity, HashSet<Transform> movedPassengers)
    {
        float rayLength = Mathf.Abs(velocity.y) + skinWidth * 2;
        for (int i = 0; i < verticalRayCount; i++)
        {
            Vector2 rayOrigin = raycastOrigins.topLeft + Vector2.right * (verticalRaySpacing * i);

            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.up, rayLength, passengerMask);
            Debug.DrawRay(rayOrigin, Vector2.up * 10, Color.blue);

            if (hit && !movedPassengers.Contains(hit.transform))
            {
                movedPassengers.Add(hit.transform);
                passengerMovement.Add(new PassengerMovement(hit.transform, velocity, true, false));
            }
        }
    }

    private PassengerMovement CreatePassengerMovement(RaycastHit2D hit, Vector3 velocity, float direction)
    {
        float pushX = (direction == 1) ? velocity.x : 0;
        float pushY = velocity.y - (hit.distance - skinWidth) * direction;

        return new PassengerMovement(hit.transform, new Vector3(pushX, pushY), direction == 1, true);
    }

    private void OnDrawGizmos()
    {
        if (localWaypoints == null) return;

        Gizmos.color = previewColor;
        float size = 0.3f;

        foreach (Vector3 waypoint in localWaypoints)
        {
            Vector3 globalPosition = Application.isPlaying ? globalWaypoints[Array.IndexOf(localWaypoints, waypoint)] : waypoint + transform.position;
            Gizmos.DrawLine(globalPosition - Vector3.up * size, globalPosition + Vector3.up * size);
            Gizmos.DrawLine(globalPosition - Vector3.left * size, globalPosition + Vector3.left * size);
        }
    }

    private struct PassengerMovement
    {
        public Transform transform;
        public Vector3 velocity;
        public bool standingOnPlatform;
        public bool moveBeforePlatform;

        public PassengerMovement(Transform transform, Vector3 velocity, bool standingOnPlatform, bool moveBeforePlatform)
        {
            this.transform = transform;
            this.velocity = velocity;
            this.standingOnPlatform = standingOnPlatform;
            this.moveBeforePlatform = moveBeforePlatform;
        }
    }
}
