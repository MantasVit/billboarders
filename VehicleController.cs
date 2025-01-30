using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

public enum VehicleState
{
    Moving, Waiting
}

public enum VehicleDirection
{
    Up, Down, Left, Right
}

public class VehicleController : MonoBehaviour
{
    // --------------- References to each vehicle side --------------------
    [SerializeField] private GameObject side;
    [SerializeField] private GameObject back;
    [SerializeField] private GameObject front;
    
    // --------------- References to wheels --------------------
    [SerializeField] private GameObject wheelLeft;
    [SerializeField] private GameObject wheelRight;

    private Vector3 _targetPosition;
    public Vector3 targetPosition {  get { return _targetPosition; } set { _targetPosition = value; } }

    private VehicleState state = VehicleState.Moving;
    
    [SerializeField] private float vehicleSpeed;

    private int xScale = 1;
    private new BoxCollider2D collider;
    private bool canMove = true;
    private Vector2 _forwardVector;
    public Vector2 forwardVector { get { return _forwardVector; } private set { _forwardVector = value; } }
    [SerializeField] private AudioSource audioIdle;
    [SerializeField] private AudioSource audioDriving;
    private Animator animator;
    private readonly float audioMinDistance = 5;
    private readonly float audioMaxDistance = 20;
    private BillboardController activeBillboard;
    private PassiveUpgradeManager passiveUpgradeManager { get { return GameManager.Instance.player.passiveUpgradeManager; } }
    private StageUpgradeManager stageUpgradeManager { get { return GameManager.Instance.stageManager.stageUpgradeManager; } }
    private const string VEHICLE_TAG = "Vehicle";
    private const string BILLBOARD_TAG = "Billboard";
    private static readonly int AnimDestroy = Animator.StringToHash("Destroy");
    private static readonly int AnimRiding = Animator.StringToHash("Riding");
    private static readonly int AnimIdle = Animator.StringToHash("Idle");

    private void Awake()
    {
        collider = gameObject.AddComponent<BoxCollider2D>();
    }
    
    protected virtual void Update()
    {
        ScaleVehicle();
        AdjustAudioVolume();
        MoveVehicle();
    }

    public void OnTriggerEnter2D(Collider2D collision)
    {
        // If the object entering this trigger is a vehicle
        // checks if there is a current collision based on the forward vector
        // if so, stop the vehicle
        if (collision.gameObject.CompareTag(VEHICLE_TAG))
        {
            Vector2 objectPosition = transform.position;
            Vector2 collisionPosition = collision.transform.position;
            Vector2 directionToCollision = (collisionPosition - objectPosition).normalized;
            float dotProduct = Vector2.Dot(directionToCollision, forwardVector.normalized);
            if (dotProduct > 0.9f)
            {
                canMove = false;
            }
        }

        if (stageUpgradeManager.GetAttributeValue(StageUpgradeAttribute.VehicleReceiveProfit) != 1 ||
            !collision.gameObject.CompareTag(BILLBOARD_TAG))
        {
            return;
        }
        
        // If there is an interaction with a billboard
        // check whether there is eligibility to receive a reward
        activeBillboard = collision.GetComponentInParent<BillboardController>();
        if (activeBillboard != null && activeBillboard.isPurchased)
        {
            StartCoroutine(InteractionDelay());
        }
    }
    
    public void OnTriggerExit2D(Collider2D collision)
    {
        // If the last object to leave this trigger is a vehicle
        // checks to see if there is no longer a front facing collision
        // if so, the vehicle can continue moving forward
        if (!collision.gameObject.CompareTag(VEHICLE_TAG)) return;
        
        Vector2 objectPosition = transform.position;
        Vector2 collisionPosition = collision.transform.position;
        Vector2 directionToCollision = (collisionPosition - objectPosition).normalized;
        float dotProduct = Vector2.Dot(directionToCollision, forwardVector.normalized);
        if (dotProduct > 0.9f)
        {
            canMove = true;
        }
    }

    /// <summary>
    /// Method <c>GrantReward</c> checks to see whether the reward chance has been hit.
    /// </summary>
    private void GrantReward()
    {
        if (!(activeBillboard.watchRate > Random.Range(1, 101))) return;
        
        GameEvents.Vehicle_ShowRewardBar.Raise(this);
        activeBillboard.SendReward(passiveUpgradeManager.GetUpgradeValue(UpgradeAttribute.VehicleIncomeBoost));
        GameEvents.Challenge_AddValue.Raise(ChallengeTarget.GetInteractions);
    }

