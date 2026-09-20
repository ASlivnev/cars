using System.Collections.Generic;
using UnityEngine;

// Повесить на машину-соперника (тот же префаб, что у игрока, с компонентом PrometeoCarController).
// При добавлении этого скрипта поле isPlayerControlled в PrometeoCarController автоматически
// выключается, чтобы машина не реагировала на WASD игрока.
[RequireComponent(typeof(PrometeoCarController))]
[RequireComponent(typeof(Rigidbody))]
public class EnemyCarAI : MonoBehaviour
{
    [Header("Цель (режим \"все против всех\")")]
    [Tooltip("Как часто выбирать новую случайную цель среди всех машин на карте (цель также меняется сразу, если текущая машина уничтожена)")]
    public float retargetInterval = 15f;
    [Tooltip("Максимум AI, одновременно таранящих одну и ту же машину. При выборе цели AI сперва ищет жертву, у которой атакующих меньше этого числа - без этого несколько ботов могут выбрать одну и ту же цель и всей толпой съехаться в одну точку")]
    public int maxAttackersPerTarget = 2;

    [Header("Разделение (избегание толкучки на подъезде)")]
    [Tooltip("Радиус, в котором другие машины (кроме текущей цели) отталкивают AI в сторону")]
    public float separationRadius = 5f;
    [Tooltip("Сила отталкивания от соседних машин относительно стремления к цели (0 = разделение выключено, 1 = равный вес с целью). Без этого несколько ботов, атакующих одну цель, стремятся в одну и ту же точку и сминаются друг в друга ещё до самого тарана")]
    [Range(0f, 2f)]
    public float separationWeight = 0.8f;

    [Header("Объезд препятствий")]
    public float obstacleCheckDistance = 8f;
    public float obstacleSideOffset = 1.5f;
    [Tooltip("Слои препятствий. По умолчанию Everything - стены обнаруживаются вне зависимости от того, на каком они слое; другие машины из объезда исключаются отдельно в коде, они не считаются препятствием")]
    public LayerMask obstacleAvoidMask = ~0;

    [Header("Вождение в поворотах")]
    [Tooltip("Угол до цели (град.), выше которого AI дёргает ручник для резкого разворота")]
    public float sharpTurnAngle = 50f;
    [Tooltip("Ручник на резком повороте включается только если скорость (км/ч) выше этого значения")]
    public float minSpeedForHandbrakeTurn = 20f;

    [Header("Несовершенство ИИ")]
    [Tooltip("Как часто AI 'обновляет' в голове положение цели при таране - имитация реакции")]
    public float reactionDelay = 0.15f;
    [Tooltip("Случайный разброс прицеливания при таране (метры)")]
    public float aimErrorRadius = 1.5f;

    [Header("Rubber-banding (подстройка сложности)")]
    [Tooltip("Дистанция до цели, на которой буст скорости уже максимальный")]
    public float rubberBandDistance = 40f;
    [Tooltip("Во сколько раз может вырасти максимальная скорость, если AI сильно отстал")]
    public float maxSpeedBoostMultiplier = 1.3f;
    public float rubberBandUpdateInterval = 1f;

    [Header("Застревание")]
    [Tooltip("Как часто проверять, насколько машина реально сместилась")]
    public float stuckCheckInterval = 2f;
    [Tooltip("Если за stuckCheckInterval машина проехала меньше этого расстояния - считаем, что застряла")]
    public float minMoveDistance = 3f;
    public float reverseDuration = 1f;
    [Tooltip("Пауза после реверса, прежде чем снова можно проверять застревание (чтобы не дёргаться туда-сюда)")]
    public float reverseCooldown = 1.5f;

    [Header("Разъезд из кучи машин")]
    [Tooltip("Радиус, в котором учитываются другие машины при определении, что AI застрял в куче/заторе")]
    public float clumpCheckRadius = 6f;
    [Tooltip("Минимальное количество других живых машин в clumpCheckRadius, чтобы считать себя частью кучи")]
    public int clumpMinNearbyCars = 2;
    [Tooltip("Сколько секунд подряд нужно непрерывно быть в куче, прежде чем AI бросит монетку - решать, отъезжать ли ему")]
    public float clumpTimeToReact = 15f;
    [Tooltip("Вероятность (0-1), что по истечении clumpTimeToReact машина реально отъедет. Меньше 1, чтобы куча не разъезжалась вся разом одним и тем же способом - часть машин остаётся толкаться, а часть уезжает разгоняться")]
    [Range(0f, 1f)]
    public float clumpRetreatChance = 0.5f;
    [Tooltip("На какое расстояние AI отъезжает от кучи, прежде чем снова развернуться в сторону цели (уже с разгона)")]
    public float clumpRetreatDistance = 15f;
    [Tooltip("Максимальное время на сам манёвр отъезда - если что-то мешает отъехать на нужное расстояние (например, стена), всё равно прерываем по таймауту")]
    public float clumpRetreatTimeout = 4f;

