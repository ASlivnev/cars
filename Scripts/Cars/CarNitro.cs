using UnityEngine;

// Повесить на тот же объект, где PrometeoCarController. Нитро прибавляет скорость напрямую
// через Rigidbody, в обход системы газа/maxSpeed контроллера - поэтому машина может
// разогнаться выше своего обычного потолка скорости.
[RequireComponent(typeof(PrometeoCarController))]
[RequireComponent(typeof(Rigidbody))]
public class CarNitro : MonoBehaviour
{
    [Tooltip("Клавиша активации нитро")]
    public KeyCode activateKey = KeyCode.Q;
    [Tooltip("На сколько км/ч суммарно разгоняет нитро за всё время работы")]
    public float speedBoost = 100f;
    [Tooltip("За сколько секунд нитро разгоняет машину на speedBoost")]
    public float boostDuration = 1f;
    [Tooltip("Система частиц (выхлоп/пламя) - играет всё время, пока активно нитро, и останавливается, когда нитро заканчивается")]
    [SerializeField] ParticleSystem nitroEffect;
    [Tooltip("AudioSource со звуком нитро - проигрывается, пока активно нитро, и останавливается, когда нитро заканчивается. Как и carEngineSound/tireScreechSound в PrometeoCarController, это AudioSource с уже назначенным в инспекторе AudioClip")]
    [SerializeField] AudioSource nitroSound;
    [Tooltip("Сколько раз за игру можно использовать нитро (0 - нельзя использовать вообще)")]
    public int maxUses = 2;

    PrometeoCarController car;
    Rigidbody rb;

    bool isBoosting;
    float boostTimer;
    int usesLeft;

    void Awake()
    {
        car = GetComponent<PrometeoCarController>();
        rb = GetComponent<Rigidbody>();
        usesLeft = maxUses;
    }

    void Update()
    {
        // Input.GetKey* - это глобальный опрос клавиатуры, не привязанный к конкретному объекту.
        // Этот же компонент включён и у противников (см. CarRole) - без проверки isPlayerControlled
        // нажатие Q игроком одновременно активировало бы нитро на всех машинах сразу. У противников
        // форсажем управляет EnemyCarAI через TryActivate(), а не эта клавиша.
        if(car.isPlayerControlled && Input.GetKeyDown(activateKey)){
            TryActivate();
        }
    }

    // Публичный запуск форсажа - используется и игроком (через Update() выше), и EnemyCarAI для
    // ботов. Сам проверяет, можно ли активировать нитро прямо сейчас (уже не разгоняется,
    // машина жива, остались использования).
    public void TryActivate()
    {
        if(isBoosting || !car.enabled || usesLeft <= 0) return;
        StartBoost();
    }

    void FixedUpdate()
    {
        if(!isBoosting) return;

        // Машина могла умереть (car.enabled = false в CarHealth.Die()) прямо во время разгона -
        // тогда нитро нужно оборвать, а не продолжать толкать уже неуправляемый обломок.
        if(!car.enabled){
            StopBoost();
            return;
        }

        // Постоянное ускорение (ForceMode.Acceleration игнорирует массу) даёт ровно speedBoost
        // км/ч суммарной прибавки к скорости за boostDuration секунд, независимо от массы машины
        // и от частоты кадров - в отличие от одного импульса, растянуто по времени, поэтому
        // разгон ощущается плавным, а не мгновенным рывком.
        float accelerationMS2 = (speedBoost / 3.6f) / boostDuration;
        rb.AddForce(transform.forward * accelerationMS2, ForceMode.Acceleration);

        boostTimer -= Time.fixedDeltaTime;
        if(boostTimer <= 0f){
            StopBoost();
        }
    }

    void StartBoost()
    {
        isBoosting = true;
        boostTimer = boostDuration;
        usesLeft--;

        if(nitroEffect != null){
            nitroEffect.Play();
        }
        if(nitroSound != null){
            nitroSound.Play();
        }
    }

    void StopBoost()
    {
        isBoosting = false;

        if(nitroEffect != null){
            nitroEffect.Stop();
        }
        if(nitroSound != null){
            nitroSound.Stop();
        }
    }
}
