using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    private Rigidbody rb;
    private Vector2 moveInput;

    [SerializeField]
    private float moveSpeed = 5f;

    [SerializeField]
    private float jumpForce = 5f;

    [SerializeField]
    private float rotationSpeed = 120f;

    [SerializeField]
    private float groundCheckDistance = 0.15f;

    [SerializeField]
    private float slopeSmoothSpeed = 8f;

    private bool isGrounded;
    private bool isJumping;

    private Vector3 groundNormal = Vector3.up;
    private Vector3 smoothGroundNormal = Vector3.up;

    private float yaw;


    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        yaw = transform.eulerAngles.y;
    }


    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }


    public void OnJump(InputValue value)
    {
        if (value.isPressed && isGrounded)
        {
            rb.AddForce(
                Vector3.up * jumpForce,
                ForceMode.Impulse
            );

            isGrounded = false;
            isJumping = true;
        }
    }


    void FixedUpdate()
    {
        CheckGround();

        // 着地したらジャンプ状態を解除
        if (isJumping &&
            isGrounded &&
            rb.linearVelocity.y <= 0.1f)
        {
            isJumping = false;
        }


        // =========================
        // 回転
        // =========================

        float rotation = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.leftArrowKey.isPressed)
            {
                rotation -= rotationSpeed * Time.fixedDeltaTime;
            }

            if (Keyboard.current.rightArrowKey.isPressed)
            {
                rotation += rotationSpeed * Time.fixedDeltaTime;
            }
        }


        if (rotation != 0f)
        {
            yaw += rotation;

            // 回転中は横方向の移動を止める
            // 上下方向の速度は維持
            rb.linearVelocity = new Vector3(
                0f,
                rb.linearVelocity.y,
                0f
            );

            Quaternion targetRotation =
                Quaternion.Euler(
                    0f,
                    yaw,
                    0f
                );

            rb.MoveRotation(targetRotation);

            return;
        }


        // =========================
        // 移動
        // =========================

        // ★ 機体座標系で移動
        //
        // W → 機体の前
        // S → 機体の後ろ
        // A → 機体の左
        // D → 機体の右
        //
        Vector3 movement =
            transform.right * moveInput.x +
            transform.forward * moveInput.y;

        // Y方向はここでは一旦0
        movement.y = 0f;


        // =========================
        // スロープ
        // =========================

        if (isGrounded &&
            movement.sqrMagnitude > 0.001f)
        {
            movement = Vector3.ProjectOnPlane(
                movement,
                groundNormal
            );

            if (movement.sqrMagnitude > 0.001f)
            {
                movement =
                    movement.normalized * moveSpeed;
            }
        }
        else if (movement.sqrMagnitude > 0.001f)
        {
            movement =
                movement.normalized * moveSpeed;
        }


        // =========================
        // ジャンプ・落下
        // =========================

        if (rb.linearVelocity.y > 0f)
        {
            if (isJumping)
            {
                movement.y =
                    rb.linearVelocity.y;
            }
        }
        else if (!isGrounded)
        {
            movement.y =
                rb.linearVelocity.y;
        }


        rb.linearVelocity = movement;


        // =========================
        // スロープ上で機体を傾ける
        // =========================

        if (isGrounded && !isJumping)
        {
            smoothGroundNormal = Vector3.Slerp(
                smoothGroundNormal,
                groundNormal,
                slopeSmoothSpeed *
                Time.fixedDeltaTime
            );


            Quaternion yawRotation =
                Quaternion.Euler(
                    0f,
                    yaw,
                    0f
                );


            Vector3 forward =
                yawRotation *
                Vector3.forward;


            Vector3 slopeForward =
                Vector3.ProjectOnPlane(
                    forward,
                    smoothGroundNormal
                );


            if (slopeForward.sqrMagnitude > 0.001f)
            {
                slopeForward.Normalize();


                Quaternion targetRotation =
                    Quaternion.LookRotation(
                        slopeForward,
                        smoothGroundNormal
                    );


                Quaternion smoothRotation =
                    Quaternion.Slerp(
                        rb.rotation,
                        targetRotation,
                        slopeSmoothSpeed *
                        Time.fixedDeltaTime
                    );


                rb.MoveRotation(
                    smoothRotation
                );
            }
        }
        else if (!isGrounded)
        {
            // 空中ではヨーだけ維持
            Quaternion targetRotation =
                Quaternion.Euler(
                    0f,
                    yaw,
                    0f
                );

            rb.MoveRotation(
                targetRotation
            );
        }
    }


    // =========================
    // 接地判定
    // =========================

    void CheckGround()
    {
        isGrounded = false;
        groundNormal = Vector3.up;

        BoxCollider box = GetComponent<BoxCollider>();

        Vector3 center = box.bounds.center;
        Vector3 extents = box.bounds.extents;

        Vector3[] checkPoints =
        {
            center + new Vector3(
                -extents.x * 0.8f,
                -extents.y + 0.05f,
                -extents.z * 0.8f
            ),

            center + new Vector3(
                extents.x * 0.8f,
                -extents.y + 0.05f,
                -extents.z * 0.8f
            ),

            center + new Vector3(
                -extents.x * 0.8f,
                -extents.y + 0.05f,
                extents.z * 0.8f
            ),

            center + new Vector3(
                extents.x * 0.8f,
                -extents.y + 0.05f,
                extents.z * 0.8f
            )
        };

        bool foundGround = false;
        float closestGroundY = float.NegativeInfinity;
        Vector3 bestNormal = Vector3.up;

        foreach (Vector3 point in checkPoints)
        {
            if (Physics.Raycast(
                point,
                Vector3.down,
                out RaycastHit hit,
                groundCheckDistance
            ))
            {
                if (Vector3.Dot(
                    hit.normal,
                    Vector3.up
                ) > 0.5f)
                {
                    if (!foundGround ||
                        hit.point.y > closestGroundY)
                    {
                        foundGround = true;
                        closestGroundY = hit.point.y;
                        bestNormal = hit.normal;
                    }
                }
            }
        }

        if (foundGround)
        {
            isGrounded = true;
            groundNormal = bestNormal;
        }
    }
}