    [Header("Стрельба из пулемёта")]
    [Tooltip("Минимальный кулдаун (сек) между очередями - AI не стреляет непрерывно, конкретное значение каждый раз выбирается случайно между Min и Max")]
    public float shootCooldownMin = 10f;
    [Tooltip("Максимальный кулдаун (сек) между очередями")]
    public float shootCooldownMax = 15f;
    [Tooltip("Как долго (сек) держится нажатым курок за одну очередь")]
    public float burstDuration = 1f;
    [Tooltip("Максимальная дистанция до цели, на которой AI вообще пытается стрелять")]
    public float fireRange = 60f;
    [Tooltip("Угол (град.) между направлением машины и целью, в пределах которого цель считается 'на линии огня'")]
    public float fireAngleThreshold = 8f;

    [Header("Форсаж (нитро) при таране")]
    [Tooltip("Кулдаун (сек) между использованиями нитро для тарана")]
    public float nitroCooldown = 15f;
    [Tooltip("Вероятность (0-1), что при удачной возможности для тарана AI реально включит нитро - не каждый раз, чтобы это не выглядело как гарантированная реакция")]
    [Range(0f, 1f)]
    public float nitroUseChance = 0.5f;
    [Tooltip("Минимальная дистанция до цели (метры) для использования нитро - вплотную разгоняться уже поздно и бессмысленно")]
    public float nitroMinDistance = 10f;
    [Tooltip("Максимальная дистанция до цели (метры) для использования нитро - слишком далеко, и цель успеет свернуть с курса за время разгона")]
    public float nitroMaxDistance = 35f;
    [Tooltip("Угол (град.) между направлением машины и целью, в пределах которого таран форсажем считается удачной возможностью")]
    public float nitroAngleThreshold = 20f;

    PrometeoCarController car;
    Rigidbody rb;
    CarMachineGuns guns;
    CarNitro nitro;

    Transform currentTarget;
    Rigidbody currentTargetRb;
    float retargetTimer;

    Vector3 perceivedTargetPoint;
    float reactionTimer;

    bool isHandbraking;

    int baseMaxSpeed;
    float rubberBandTimer;

    Vector3 lastCheckPosition;
    float stuckCheckTimer;
    float reverseCooldownTimer;
    bool isReversing;
    float reverseTimer;

    float clumpTimer;
    bool isRetreatingFromClump;
    Vector3 retreatDirection;
    Vector3 retreatStartPosition;
    float retreatManeuverTimer;

    float shootCooldownTimer;
    bool isBursting;
    float burstTimer;

    float nitroCooldownTimer;

    // Реестр всех живых EnemyCarAI на карте - нужен, чтобы при выборе цели можно было
    // посчитать, сколько ботов уже атакует конкретную машину (см. CountAttackers).
    static readonly List<EnemyCarAI> AllEnemies = new List<EnemyCarAI>();

    void OnEnable()
    {
        AllEnemies.Add(this);
    }

    void OnDisable()
    {
        AllEnemies.Remove(this);
    }

    void Start()
    {
        car = GetComponent<PrometeoCarController>();
        car.isPlayerControlled = false;
        rb = GetComponent<Rigidbody>();
        guns = GetComponent<CarMachineGuns>();
        nitro = GetComponent<CarNitro>();
        baseMaxSpeed = car.GetMaxSpeed();
        lastCheckPosition = transform.position;
        PickRandomTarget();

        // Случайный старт кулдаунов - чтобы все боты не открывали огонь/форсаж синхронно в один момент.
        shootCooldownTimer = Random.Range(shootCooldownMin, shootCooldownMax);
        nitroCooldownTimer = Random.Range(0f, nitroCooldown);
    }

