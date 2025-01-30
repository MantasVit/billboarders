using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

public enum CrossingState
{
    Neutral, Active, Disabled
}

public class PedestrianCrossing : MonoBehaviour
{
    [SerializeField] private BoxCollider2D crossing;
    
    // Colliders for curbs on each side of the crossing
    [SerializeField] private BoxCollider2D curbOne;
    [SerializeField] private BoxCollider2D curbTwo;
    
    // Agents which are overlapping with the crossing
    private List<NPCController> overlappingAgents = new List<NPCController>();
    
    // Agents which are waiting by the curb
    private List<NPCController> waitingAgents = new List<NPCController>();
    
    private List<VehicleController> vehicleHits = new List<VehicleController>();
    private HashSet<NPCController> npcHits = new HashSet<NPCController>();
    
    // Vehicles which are overlapping with the crossing
    private List<VehicleController> overlappedVehicles = new List<VehicleController>();

    private CrossingState _state = CrossingState.Neutral;
    public CrossingState state { get { return _state; } set { _state = value; } }

    // Crossing group that the current crossing is a part of
    private CrossingGrouper _crossingGroup = null;
    public CrossingGrouper crossingGroup { get { return _crossingGroup; } set { _crossingGroup = value; } }
    
    private const string VEHICLE_TAG = "Vehicle";
    private const string NPC_TAG = "NPC";
    private const string REWARDNPC_TAG = "Reward NPC";

    private void Start()
    {
        StartCoroutine(CheckCollisions());
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag(VEHICLE_TAG))
        {
            VehicleController vehicle = collision.GetComponent<VehicleController>();
            if (AddOverlappingVehicle(vehicle))
            {
                // Send this event to notify the crossing group that a new vehicle has been added
                GameEvents.Crossing_VehicleEntered.Raise(this, vehicle);
            }
            if (CrossingInUse() || state != CrossingState.Active)
            {
                vehicle.SetState(VehicleState.Waiting);
            }
            SetState(CrossingState.Active);
        }

        if (!collision.CompareTag(NPC_TAG) && !collision.CompareTag(REWARDNPC_TAG)) return;
        
