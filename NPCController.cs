using System;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

public enum NPCState
{
    Moving, Waiting, Jumping, Interacting
}

enum NPCAnimState
{
    Idle, IdleBack, WalkVertical, WalkHorizontal, Jumping
}

enum WalkDirection
{
    Horizontal, Vertical, Idle
}

public class NPCController : MonoBehaviour
{
    // --------------- References sprites of each side of NPC --------------------
    private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite Sprite_MoveFront;
    [SerializeField] private Sprite Sprite_MoveBack;
    [SerializeField] private Sprite Sprite_MoveLeft;
    [SerializeField] private Sprite Sprite_MoveRight;
    
    private NavMeshAgent _agent;
    public NavMeshAgent agent { get { return _agent; } private set { _agent = value; } }
    
    private Vector3 _targetDestination;
    public Vector3 targetDestination { get { return _targetDestination; } private set { _targetDestination = value; } }
    
    private BoxCollider2D _clickCollider;
    public BoxCollider2D clickCollider { get { return _clickCollider; } private set { _clickCollider = value; } }
    
    private NPCState _state = NPCState.Moving;
    public NPCState state { get { return _state; } protected set { _state = value; } }
    
    private Animator animator;
    private float basePosZ = -1;
    public Vector3 lastPosition;
    protected bool isWaitingAtCurb = false;
    protected float speedScale = 1f;
    
    private static readonly int AnimDestroy = Animator.StringToHash("Destroy");
    private static readonly int AnimWalkHorizontal = Animator.StringToHash("WalkingLeftOrRight");
    private static readonly int AnimWalkVertical = Animator.StringToHash("WalkingFrontOrBack");
    private static readonly int AnimJumping = Animator.StringToHash("Jumping");
    
    private StageController stageController { get { return GameManager.Instance.stageManager.stageController; } }

    protected virtual void Awake()
    {
        clickCollider = GetComponent<BoxCollider2D>();
        animator = GetComponentInChildren<Animator>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        agent = gameObject.GetComponent<NavMeshAgent>();
        agent.updateRotation = false;
        agent.updateUpAxis = false;
        lastPosition = transform.position;
    }

    protected virtual void Start()
    { 
        speedScale = Random.Range(0.85f, 1.15f);
        ScaleNPC();
        agent.isStopped = true;
    }

    protected virtual void Update()
    {
        ScaleNPC();
        AnimationController();
        CheckDestinationReached();
    }

    protected virtual void SetState(NPCState _state)
    {
        if (_state == NPCState.Moving)
        {
            state = NPCState.Moving;
            agent.isStopped = false;
        }
        else if (_state == NPCState.Waiting)
        {
            state = NPCState.Waiting;
            agent.isStopped = true;
        }
    }