    void Update()
    {
        if(reverseCooldownTimer > 0f){
            reverseCooldownTimer -= Time.deltaTime;
        }

        retargetTimer -= Time.deltaTime;
        if(retargetTimer <= 0f || currentTarget == null || IsTargetDead()){
            PickRandomTarget();
        }

        UpdateRubberBanding();
        HandleShooting();
        HandleNitroUsage();

        if(isReversing){
            HandleReverse();
            return;
        }

        if(isRetreatingFromClump){
            HandleRetreatFromClump();
            return;
        }

        if(currentTarget != null){
            Ram();
        }

        CheckIfStuck();
        CheckClump();
    }

    // Выбирает случайную живую машину среди ВСЕХ на карте (реестр PrometeoCarController.AllCars,
    // куда сама себя регистрирует и игрок, и любой другой противник) - без учёта дистанции или
    // видимости, поэтому AI всегда знает, куда ехать, и не зависит от настройки слоёв рейкастов.
    void PickRandomTarget()
    {
        retargetTimer = retargetInterval;

        List<PrometeoCarController> candidates = new List<PrometeoCarController>();
        foreach(PrometeoCarController candidate in PrometeoCarController.AllCars){
            if(candidate == null || candidate == car) continue;
            CarHealth health = candidate.GetComponent<CarHealth>();
            if(health != null && health.isDead) continue;
            candidates.Add(candidate);
        }

        if(candidates.Count == 0){
            currentTarget = null;
            currentTargetRb = null;
            return;
        }

        // Сначала пробуем выбрать цель, у которой ещё не набралось maxAttackersPerTarget
        // атакующих - это распределяет ботов по разным машинам вместо того, чтобы все
        // ломились к одной и той же жертве. Если свободных целей нет (все уже "разобраны"),
        // выбираем из полного списка, чтобы не оставлять AI вообще без цели.
        List<PrometeoCarController> underAttacked = candidates.FindAll(c => CountAttackers(c.transform) < maxAttackersPerTarget);
        List<PrometeoCarController> pool = underAttacked.Count > 0 ? underAttacked : candidates;

        PrometeoCarController chosen = pool[Random.Range(0, pool.Count)];
        currentTarget = chosen.transform;
        currentTargetRb = chosen.GetComponent<Rigidbody>();
    }

    int CountAttackers(Transform target)
    {
        int count = 0;
        foreach(EnemyCarAI enemy in AllEnemies){
            if(enemy != null && enemy != this && enemy.currentTarget == target){
                count++;
            }
        }
        return count;
    }

    bool IsTargetDead()
    {
        CarHealth health = currentTarget.GetComponent<CarHealth>();
        return health != null && health.isDead;
    }

    // Таран: едем в цель с упреждением по её скорости, отталкиваясь от соседних машин
    // (кроме самой цели) и объезжая препятствия на пути.
    void Ram()
    {
        UpdatePerceivedTarget();

        Vector3 desiredDirection = perceivedTargetPoint - transform.position;
        desiredDirection.y = 0f;
        desiredDirection = desiredDirection.sqrMagnitude > 0.01f ? desiredDirection.normalized : transform.forward;

        Vector3 combinedDirection = desiredDirection + ComputeSeparation() * separationWeight;
        Vector3 finalDirection = combinedDirection.sqrMagnitude > 0.01f
            ? AvoidObstacles(combinedDirection.normalized)
            : transform.forward;

        DriveTowards(transform.position + finalDirection * 10f);
    }

    // Отталкивание от соседних машин (кроме текущей цели - её как раз нужно таранить, а не
    // избегать). Без этого несколько ботов, атакующих одну и ту же цель, стремятся в одну и ту
    // же точку и сминаются друг в друга ещё на подъезде, вместо того чтобы обступить цель
    // с разных сторон. Сила отталкивания растёт линейно по мере приближения к соседу.
    Vector3 ComputeSeparation()
    {
        Vector3 push = Vector3.zero;
        foreach(PrometeoCarController candidate in PrometeoCarController.AllCars){
            if(candidate == null || candidate == car) continue;
            if(candidate.transform == currentTarget) continue;

            CarHealth health = candidate.GetComponent<CarHealth>();
            if(health != null && health.isDead) continue;

            Vector3 offset = transform.position - candidate.transform.position;
            offset.y = 0f;
            float distance = offset.magnitude;
            if(distance > 0.01f && distance < separationRadius){
                push += offset.normalized * (1f - distance / separationRadius);
            }
        }
        return push;
    }