    /// <summary>
    /// Method <c>InteractionDelay</c> grants a reward when eligible with a randomised delay for variety reasons.
    /// </summary>
    private IEnumerator InteractionDelay()
    {
        yield return new WaitForSeconds(Random.Range(1f, 2f));
        GrantReward();
    }

    /// <summary>
    /// Method <c>MoveVehicle</c> moves the vehicle and calls a trigger to destroy it if the destination has been reached.
    /// </summary>
    private void MoveVehicle()
    {
        if(Vector2.Distance(gameObject.transform.position, targetPosition) < 0.1f)
        {
            animator.SetTrigger(AnimDestroy);
            collider.enabled = false;
            GameEvents.Vehicle_DestinationReached.Raise(this);
        }
        else if(state == VehicleState.Moving && canMove)
        {
            gameObject.transform.position = Vector3.MoveTowards(gameObject.transform.position, targetPosition, vehicleSpeed * Time.deltaTime * GameManager.Instance.stageManager.stageController.GetObjectBoundValue(gameObject));
            RotateWheels();
        }
    }

    private void RotateWheels()
    {
        wheelLeft.transform.Rotate(0, 0, -150f * Time.deltaTime);
        wheelRight.transform.Rotate(0, 0, -150f * Time.deltaTime);
    }

    private void ScaleVehicle()
    {
        float value = GameManager.Instance.stageManager.stageController.GetObjectBoundValue(gameObject);
        gameObject.transform.position = new Vector3(gameObject.transform.position.x, gameObject.transform.position.y, -1 * value);
        gameObject.transform.localScale = new Vector3(1 * xScale, 1, 1) * value;
    }

    /// <summary>
    /// Method <c>Spawn</c> used to spawn the vehicle and set up its base properties depending on which direction it is travelling.
    /// </summary>
    private void Spawn(VehicleDirection direction)
    {
        BoxCollider2D objectCollider = null;
        switch (direction)
        {
            case VehicleDirection.Up:
                animator = back.GetComponent<Animator>();
                back.SetActive(true);
                objectCollider = back.GetComponent<BoxCollider2D>();
                forwardVector = Vector2.up;
                break;
            case VehicleDirection.Down:
                animator = front.GetComponent<Animator>();
                front.SetActive(true);
                objectCollider = front.GetComponent<BoxCollider2D>();
                forwardVector = Vector2.down;
                break;
            case VehicleDirection.Left:
                animator = side.GetComponent<Animator>();
                side.SetActive(true);
                objectCollider = side.GetComponent<BoxCollider2D>();
                forwardVector = Vector3.left;
                xScale = -1;
                break;
            case VehicleDirection.Right:
                animator = side.GetComponent<Animator>();
                side.SetActive(true);
                objectCollider = side.GetComponent<BoxCollider2D>();
                forwardVector = Vector2.right;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
        }
        animator.SetBool(AnimRiding, true);
        animator.SetBool(AnimIdle, false);
        collider.offset = objectCollider.offset;
        collider.size = objectCollider.size;
        collider.isTrigger = true;
        audioDriving.Play();
    }

    /// <summary>
    /// Method <c>SetState</c> adjusts vehicle animation and sounds based on its new state.
    /// </summary>
    public void SetState(VehicleState _state)
    {
        state = _state;
        if (!animator) return;
        
        if(state == VehicleState.Moving)
        {
            animator.SetBool(AnimRiding, true);
            animator.SetBool(AnimIdle, false);
            audioDriving.Play();
            audioIdle.Stop();
        }
        else if (state == VehicleState.Waiting)
        {
            animator.SetBool(AnimRiding, false);
            animator.SetBool(AnimIdle, true);
            audioDriving.Stop();
            audioIdle.Play();
        }
    }

    /// <summary>
    /// Method <c>AdjustAudioVolume</c> smooths out vehicle sounds volume and gives a nice 3D effect based on vehicles current distance from the camera.
    /// </summary>
    private void AdjustAudioVolume()
    {
        float distance = Vector3.Distance(transform.position, Camera.main.transform.position);
        if (distance < audioMinDistance)
        {
            audioDriving.volume = 1;
            audioIdle.volume = 1;
        }
        if (distance > audioMaxDistance)
        {
            audioDriving.volume = 0;
            audioIdle.volume = 0;
        }
        else
        {
            audioDriving.volume = 1 - ((distance - audioMinDistance) / (audioMaxDistance - audioMinDistance));
            audioIdle.volume = 1 - ((distance - audioMinDistance) / (audioMaxDistance - audioMinDistance));
        }
    }
}