    /// <summary>
    /// Method <c>SetAnimState</c> sets the NPC animation state along with the correct sprite.
    /// </summary>
    private void SetAnimState(NPCAnimState animState)
    {
        bool walkingLeftRight = false;
        bool walkingFrontBack = false;
        bool jumping = false;
        
        switch (animState)
        {
            case NPCAnimState.Idle:
                SetSprite(Sprite_MoveFront);
                break;
            case NPCAnimState.IdleBack:
                SetSprite(Sprite_MoveBack);
                break;
            case NPCAnimState.WalkHorizontal:
                walkingLeftRight = true;
                SetSprite(agent.velocity.x > 0 ? Sprite_MoveRight : Sprite_MoveLeft);
                break;
            case NPCAnimState.WalkVertical:
                walkingFrontBack = true;
                SetSprite(agent.velocity.y > 0 ? Sprite_MoveBack : Sprite_MoveFront);
                break;
            case NPCAnimState.Jumping:
                jumping = true;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(animState), animState, null);
        }
        animator.SetBool(AnimWalkHorizontal, walkingLeftRight);
        animator.SetBool(AnimWalkVertical, walkingFrontBack);
        animator.SetBool(AnimJumping, jumping);
    }

    protected void ChangeScale(float value)
    {
        gameObject.transform.localScale = new Vector3(1, 1, 1) * value;
    }

    protected void ChangePosZ(float value)
    {
        gameObject.transform.position = new Vector3(gameObject.transform.position.x, gameObject.transform.position.y, basePosZ * value);
    }

    /// <summary>
    /// Method <c>ResetPos</c> resets NPC position once it is stopped. Required to work with the custom 2D navmesh solution.
    /// </summary>
    protected void ResetPos()
    {
        gameObject.transform.position = lastPosition;
    }

    public void SetTargetDestination(Vector3 target)
    {
        targetDestination = target;
        agent.SetDestination(targetDestination);
    }

    public virtual void SetTempDestination(Vector3 target)
    {
        agent.SetDestination(target);
    }
    
    public void WalkToCurb(Vector3 position)
    {
        isWaitingAtCurb = true;
        agent.SetDestination(position);
    }

    /// <summary>
    /// Method <c>ResetDestination</c> resets NPC destination when it is ready to move at the crosswalk.
    /// </summary>
    public virtual void ResetDestination()
    {
        if (isWaitingAtCurb)
        {
            isWaitingAtCurb = false;
        }
        agent.SetDestination(targetDestination);
        SetState(NPCState.Moving);
    }

    private void AnimationController()
    {
        SetAnimState(GetDesiredAnimState());
    }
    
    private WalkDirection GetWalkDirection() {
        float absX = Mathf.Abs(agent.velocity.x);
        float absY = Mathf.Abs(agent.velocity.y);
        if (absX > absY) return WalkDirection.Horizontal;
        if (absY > absX) return WalkDirection.Vertical;
        return WalkDirection.Idle;
    }

    /// <summary>
    /// Method <c>GetDesiredAnimState</c> gets the desired animation state based on movement direction and activity.
    /// </summary>
    private NPCAnimState GetDesiredAnimState()
    {
        WalkDirection direction = GetWalkDirection();

        if (direction == WalkDirection.Horizontal)
        {
            return NPCAnimState.WalkHorizontal;
        }

        if (direction == WalkDirection.Vertical)
        {
            return NPCAnimState.WalkVertical;
        }
        
        if (state == NPCState.Jumping)
        {
            return NPCAnimState.Jumping;
        }
        
        // Checks whether the NPC is idle whilst not waiting at a crosswalk
        // or is waiting at a crosswalk with their target direction having lower Y coordinates on a 2D space than the current position
        if (!isWaitingAtCurb && agent.isStopped && state != NPCState.Jumping ||
            isWaitingAtCurb && agent.isStopped && state != NPCState.Jumping && targetDestination.y < transform.position.y)
        {
            return NPCAnimState.Idle;
        }
        
        // If the NPC is waiting at a crosswalk and the target Y position is higher than the current position, it will be set to move vertically up
        if(isWaitingAtCurb && agent.isStopped && state != NPCState.Jumping && targetDestination.y > transform.position.y)
        {
            return NPCAnimState.IdleBack;
        }

        return NPCAnimState.Idle;
    }

    private void SetSprite(Sprite sprite)
    {
        spriteRenderer.sprite = sprite;
    }

    /// <summary>
    /// Method <c>ScaleNPC</c> scales NPC Z value, scale, and speed, to correctly simulate 2D depth effect.
    /// </summary>
    protected virtual void ScaleNPC()
    {
        float value = stageController.GetObjectBoundValue(gameObject);
        ChangeScale(value);
        if (!agent.isStopped)
        {
            agent.speed = value * speedScale * GameManager.Instance.player.passiveUpgradeManager.GetUpgradeValue(UpgradeAttribute.CivilianMovementSpeed);
            ChangePosZ(value);
            // Saving current position in case it needs to be restored when the NPC continues moving after being stopped
            // required to work with the custom 2D navmesh solution in order to prevent the characters Z-position from resetting
            lastPosition = gameObject.transform.position;
        }
        else
        {
            ResetPos();
        }
    }

    /// <summary>
    /// Method <c>CheckDestinationReached</c> checks whether the final destination is reached, or if the NPC is interacting with an object.
    /// </summary>
    protected virtual void CheckDestinationReached()
    {
        if(Vector2.Distance(agent.nextPosition, targetDestination) < 0.1f)
        {
            DestroyNPC();
        }
        else if (state != NPCState.Waiting && Vector3.Distance(agent.nextPosition, agent.destination) < 0.1f)
        {
            SetState(NPCState.Waiting);
        }
    }

    protected void DestroyNPC()
    {
        animator.SetTrigger(AnimDestroy);
        // Sends event to the NPC manager to destroy this NPC
        GameEvents.NPC_DestinationReached.Raise(this);
    }
}