    // Имитация реакции водителя: реальную позицию/скорость цели AI "считывает" не каждый кадр,
    // а раз в reactionDelay, плюс добавляет случайный промах прицеливания.
    void UpdatePerceivedTarget()
    {
        reactionTimer -= Time.deltaTime;
        if(reactionTimer > 0f) return;
        reactionTimer = reactionDelay;

        Vector3 targetPoint = currentTarget.position;
        if(currentTargetRb != null){
            targetPoint += currentTargetRb.linearVelocity * 0.5f;
        }

        Vector2 aimError = Random.insideUnitCircle * aimErrorRadius;
        targetPoint += new Vector3(aimError.x, 0f, aimError.y);

        perceivedTargetPoint = targetPoint;
    }

    // Стрельба отдельна от манёвров тарана - AI не палит непрерывно, а раз в случайные
    // shootCooldownMin..shootCooldownMax секунд решает открыть короткую очередь длиной
    // burstDuration, если в этот момент цель на линии огня. Работает независимо от того,
    // едет ли машина вперёд, отъезжает от кучи или сдаёт назад (см. вызов в Update()).
    void HandleShooting()
    {
        if(guns == null || currentTarget == null) return;

        if(isBursting){
            if(IsTargetInLineOfFire()){
                guns.TryFire();
            }

            burstTimer -= Time.deltaTime;
            if(burstTimer <= 0f){
                isBursting = false;
                shootCooldownTimer = Random.Range(shootCooldownMin, shootCooldownMax);
            }
            return;
        }

        shootCooldownTimer -= Time.deltaTime;
        if(shootCooldownTimer <= 0f && IsTargetInLineOfFire()){
            isBursting = true;
            burstTimer = burstDuration;
            guns.TryFire();
        }
    }

    // Цель "на линии огня", если она в пределах fireRange, в пределах угла fireAngleThreshold
    // от направления машины, и между машиной и целью нет препятствия (стены/другой машины) -
    // иначе бот открывал бы огонь в стену или в спину случайно проезжающему мимо сопернику.
    bool IsTargetInLineOfFire()
    {
        Vector3 toTarget = currentTarget.position - transform.position;
        toTarget.y = 0f;

        float distance = toTarget.magnitude;
        if(distance > fireRange || distance < 0.01f) return false;

        float angle = Vector3.Angle(transform.forward, toTarget);
        if(angle > fireAngleThreshold) return false;

        if(Physics.Raycast(transform.position + Vector3.up * 0.5f, toTarget.normalized, out RaycastHit hit, distance)){
            if(hit.transform.root != currentTarget.root) return false;
        }

        return true;
    }

    // Форсаж, как и стрельба, доступен не постоянно - раз в nitroCooldown секунд AI проверяет,
    // есть ли удачная возможность для тарана, и с вероятностью nitroUseChance решает
    // воспользоваться нитро. Кулдаун списывается независимо от результата монетки - иначе при
    // неудачном броске AI пробовал бы снова в следующем же кадре, и вероятность 50/50 ничего
    // не значила бы (за секунду набралось бы десятки попыток).
    void HandleNitroUsage()
    {
        if(nitro == null || currentTarget == null) return;

        nitroCooldownTimer -= Time.deltaTime;
        if(nitroCooldownTimer > 0f) return;
        if(!IsGoodRammingOpportunity()) return;

        nitroCooldownTimer = nitroCooldown;

        if(Random.value < nitroUseChance){
            nitro.TryActivate();
        }
    }

    // "Удачная возможность для тарана" - цель в разумном диапазоне дистанций (не вплотную, но и
    // не так далеко, чтобы машина успела свернуть с курса за время разгона), почти прямо по
    // курсу машины, и между нами нет препятствия - иначе AI на форсаже впечатался бы в стену.
    bool IsGoodRammingOpportunity()
    {
        Vector3 toTarget = currentTarget.position - transform.position;
        toTarget.y = 0f;

        float distance = toTarget.magnitude;
        if(distance < nitroMinDistance || distance > nitroMaxDistance) return false;

        float angle = Vector3.Angle(transform.forward, toTarget);
        if(angle > nitroAngleThreshold) return false;

        if(Physics.Raycast(transform.position + Vector3.up * 0.5f, toTarget.normalized, out RaycastHit hit, distance)){
            if(hit.transform.root != currentTarget.root) return false;
        }

        return true;
    }

