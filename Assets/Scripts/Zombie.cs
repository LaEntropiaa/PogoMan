using UnityEngine;

public class Zombie : Enemy
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100.0f;

    [Header("Attack")]
    [SerializeField] private float attackDamage = 10.0f;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackCooldown = 1.0f;

    [Header("Animation")]
    [SerializeField] private Animator animator;

    private float currentHealth;
    private float attackTimer;
    private bool isDead;

    private static readonly int IsWalking =
        Animator.StringToHash("IsWalking");

    private static readonly int IsAttacking =
        Animator.StringToHash("IsAttacking");

    private static readonly int DieTrigger =
        Animator.StringToHash("Die");

    protected override void Awake()
    {
        base.Awake();

        currentHealth = maxHealth;

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    protected override void FixedUpdate()
    {
        if (isDead || target == null)
            return;

        attackTimer -= Time.fixedDeltaTime;

        float distance = Vector3.Distance(
            rb.position,
            target.position
        );

        if (distance <= attackRange)
        {
            Attack();
        }
        else
        {
            FollowTarget();
        }

        UpdateAnimation(distance);
    }

    private void Attack()
    {
        if (attackTimer > 0.0f)
            return;

        attackTimer = attackCooldown;

        if (animator != null)
        {
            animator.SetTrigger(IsAttacking);
        }

        Debug.Log($"{name} atacó a {target.name}");
    }

    private void UpdateAnimation(float distance)
    {
        if (animator == null)
            return;

        bool walking =
            distance > attackRange &&
            !isDead;

        animator.SetBool(IsWalking, walking);
    }

    public void TakeDamage(float damage)
    {
        if (isDead)
            return;

        currentHealth -= damage;

        Debug.Log(
            $"{name} recibió {damage} de daño. " +
            $"Vida: {currentHealth}/{maxHealth}"
        );

        if (currentHealth <= 0.0f)
        {
            Die();
        }
    }

    private void Die()
    {
        isDead = true;

        if (animator != null)
        {
            animator.SetBool(IsWalking, false);
            animator.SetTrigger(DieTrigger);
        }

        rb.linearVelocity = Vector3.zero;
    }
}
