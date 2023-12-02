using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UIElements;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    private float moveSpeed;
    public float walkSpeed;
    public float sprintSpeed;
    public float slideSpeed;
    public float wallRunSpeed;
    public float climbingSpeed;

    private float desiredMoveSpeed;
    private float desiredLastSpeed;

    public float speedIncreaseMultiplier;
    public float slopeIncreaseMultiplier;

    public float groundDrag;

    [Header("Jumping")]
    public float jumpForce;
    public float jumpCooldown;
    public float airMultiplier;
    bool readyToJump;

    [Header("Crouching")]
    public float crouchSpeed;
    public float crouchYScale;
    private float startYScale;

    [Header("Keybinds")]
    public KeyCode jumpKey = KeyCode.Space;
    public KeyCode sprintKey = KeyCode.LeftShift;
    public KeyCode crouchKey = KeyCode.LeftControl;

    [Header("Ground Check")]
    public float playerHeight;
    public Transform groundCheck;
    public float groundDistance = 0.1f;
    public LayerMask whatIsGround;
    public bool grounded;

    [Header("Slope Handling")]
    public float maxSlopeAngle;
    private RaycastHit slopeHit;
    private bool exitingSlope;

    public Transform orientation;
    public Animator animator;

    float horizontalInput;
    float verticalInput;

    Vector3 moveDirection;

    Rigidbody rb;

    public MovementState state;
    public enum MovementState
    {
        idle,
        walking,
        sprinting,
        wallrunning,
        crouching,
        sliding,
        climbing,
        air
    }

    public bool idle = true;
    public bool walking = false;
    public bool sprinting = false;
    public bool sliding = false;
    public bool crouching = false;
    public bool wallrunning = false;
    public bool climbing = false;
    
    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        readyToJump = true;

        startYScale = transform.localScale.y;
    }

    private void Update()
    {
        grounded = Physics.CheckSphere(groundCheck.position, groundDistance, whatIsGround);

        MyInput();
        SpeedControl();
        StateHandler();

        if (grounded)
            rb.drag = groundDrag;
        else
            rb.drag = 0;
    }

    private void FixedUpdate()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");
        MovePlayer();
    }

    private void MyInput()
    {
        if (Input.GetKey(jumpKey) && readyToJump && grounded)
        {
            
            readyToJump = false;

            Jump();

            Invoke(nameof(ResetJump), jumpCooldown);
        }
        if (Input.GetKeyDown(crouchKey))
        {
            if (!Input.GetKeyDown(KeyCode.LeftShift))
                crouching = true;
                transform.localScale = new Vector3(transform.localScale.x, crouchYScale, transform.localScale.z);
                rb.AddForce(Vector3.down * 5f, ForceMode.Impulse);
        }
        if (Input.GetKeyUp(crouchKey))
        {
            crouching = false;
            transform.localScale = new Vector3(transform.localScale.x, startYScale, transform.localScale.z);
        }
    }

    private void StateHandler()
    {
        if (climbing)
        {
            animator.SetBool("Idle", false);
            animator.SetBool("Walk", false);
            animator.SetBool("Sprint", false);
            idle = false;
            state = MovementState.climbing;
            desiredMoveSpeed = climbingSpeed;
        }
        else if (wallrunning)
        {
            animator.SetBool("Idle", false);
            animator.SetBool("Walk", false);
            animator.SetBool("Sprint", false);
            idle = false;
            state = MovementState.wallrunning;
            desiredMoveSpeed = wallRunSpeed;
        }
        if (sliding && state != MovementState.crouching)
        {
            animator.SetBool("Idle", false);
            animator.SetBool("Walk", false);
            animator.SetBool("Sprint", true);
            idle = false;
            state = MovementState.sliding;
            if (OnSlope() && rb.velocity.y < 0.1f)
            {
                desiredMoveSpeed = slideSpeed;
            }
            else
            {
                desiredMoveSpeed = sprintSpeed;
            }
        }
        else if (Input.GetKey(crouchKey) && state != MovementState.sliding)
        {
            animator.SetBool("Idle", false);
            animator.SetBool("Walk", true);
            animator.SetBool("Sprint", false);
            idle = false;
            state = MovementState.crouching;
            desiredMoveSpeed = crouchSpeed;
        }
        else if (grounded && Input.GetKey(sprintKey))
        {
            animator.SetBool("Idle", false);
            animator.SetBool("Walk", false);
            animator.SetBool("Sprint", true);
            idle = false;
            sprinting = true;
            walking = false;
            state = MovementState.sprinting;
            desiredMoveSpeed = sprintSpeed;
        }
        else if (grounded && !Input.anyKey)
        {
            animator.SetBool("Idle", true);
            animator.SetBool("Walk", false);
            animator.SetBool("Sprint", false);
            idle = true;
            sprinting = false;
            walking = false;
            state = MovementState.idle;
            desiredMoveSpeed = 0f;
        }
        else if (grounded && Input.anyKey)
        {
            animator.SetBool("Idle", false);
            animator.SetBool("Walk", true);
            animator.SetBool("Sprint", false);
            idle = false;
            sprinting = false;
            walking = true;
            state = MovementState.walking;
            desiredMoveSpeed = walkSpeed;
        }
        else if (!grounded)
        {
            animator.SetBool("Idle", true);
            animator.SetBool("Walk", false);
            animator.SetBool("Sprint", false);
            idle = true;
            sprinting = false;
            walking = false;
            state = MovementState.air;
        }
        if (Mathf.Abs(desiredMoveSpeed - desiredLastSpeed) > 7f && moveSpeed != 0)
        {
            StopAllCoroutines();
            StartCoroutine(SmoothLerpMoveSpeed());
        }
        else
        {
            moveSpeed = desiredMoveSpeed;
        }
        desiredLastSpeed = desiredMoveSpeed;
    }


    private IEnumerator SmoothLerpMoveSpeed()
    {
        float time = 0;
        float difference = Mathf.Abs(desiredMoveSpeed - moveSpeed);
        float startValue = moveSpeed;

        while (time < difference)
        {
            moveSpeed = Mathf.Lerp(startValue, desiredMoveSpeed, time / difference);
            if (OnSlope())
            {
                float slopeAngle = Vector3.Angle(Vector3.up, slopeHit.normal);
                float slopeAngleIncrease = 1 + (slopeAngle / 90f);

                time += Time.deltaTime * speedIncreaseMultiplier * slopeIncreaseMultiplier * slopeAngleIncrease;
            }
            else
                time += Time.deltaTime * speedIncreaseMultiplier;
            yield return null;
        }
        moveSpeed = desiredMoveSpeed;
    }

    private void MovePlayer()
    {
        moveDirection = orientation.forward * verticalInput + orientation.right * horizontalInput;

        if (OnSlope() && !exitingSlope)
        {
            rb.AddForce(GetSlopeMoveDirection(moveDirection) * moveSpeed * 20f, ForceMode.Force);

            if (rb.velocity.y > 0)
                rb.AddForce(Vector3.down * 80f, ForceMode.Force);
        }
        else if (grounded && !idle)
        { 
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f, ForceMode.Force);
        }
        else if (!grounded)
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f * airMultiplier, ForceMode.Force);

        rb.useGravity = !OnSlope();
    }

    private void SpeedControl()
    {
        if (OnSlope() && !exitingSlope)
        {
            if (rb.velocity.magnitude > moveSpeed)
                rb.velocity = rb.velocity.normalized * moveSpeed;
        }
        else
        {
            Vector3 flatVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            if (flatVel.magnitude > desiredMoveSpeed)
            {
                Vector3 limitedVel = flatVel.normalized * desiredMoveSpeed;
                rb.velocity = new Vector3(limitedVel.x, rb.velocity.y, limitedVel.z);
            }
        }
    }

    public void Jump()
    {
        exitingSlope = true;
        animator.SetTrigger("Jump");
        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

        rb.AddForce(transform.up * jumpForce, ForceMode.Force);
    }
    private void ResetJump()
    {
        readyToJump = true;
        exitingSlope = false;
    }

    public bool OnSlope()
    {
        if (Physics.CheckSphere(groundCheck.position, groundDistance, whatIsGround))
        {
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            return angle < maxSlopeAngle && angle != 0;
        }
        return false;
    }

    public Vector3 GetSlopeMoveDirection(Vector3 direction)
    {
        return Vector3.ProjectOnPlane(direction, slopeHit.normal).normalized;
    }
}