    // Три луча (центр, слева, справа) впереди машины. Если центр упирается в препятствие -
    // машина уходит в свободную сторону; если свободно нигде - разворот. Другие машины
    // намеренно НЕ считаются препятствием (см. IsRealObstacle) - иначе AI сворачивал бы прочь
    // от того, кого как раз пытается таранить.
    Vector3 AvoidObstacles(Vector3 desiredDirection)
    {
        Vector3 origin = transform.position + Vector3.up * 0.5f;

        if(IsRealObstacle(origin, transform.forward)){
            bool rightBlocked = IsRealObstacle(origin + transform.right * obstacleSideOffset, transform.forward);
            bool leftBlocked = IsRealObstacle(origin - transform.right * obstacleSideOffset, transform.forward);

            if(!rightBlocked){
                return Quaternion.Euler(0f, 60f, 0f) * transform.forward;
            }else if(!leftBlocked){
                return Quaternion.Euler(0f, -60f, 0f) * transform.forward;
            }else{
                return Quaternion.Euler(0f, 150f, 0f) * transform.forward;
            }
        }

        return desiredDirection;
    }

    bool IsRealObstacle(Vector3 origin, Vector3 direction)
    {
        if(!Physics.Raycast(origin, direction, out RaycastHit hit, obstacleCheckDistance, obstacleAvoidMask)){
            return false;
        }
        // Столкнулись с другой машиной - это не препятствие для объезда, а потенциальная цель.
        return hit.collider.GetComponentInParent<PrometeoCarController>() == null;
    }

    // Рулит в сторону точки и решает, нужен ли ручник для резкого разворота. Газ при этом
    // не отпускаем никогда: у машины на WheelCollider руль доворачивает корпус только пока
    // есть движение вперёд, поэтому обнуление газа в повороте останавливает саму возможность
    // довернуть - машина зависает с большим углом до цели и застревает на месте.
    void DriveTowards(Vector3 targetPosition)
    {
        Vector3 flatDirection = targetPosition - transform.position;
        flatDirection.y = 0f;

        float absAngle = 0f;
        if(flatDirection.sqrMagnitude > 0.01f){
            float signedAngle = Vector3.SignedAngle(transform.forward, flatDirection, Vector3.up);
            absAngle = Mathf.Abs(signedAngle);

            if(signedAngle > 3f){
                car.TurnRight();
            }else if(signedAngle < -3f){
                car.TurnLeft();
            }else{
                car.ResetSteeringAngle();
            }
        }

        bool fastEnoughToDrift = Mathf.Abs(car.carSpeed) > minSpeedForHandbrakeTurn;

        if(absAngle > sharpTurnAngle && fastEnoughToDrift){
            car.Handbrake();
            isHandbraking = true;
        }else if(isHandbraking){
            car.RecoverTraction();
            isHandbraking = false;
        }

        // Газуем всегда, в том числе во время ручника - иначе занос не на чем будет тянуть.
        car.GoForward();
    }

    // Если текущая цель сильно оторвалась, временно поднимаем потолок скорости AI, чтобы он мог
    // догнать; если AI близко или обгоняет - потолок возвращается к обычному значению.
    void UpdateRubberBanding()
    {
        rubberBandTimer -= Time.deltaTime;
        if(rubberBandTimer > 0f) return;
        rubberBandTimer = rubberBandUpdateInterval;

        if(currentTarget == null){
            car.SetMaxSpeed(baseMaxSpeed);
            return;
        }

        float distance = Vector3.Distance(transform.position, currentTarget.position);
        float t = Mathf.Clamp01(distance / rubberBandDistance);
        float multiplier = Mathf.Lerp(1f, maxSpeedBoostMultiplier, t);
        car.SetMaxSpeed(Mathf.RoundToInt(baseMaxSpeed * multiplier));
    }

    // Раз в stuckCheckInterval проверяем реальное смещение машины (а не мгновенную скорость).
    // Если машина застряла - сдаёт назад, затем берёт паузу (reverseCooldown).
    void CheckIfStuck()
    {
        stuckCheckTimer += Time.deltaTime;
        if(stuckCheckTimer < stuckCheckInterval) return;

        float movedDistance = Vector3.Distance(transform.position, lastCheckPosition);
        lastCheckPosition = transform.position;
        stuckCheckTimer = 0f;

        if(movedDistance < minMoveDistance && reverseCooldownTimer <= 0f){
            isReversing = true;
            reverseTimer = reverseDuration;
        }
    }

