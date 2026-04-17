using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPCBehaviour : MonoBehaviour
{
    public Sprite ActiveSprite;  // Основной спрайт, когда NPC двигается
    public Sprite IdleSprite;
    private SpriteRenderer spriteRenderer;

    // MOVEMENT
    public float moveSpeed = 2f;
    public float jumpForce = 16f;
    public LayerMask groundLayer;
    public Transform groundCheck;
    public Transform wallCheck;
    public Transform groundAheadCheck;
    public Transform ladderGroundCheck;
    private float groundCheckRadius = 0.2f;
    private float wallCheckRadius = 0.2f;

    // FIND FLOWERS
    public string flowerTag;
    public GameObject targetFlower = null;

    private Rigidbody2D rb;
    private bool facingRight = true;

    // STATES
    public bool isGrounded = false;
    public bool isHitAWall = false;
    public bool isClimbing = false;
    public bool isIdle = false;


    // GROUND
    public bool canCheckGround = false;
    private float groundCheckDelay = 0.9f; // Задержка перед проверкой
    public float checkGroundTimer = 0f;

    // CLIMBING
    private float climbSpeed = 2f;
    public GameObject targetLadder = null;
    public bool isNavigatingToLadder = false;
    private string ladderTag = "Ladder";
    private float yLevelTolerance = 3f;
    private float xLevelTolerance = 0.5f;
    private bool needALadder = false;

    private bool isEscaping = false;
    private float escapeDirection = -1f;
    private float startYPosition;
    private float yChangeThreshold = 1f;

    // IDLE
    private float minActionTime = 3f;
    private float maxActionTime = 6f;
    private float actionTimer = 0f;
    public Coroutine idleCoroutine;


    // SEEDING
    public GameObject FlowerSeedPrefab;         // Префаб семечка
    public float plantInterval = 3f;            // Интервал между посадками
    private float plantTimer = 0f;              // Таймер для посадки

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        Patrol();
    }

    void Update()
    {
        CheckGround();
        CheckWall();

        if (targetFlower == null)
        {
            FindFlower();
            PlantFlowers();

            if (!isIdle)
            {
                Patrol();
                isIdle = true;
            }
        }

        if (targetFlower != null)
        {
            isIdle = false;

            if (idleCoroutine != null)
            {
                StopCoroutine(idleCoroutine);
                idleCoroutine = null;
                spriteRenderer.sprite = ActiveSprite;
            }

            if (IsTargetAtSameHeight(targetFlower.transform.position) && !isClimbing) // если на одной высоте
            {
                MoveTowards(targetFlower.transform.position);
                isNavigatingToLadder = false;
            }
            else if (IsTargetLower(targetFlower.transform.position)) // если цель ниже
            {
                if (!isEscaping)
                {
                    if (IsStuckVertically(targetFlower.transform.position) && !isClimbing)
                    {
                        isEscaping = true;
                        escapeDirection = GetEscapeDirection(); // Получаем направление обхода
                        startYPosition = transform.position.y;   // Запоминаем начальную Y
                    }
                    else
                    {
                        MoveTowards(targetFlower.transform.position);
                    }
                }
                else
                {
                    rb.velocity = new Vector2(escapeDirection * moveSpeed, rb.velocity.y);

                    if (escapeDirection > 0)
                        facingRight = true;
                    else if (escapeDirection < 0)
                        facingRight = false;

                    if (Mathf.Abs(transform.position.y - startYPosition) >= yChangeThreshold)
                    {
                        isEscaping = false;
                        rb.velocity = Vector2.zero;
                    }
                }
            }
            else // если цель выше
            {
                if (!isNavigatingToLadder)
                {
                    FindNearestLadder();
                }

                if (targetLadder != null)
                {
                    needALadder = true;
                    MoveTowards(targetLadder.transform.position);
                    if (isClimbing)
                    {
                        isNavigatingToLadder = false;
                        needALadder = false;
                    }
                }
            }
        }

        FlipSprite();
    }

    void OnTriggerEnter2D(Collider2D obj) //«Наезд» на объект
    {
        if (obj.CompareTag("Ladder") && needALadder && obj.transform == targetLadder.transform)
        {
            EnterLadder();
        }

        if (obj.CompareTag("Collectable"))
        {
            if (obj.GetComponent<GrowingObject>().Item.Type == flowerTag && obj.transform == targetFlower.transform)
            {
                PickUpFlower();
            }
        }
    }

    void FixedUpdate()
    {
        if (isGrounded)
        {
            // Прыжок, если есть препятствие перед ногами
            if (isHitAWall && !isClimbing)
            {
                Jump();
            }
        }

        if (isClimbing)
        {
            ClimbLadder();
        }
    }

    void CheckGround()
    {
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }

    void CheckWall()
    {
        isHitAWall = Physics2D.OverlapCircle(wallCheck.position, wallCheckRadius, groundLayer);
    }

    void FlipDirection()
    {
        facingRight = !facingRight;
        transform.localScale = new Vector3(-transform.localScale.x, 1f, 1f);
    }

    void FlipSprite()
    {
        if (facingRight)
            transform.localScale = new Vector3(1f, 1f, 1f);
        else
            transform.localScale = new Vector3(-1f, 1f, 1f);
    }

    void Patrol()
    {
        idleCoroutine = StartCoroutine(IdleRoutine());
    }

    void PlantFlowers()
    {
        plantTimer += Time.deltaTime;

        if (plantTimer >= plantInterval)
        {
            Vector2 spawnPosition = (Vector2)transform.position + new Vector2(facingRight ? 0.5f : -0.5f, -0.5f);

            if (FlowerSeedPrefab != null)
            {
                Instantiate(FlowerSeedPrefab, spawnPosition, Quaternion.identity);
                plantTimer = 0f;
            }
        }
    }

    void MoveTowards(Vector2 targetPosition)
    {
        float direction = (targetPosition.x - transform.position.x) > 0 ? 1 : -1;
        rb.velocity = new Vector2(direction * moveSpeed, rb.velocity.y);

        if (direction > 0)
            facingRight = true;
        else if (direction < 0)
            facingRight = false;
    }

    void Jump()
    {
        rb.velocity = new Vector2(rb.velocity.x, jumpForce);
    }

    void FindFlower()
    {
        GameObject[] candidates = GameObject.FindGameObjectsWithTag("Collectable");

        float closestDistance = Mathf.Infinity;
        GameObject closestFlower = null;

        foreach (GameObject obj in candidates)
        {
            // Получаем компонент ObjectWeapon
            GrowingObject weapon = obj.GetComponent<GrowingObject>();

            // Проверяем, что компонент и вложенный объект существуют
            if (weapon != null && weapon.Item != null)
            {
                // Проверяем тип предмета
                if (weapon.Item.Type == flowerTag && weapon.IsGrown())
                {
                    float distance = Vector2.Distance(transform.position, obj.transform.position);

                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestFlower = obj;
                    }
                }
            }
        }

        targetFlower = closestFlower;
    }

    void PickUpFlower()
    {
        Destroy(targetFlower);
        targetFlower = null;
    }

    void ClimbLadder()
    {
        // Поднимаемся вверх по лестнице
        rb.velocity = new Vector2(0f, climbSpeed);
        checkGroundTimer += Time.fixedDeltaTime;

        if (checkGroundTimer >= groundCheckDelay)
        {
            canCheckGround = true;
        }

        // проверка, что достигли верха лестницы
        if (canCheckGround && HasReachedTopOfLadder())
        {
            ExitLadder();
        }
    }

    bool HasReachedTopOfLadder()
    {
        return Physics2D.OverlapCircle(ladderGroundCheck.position, groundCheckRadius, groundLayer);
    }

    public void EnterLadder()
    {
        isClimbing = true;
        rb.bodyType = RigidbodyType2D.Kinematic; // Отключаем физику
        rb.velocity = Vector2.zero;
        checkGroundTimer = 0f;
        canCheckGround = false;
    }

    void ExitLadder()
    {
        isClimbing = false;
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.velocity = Vector2.zero;
        canCheckGround = false;

        targetLadder = null;
        needALadder = false;
        isNavigatingToLadder = false;
    }

    void FindNearestLadder()
    {
        GameObject[] nearbyLadders = GameObject.FindGameObjectsWithTag("Ladder");

        float closestDistance = Mathf.Infinity;

        foreach (GameObject ladder in nearbyLadders)
        {
                // Проверяем, что лестница на нашем уровне Y и при этом чуть выше
                if (Mathf.Abs(transform.position.y - ladder.transform.position.y) <= yLevelTolerance && ladder.transform.position.y > transform.position.y)
                {
                    float distance = Vector2.Distance(transform.position, ladder.transform.position);

                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        targetLadder = ladder; // Сохраняем как GameObject
                        isNavigatingToLadder = true;
                    }
                }
        }
    }

    bool IsTargetAtSameHeight(Vector2 targetPosition)
    {
        return Mathf.Abs(transform.position.y - targetPosition.y) <= yLevelTolerance;
    }

    bool IsTargetLower(Vector2 targetPosition)
    {
        return transform.position.y > targetPosition.y;
    }

    bool IsStuckVertically(Vector2 targetPosition)
    {
        bool isCloseX = Mathf.Abs(transform.position.x - targetPosition.x) <= xLevelTolerance;
        bool isDifferentY = Mathf.Abs(transform.position.y - targetPosition.y) > yLevelTolerance;
        return isCloseX && isDifferentY;
    }

    float GetEscapeDirection()
    {
        Vector2 position = (Vector2)transform.position;

        // Проверяем наличие земли слева и справа на небольшом удалении
        for (float distance = 0.5f; distance <= 10f; distance += 0.5f)
        {
            // Слева
            Vector2 leftCheck = position + new Vector2(-distance, -1f);
            bool leftHasGround = Physics2D.OverlapCircle(leftCheck, groundCheckRadius, groundLayer);

            // Справа
            Vector2 rightCheck = position + new Vector2(distance, -1f);
            bool rightHasGround = Physics2D.OverlapCircle(rightCheck, groundCheckRadius, groundLayer);

            // Если слева нет земли — идём налево
            if (!leftHasGround)
                return -1f;

            // Если справа нет земли — идём направо
            if (!rightHasGround)
                return 1f;
        }

        // Если обрыва не нашлось — просто разворачиваемся
        return facingRight ? -1f : 1f;
    }

    IEnumerator IdleRoutine()
    {
        isIdle = true;

        while (isIdle)
        {
            if (targetFlower != null)
            {
                break;
            }

            // Случайное действие и время
            yield return new WaitForSeconds(Random.Range(minActionTime, maxActionTime));
            int action = Random.Range(0, 3);

            spriteRenderer.sprite = IdleSprite;
            yield return new WaitForSeconds(1f); // Пауза с IdleSprite

            spriteRenderer.sprite = ActiveSprite;

            if (action == 0) // Идти влево
            {
                facingRight = false;
                yield return StartCoroutine(MoveForSeconds(-1f, Random.Range(minActionTime, maxActionTime)));
                Debug.Log("Идет влево ИДЛЕ");
            }
            else if (action == 1) // Идти вправо
            {
                facingRight = true;
                yield return StartCoroutine(MoveForSeconds(1f, Random.Range(minActionTime, maxActionTime)));
                Debug.Log("Идет вправо ИДЛЕ");
            }
            else if (action == 2) // Подняться по лестнице
            {
                Debug.Log("Идет к лестнице ИДЛЕ");

                if (!isNavigatingToLadder)
                {
                    FindNearestLadder();
                }

                if (targetLadder != null)
                {
                    needALadder = true;
                    MoveTowards(targetLadder.transform.position);
                    if (isClimbing)
                    {
                        isNavigatingToLadder = false;
                        needALadder = false;
                    }
                }
            }
        }
    }

    IEnumerator MoveForSeconds(float direction, float seconds)
    {
        float elapsed = 0f;

        while (elapsed < seconds && isIdle)
        {
            rb.velocity = new Vector2(direction * moveSpeed, rb.velocity.y);
            Debug.Log(elapsed);

            elapsed += Time.fixedDeltaTime;
            yield return null;
        }

        rb.velocity = Vector2.zero;
    }
}
