using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum CrossingDirection { Horizontal, Vertical }

public class CrossingGrouper : MonoBehaviour
{
    [SerializeField] private PedestrianCrossing firstCrossing;
    [SerializeField] private PedestrianCrossing secondCrossing;
    
    // Crossing groups that are opposite to the current group
    // for example: if this group is vertical
    // opposite groups will be horizontal
    [SerializeField] private List<CrossingGrouper> oppositeGroups;
    
    // Neighbour group is defined by a separate crossing
    // that is laid on the same vertical or horizontal street
    [SerializeField] private CrossingGrouper neighbourGroup;
    
    [SerializeField] private CrossingDirection crossingDirection;
    private BoxCollider2D groupCollider;
    private List<VehicleController> vehicleHits = new List<VehicleController>();
    
    // Tracks vehicles currently active by either the first or second crossing
    private List<VehicleController> _sharedVehicles = new List<VehicleController>();
    public List<VehicleController> sharedVehicles { get { return _sharedVehicles; } }
    
    private const string VEHICLE_TAG = "Vehicle";

    private void Awake()
    {
        groupCollider = GetComponent<BoxCollider2D>();
    }

    private void Start()
    {
        firstCrossing.crossingGroup = this;
        secondCrossing.crossingGroup = this;
        GameEvents.Crossing_VehicleEntered.Add(VehicleEntered);
        StartCoroutine(CheckCollisions());
    }

    private void OnDestroy()
    {
        GameEvents.Crossing_VehicleEntered.Remove(VehicleEntered);
    }

    /// <summary>
    /// Method <c>CheckCollisions</c> checks for active vehicle collisions inside the crossing group every 0.5 seconds.
    /// </summary>
    private IEnumerator CheckCollisions()
    {
        yield return new WaitForSeconds(0.5f);
        vehicleHits.Clear();
        RaycastHit2D[] hit = Physics2D.BoxCastAll(groupCollider.bounds.center, groupCollider.bounds.size, groupCollider.transform.eulerAngles.z, Vector2.zero);
        for (int i = 0; i < hit.Length; i++)
        {
            if (!hit[i].collider.CompareTag(VEHICLE_TAG)) continue;
            
            VehicleController vehicle = hit[i].collider.GetComponent<VehicleController>();
            // If crossing direction is horizontal, checks for vehicles moving horizontally
            if(crossingDirection == CrossingDirection.Horizontal)
            {
                if(vehicle.forwardVector == Vector2.left || vehicle.forwardVector == Vector2.right)
                {
                    vehicleHits.Add(vehicle);
                }
            }
            // If crossing direction is vertical, checks for vehicles moving vertically
            else if (crossingDirection == CrossingDirection.Vertical)
            {
                if (vehicle.forwardVector == Vector2.up || vehicle.forwardVector == Vector2.down)
                {
                    vehicleHits.Add(vehicle);
                }
            }
        }
        // If a previously shared vehicle no longer gets detected by the raycast
        // means it moved out of range and we can safely remove it
        for(int i = 0; i < sharedVehicles.Count; i++)
        {
            if (!vehicleHits.Contains(sharedVehicles[i]))
            {
                RemoveSharedVehicle(sharedVehicles[i]);
            }
        }
        
        StartCoroutine(CheckCollisions());
    }

    /// <summary>
    /// Method <c>VehicleEntered</c> checks whether vehicles triggered by this event
    /// belong to any crossing of this group,
    /// adds it to the list and disables all opposite crossings.
    /// </summary>
    private void VehicleEntered(PedestrianCrossing crossing, VehicleController vehicle)
    {
        if (crossing != firstCrossing && crossing != secondCrossing) return;
        if (sharedVehicles.Contains(vehicle)) return;
        
        sharedVehicles.Add(vehicle);
        DisableOppositeGroup();
    }

    private void RemoveSharedVehicle(VehicleController vehicle)
    {
        if (!sharedVehicles.Contains(vehicle)) return;
        
        sharedVehicles.Remove(vehicle);
        if (sharedVehicles.Count == 0)
        {
            EnableOppositeGroup();
        }
    }

    private void DisableGroup()
    {
        firstCrossing.SetState(CrossingState.Disabled);
        secondCrossing.SetState(CrossingState.Disabled);
    }

    
    /// <summary>
    /// Method <c>EnableGroup</c> enables all crossings of this group
    /// while disabling opposite crossings if there are vehicles active
    /// to prevent vehicle overlap.
    /// </summary>
    private void EnableGroup()
    {
        firstCrossing.SetState(CrossingState.Neutral);
        secondCrossing.SetState(CrossingState.Neutral);
        if(sharedVehicles.Count > 0)
        {
            DisableOppositeGroup();
        }
    }
    
    private bool IsGroupEnabled()
    {
        return !(firstCrossing.state == CrossingState.Disabled || secondCrossing.state == CrossingState.Disabled);
    }

    private bool CheckGroupVehiclesEmpty()
    {
        return sharedVehicles.Count == 0;
    }
    
    private bool IsNeighbourGroupEmpty()
    {
        return neighbourGroup && neighbourGroup.CheckGroupVehiclesEmpty();
    }

    /// <summary>
    /// Method <c>DisableOppositeGroup</c> disables all opposite crossing groups
    /// if current group has active vehicles.
    /// </summary>
    private void DisableOppositeGroup()
    {
        if (!IsGroupEnabled()) return;
        
        for (int i = 0; i < oppositeGroups.Count; i++)
        {
            oppositeGroups[i].DisableGroup();
        }
    }

    /// <summary>
    /// Method <c>EnableOppositeGroup</c> enables the opposite crossing group (think vertically and horizontally overlapping crossings)
    /// if there are vehicles waiting in line.
    /// </summary>
    private void EnableOppositeGroup()
    {
        if (!CheckGroupVehiclesEmpty() || !IsNeighbourGroupEmpty()) return;
        
        for (int i = 0; i < oppositeGroups.Count; i++)
        {
            oppositeGroups[i].EnableGroup();
        }
    }
}