    void HandleReverse()
    {
        car.GoReverse();
        reverseTimer -= Time.deltaTime;
        if(reverseTimer <= 0f){
            isReversing = false;
            reverseCooldownTimer = reverseCooldown;
            stuckCheckTimer = 0f;
            lastCheckPosition = transform.position;
        }
    }

    // Считает, сколько ДРУГИХ живых машин находится в clumpCheckRadius - если их несколько,
    // AI, скорее всего, застрял в толпе, а не просто едет рядом с одной машиной.
    int CountNearbyCars()
    {
        int count = 0;
        foreach(PrometeoCarController candidate in PrometeoCarController.AllCars){
            if(candidate == null || candidate == car) continue;
            CarHealth health = candidate.GetComponent<CarHealth>();
            if(health != null && health.isDead) continue;
            if(Vector3.Distance(transform.position, candidate.transform.position) <= clumpCheckRadius){
                count++;
            }
        }
        return count;
    }

    // Копит clumpTimer, пока рядом держится куча машин; вне кучи таймер сразу сбрасывается -
    // важна именно непрерывность, а не суммарное время за всю игру. По истечении таймера решение
    // отъезжать принимается не гарантированно, а с вероятностью clumpRetreatChance - иначе вся
    // куча срывалась бы отъезжать одновременно и синхронно, что выглядело бы неестественно.
    // Кому не повезло - остаётся толкаться дальше и получит следующий шанс через clumpTimeToReact.
    void CheckClump()
    {
        if(CountNearbyCars() >= clumpMinNearbyCars){
            clumpTimer += Time.deltaTime;
            if(clumpTimer >= clumpTimeToReact){
                clumpTimer = 0f;
                if(Random.value <= clumpRetreatChance){
                    StartRetreatFromClump();
                }
            }
        }else{
            clumpTimer = 0f;
        }
    }

    void StartRetreatFromClump()
    {
        isRetreatingFromClump = true;
        clumpTimer = 0f;
        retreatManeuverTimer = clumpRetreatTimeout;
        retreatStartPosition = transform.position;
        retreatDirection = ComputeRetreatDirection();
    }

    // Направление - прочь от СРЕДНЕЙ точки соседних машин, а не просто назад: если куча
    // сбоку или спереди, банальный реверс уткнётся в ту же кучу под другим углом.
    Vector3 ComputeRetreatDirection()
    {
        Vector3 sum = Vector3.zero;
        int count = 0;
        foreach(PrometeoCarController candidate in PrometeoCarController.AllCars){
            if(candidate == null || candidate == car) continue;
            CarHealth health = candidate.GetComponent<CarHealth>();
            if(health != null && health.isDead) continue;
            if(Vector3.Distance(transform.position, candidate.transform.position) <= clumpCheckRadius){
                sum += candidate.transform.position;
                count++;
            }
        }
        if(count == 0) return -transform.forward;

        Vector3 clumpCenter = sum / count;
        Vector3 direction = transform.position - clumpCenter;
        direction.y = 0f;

        // Машины могут стоять почти вплотную друг на друге - тогда направление "прочь от
        // центра" вырождается в почти нулевой вектор. Берём случайное направление вместо него.
        if(direction.sqrMagnitude < 0.01f){
            Vector2 randomFlat = Random.insideUnitCircle;
            direction = new Vector3(randomFlat.x, 0f, randomFlat.y);
        }

        return direction.normalized;
    }

    // Едет прочь от кучи (объезжая стены на пути так же, как при таране), пока не отъедет
    // на clumpRetreatDistance или не истечёт таймаут - после этого управление возвращается
    // обычному Ram(), который уже сам разгонится в сторону цели с появившейся дистанции.
    void HandleRetreatFromClump()
    {
        retreatManeuverTimer -= Time.deltaTime;

        float distanceFromStart = Vector3.Distance(transform.position, retreatStartPosition);
        if(distanceFromStart >= clumpRetreatDistance || retreatManeuverTimer <= 0f){
            isRetreatingFromClump = false;
            return;
        }

        Vector3 finalDirection = AvoidObstacles(retreatDirection);
        DriveTowards(transform.position + finalDirection * 10f);
    }

    void OnDrawGizmosSelected()
    {
        if(currentTarget != null){
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, currentTarget.position);
        }
    }
}