        if (crossingGroup && crossingGroup.sharedVehicles.Count > 0 && state == CrossingState.Neutral || overlappedVehicles.Count > 0 && state == CrossingState.Neutral)
        {
            SetState(CrossingState.Active);
        }
        // Make all new overlapping agents wait at the curb if there are overlapping vehicles and crossing is active
        if (overlappedVehicles.Count > 0 && state == CrossingState.Active)
        {
            MakeAgentWait(collision.GetComponent<NPCController>());
        }
        // Check to confirm if there are currently no active vehicles on either of the two crossings
        // that are shared by the group
        else if(crossingGroup && crossingGroup.sharedVehicles.Count == 0 || state != CrossingState.Active || overlappedVehicles.Count == 0 && state == CrossingState.Active)
        {
            overlappingAgents.Add(collision.GetComponent<NPCController>());
        }
    }

    /// <summary>
    /// Method <c>CheckCollisions</c> checks for active vehicle and NPC collisions
    /// inside the crossing every 0.5 seconds.
    /// </summary>
    private IEnumerator CheckCollisions()
    {
        yield return new WaitForSeconds(0.5f);
        vehicleHits.Clear();
        npcHits.Clear();
        RaycastHit2D[] hit = Physics2D.BoxCastAll(crossing.bounds.center, crossing.bounds.size * 1.05f, crossing.transform.eulerAngles.z, Vector2.zero);
        for(int i = 0; i < hit.Length; i++)
        {
            if (hit[i].collider.CompareTag(VEHICLE_TAG))
            {
                vehicleHits.Add(hit[i].collider.GetComponent<VehicleController>());
            }
            else if (hit[i].collider.CompareTag(NPC_TAG) || hit[i].collider.CompareTag(REWARDNPC_TAG))
            {
                npcHits.Add(hit[i].collider.GetComponent<NPCController>());
            }
        }
        // Adjust overlapping vehicles if they are no longer within collision range
        for(int i = 0; i < overlappedVehicles.Count; i++)
        {
            if (!vehicleHits.Contains(overlappedVehicles[i]))
            {
                RemoveOverlappedVehicle(overlappedVehicles[i]);
            }
        }
        // Adjust overlapping agents if they are no longer within collision range
        for (int i = 0; i < overlappingAgents.Count; i++)
        {
            if (!npcHits.Contains(overlappingAgents[i]))
            {
                RemoveOverlappingAgent(overlappingAgents[i]);
            }
        }
        
        StartCoroutine(CheckCollisions());
    }

    private bool CrossingInUse()
    {
        return overlappingAgents.Count > 0;
    }

    private Vector3 GetRandomPointOnCurb(BoxCollider2D curb)
    {
        return new Vector3(Random.Range(curb.bounds.min.x, curb.bounds.max.x), Random.Range(curb.bounds.min.y, curb.bounds.max.y), 0);
    }

    /// <summary>
    /// Method <c>FindNearestCurb</c> finds the closest curb relative to the agents position.
    /// </summary>
    private Vector3 FindNearestCurb(NavMeshAgent agent)
    {
        float distance1 = Vector3.Distance(agent.transform.position, curbOne.transform.position);
        float distance2 = Vector3.Distance(agent.transform.position, curbTwo.transform.position);
        
        return GetRandomPointOnCurb(distance1 < distance2 ? curbOne : curbTwo);
    }

    private void RemoveOverlappingAgent(NPCController agent)
    {
        if (!overlappingAgents.Contains(agent)) return;
        
        overlappingAgents.Remove(agent);
        if (overlappingAgents.Count == 0)
        {
            SetState(CrossingState.Active);
        }
    }

    private void MakeAgentWait(NPCController agent)
    {
        if (state != CrossingState.Active) return;
        
        waitingAgents.Add(agent);
        agent.WalkToCurb(FindNearestCurb(agent.agent));
        if (overlappingAgents.Contains(agent))
        {
            overlappingAgents.Remove(agent);
        }
    }

    private void MoveWaitingAgents()
    {
        for (int i = 0; i < waitingAgents.Count; i++)
        {
            waitingAgents[i].ResetDestination();
            overlappingAgents.Add(waitingAgents[i]);
        }
        waitingAgents.Clear();
    }
    
    private void MoveVehicles()
    {
        if (state != CrossingState.Active || overlappedVehicles.Count == 0 || CrossingInUse()) return;
        
        for (int i = 0; i < overlappedVehicles.Count; i++)
        {
            overlappedVehicles[i].SetState(VehicleState.Moving);
        }
    }

    private bool AddOverlappingVehicle(VehicleController vehicle)
    {
        if (overlappedVehicles.Contains(vehicle)) return false;
        
        overlappedVehicles.Add(vehicle);
        return true;
    }

    private void RemoveOverlappedVehicle(VehicleController vehicle)
    {
        if (!overlappedVehicles.Contains(vehicle)) return;
        
        overlappedVehicles.Remove(vehicle);
        if (overlappedVehicles.Count == 0)
        {
            MoveWaitingAgents();
        }
    }

    /// <summary>
    /// Method <c>SetState</c> changes the current state of the crossing
    /// and starts moving vehicles or NPCs if needed.
    /// </summary>
    public void SetState(CrossingState _state)
    {
        switch (_state)
        {
            case CrossingState.Active when state != CrossingState.Disabled:
                state = CrossingState.Active;
                MoveVehicles();
                break;
            case CrossingState.Neutral:
                state = CrossingState.Neutral;
                if (crossingGroup && crossingGroup.sharedVehicles.Count > 0 && overlappingAgents.Count == 0 || overlappedVehicles.Count > 0 && overlappingAgents.Count == 0)
                {
                    SetState(CrossingState.Active);
                }
                break;
            case CrossingState.Disabled:
                state = CrossingState.Disabled;
                MoveWaitingAgents();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(_state), _state, null);
        }
    }
}