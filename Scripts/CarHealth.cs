using System.Collections.Generic;
using UnityEngine;

// Повесить на корневой объект машины (туда же, где PrometeoCarController) - у игрока и у
// каждого противника. Урон от столкновений обрабатывается через стандартный Unity OnCollisionEnter.
// Для работы у машины должен быть Collider (например, BoxCollider на корпусе) и настроены слои.
[RequireComponent(typeof(PrometeoCarController))]
[RequireComponent(typeof(Rigidbody))]
public class CarHealth : MonoBehaviour
{
    [Header("Здоровье")]
    public float maxHealth = 100f;
    public float currentHealth;
    public bool isDead;

    [Header("Урон от столкновения")]
    [Tooltip("Скорость (км/ч), при которой столкновение наносит 100% урона")]
    public float speedFor100PercentDamage = 200f;
    [Tooltip("Ниже этой скорости (км/ч) столкновение урона не наносит")]
    public float minDamageSpeed = 5f;
    [Tooltip("Минимальный интервал между тиками урона от одного и того же соперника (сек) - чтобы урон не сыпался каждый кадр, пока машины прижаты друг к другу")]
    public float damageCooldown = 0.5f;
    [Tooltip("Слой препятствий (стены, отбойники и т.д.) - столкновение с ним наносит урон машине так же, как удар другой машиной")]
    [SerializeField] LayerMask obstacleLayer;

    PrometeoCarController car;
    Rigidbody rb;
    Dictionary<CarHealth, float> lastDamageTime = new Dictionary<CarHealth, float>();
    float lastObstacleDamageTime = -999f;

    [Header("Эффекты")]
    [SerializeField] GameObject deathEffect;
    [Tooltip("Во сколько раз темнеет цвет кузова при уничтожении (0 = чёрный, 1 = без изменений)")]
    [SerializeField] float deathDarkenFactor = 0.15f;
    [Tooltip("Сила, с которой колёса разлетаются при уничтожении машины")]
    [SerializeField] float wheelBlastForce = 6f;
    [Tooltip("Скорость (м/с) подлёта корпуса вверх при уничтожении - не зависит от массы машины")]
    [SerializeField] float bodyLaunchSpeed = 4f;
    [Tooltip("Линейное демпфирование корпуса, пока он ещё летит вверх - гасит рывок от импульса, отвечает за высоту подлёта")]
    [SerializeField] float bodyLaunchDamping = 1f;
    [Tooltip("Линейное демпфирование корпуса после того, как он долетел до пика и начал падать - 0 значит падение идёт под чистой гравитацией, ничем не гасится")]
    [SerializeField] float bodyFallDamping = 0f;

    [Header("Звуки")]
    [Tooltip("AudioSource со звуком взрыва - проигрывается один раз при уничтожении машины. Как и carEngineSound/tireScreechSound в PrometeoCarController, это AudioSource на самой машине (или её дочернем объекте) с уже назначенным в инспекторе AudioClip")]
    [SerializeField] AudioSource explosionSound;
    [Tooltip("AudioSource со звуком удара - проигрывается при столкновении с другой машиной или с препятствием (obstacleLayer), если скорость удара выше minDamageSpeed. Настраивается так же, как explosionSound")]
    [SerializeField] AudioSource collisionSound;
    [Tooltip("Минимальный интервал между проигрываниями звука столкновения (сек) - чтобы звук не сыпался каждый кадр, пока машина прижата к стене/другой машине")]
    [SerializeField] float collisionSoundCooldown = 0.3f;
    float lastCollisionSoundTime = -999f;

    void Awake()
    {
        currentHealth = maxHealth;
        car = GetComponent<PrometeoCarController>();
        rb = GetComponent<Rigidbody>();
    }

    // Столкновение обрабатывается самим движком физики. OnCollisionEnter вызывается
    // на обеих машинах. Урон зависит от того, кем приходится удар:
    // - лоб в лоб: сталкиваются передние части обеих машин — урон получают обе;
    // - в бок или в зад: урон получает тот, в кого врезались (у кого не участвует перед).
    void OnCollisionEnter(Collision collision)
    {
        if(isDead) return;

        bool isObstacle = IsInLayerMask(collision.gameObject.layer, obstacleLayer);

        CarHealth otherHealth = collision.collider.GetComponentInParent<CarHealth>();
        bool isOtherCar = otherHealth != null && otherHealth != this && !otherHealth.isDead && otherHealth.enabled;

        // Звук удара - только при столкновении с другой машиной или с препятствием
        // (obstacleLayer), а не с чем угодно (например, с дорогой).
        if(isOtherCar || isObstacle){
            TryPlayCollisionSound(collision);
        }

        // Препятствие наносит урон самой машине сразу, без учёта перед/зад - в отличие
        // от столкновения машин, тут нет "противника", которому нужно было бы разбираться,
        // чей перед участвовал в ударе.
        if(isObstacle){
            TryDealObstacleDamage();
        }

        if(!isOtherCar) return;

        if(collision.contactCount == 0) return;
        Vector3 normal = collision.GetContact(0).normal;

        // normal направлен от чужого коллайдера к нам. По дот-продукту с forward
        // определяем, чей перед участвует в контакте.
        bool ourFrontInvolved = Vector3.Dot(transform.forward, normal) < -0.5f;
        bool otherFrontInvolved = Vector3.Dot(otherHealth.transform.forward, normal) > 0.5f;

        // Если ни один перед не участвует (обе машины ударились боком/задом), урон не наносим.
        if(!ourFrontInvolved && !otherFrontInvolved) return;

        // Наш перед участвует — мы наносим урон противнику.
        // Чужой перед участвует — он нанесёт урон нам в своём событии OnCollisionEnter.
        if(ourFrontInvolved){
            TryDealDamage(otherHealth);
        }
    }

    static bool IsInLayerMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    void TryDealDamage(CarHealth target)
    {
        if(target == null || target.isDead) return;

        if(lastDamageTime.TryGetValue(target, out float lastTime) && Time.time - lastTime < damageCooldown) return;
        lastDamageTime[target] = Time.time;
        CleanupDeadEntries();

        float mySpeed = GetActualSpeedKmH();
        if(mySpeed < minDamageSpeed) return;

        float damagePercent = Mathf.Clamp01(mySpeed / speedFor100PercentDamage);
        float damage = target.maxHealth * damagePercent;
        target.TakeDamage(damage);
    }

    // Урон от удара о препятствие (obstacleLayer) - зависит от собственной скорости машины,
    // как и урон от другой машины, но наносится самой себе и по своему отдельному кулдауну
    // (у препятствия нет CarHealth, поэтому lastDamageTime по сопернику тут не подходит).
    void TryDealObstacleDamage()
    {
        if(Time.time - lastObstacleDamageTime < damageCooldown) return;
        lastObstacleDamageTime = Time.time;

        float mySpeed = GetActualSpeedKmH();
        if(mySpeed < minDamageSpeed) return;

        float damagePercent = Mathf.Clamp01(mySpeed / speedFor100PercentDamage);
        float damage = maxHealth * damagePercent;
        TakeDamage(damage);
    }

    float GetActualSpeedKmH()
    {
        return rb.linearVelocity.magnitude * 3.6f;
    }

    // relativeVelocity - это скорость сближения в момент удара (в отличие от GetActualSpeedKmH,
    // которая просто скорость машины) - поэтому звук корректно проигрывается и для лёгкого
    // столкновения на большой скорости, и для сильного лобового удара на встречных курсах.
    void TryPlayCollisionSound(Collision collision)
    {
        if(collisionSound == null) return;

        float impactSpeedKmH = collision.relativeVelocity.magnitude * 3.6f;
        if(impactSpeedKmH < minDamageSpeed) return;

        if(Time.time - lastCollisionSoundTime < collisionSoundCooldown) return;
        lastCollisionSoundTime = Time.time;

        collisionSound.Play();
    }

    void CleanupDeadEntries()
    {
        List<CarHealth> toRemove = null;
        foreach(var pair in lastDamageTime){
            if(pair.Key == null || pair.Key.isDead){
                if(toRemove == null) toRemove = new List<CarHealth>();
                toRemove.Add(pair.Key);
            }
        }
        if(toRemove != null){
            foreach(var key in toRemove){
                lastDamageTime.Remove(key);
            }
        }
    }

    public void TakeDamage(float amount)
    {
        if(isDead) return;

        currentHealth -= amount;
        DamagePopup.Show(transform.position + Vector3.up * 2.2f, amount);
        if(currentHealth <= 0f){
            currentHealth = 0f;
            Die();
        }
    }

    void Die()
    {
        isDead = true;
        car.isPlayerControlled = false;

        EnemyCarAI ai = GetComponent<EnemyCarAI>();
        if(ai != null){
            ai.enabled = false;
        }

        // WheelCollider - это отдельная физическая симуляция подвески, которая работает
        // независимо от скрипта PrometeoCarController (отключение скрипта её не останавливает).
        // Пока колёса-коллайдеры живы, подвеска продолжает "прижимать" корпус к земле и гасит
        // почти любой импульс вверх - поэтому удаляем их ДО прыжка, а не после.
        CleanupWreck();

        rb.mass /= 10f;

        // Раньше тут стоял FreezeAll - корпус физически не мог никуда сдвинуться. Вместо этого
        // даём ему подлететь импульсом (VelocityChange не зависит от массы машины, не нужно
        // подбирать число под тяжёлый корпус), а демпфирование гасит вращение/скорость, чтобы
        // машина естественно "приземлилась" и не кувыркалась бесконечно.
        rb.constraints = RigidbodyConstraints.None;
        rb.linearDamping = bodyLaunchDamping;
        rb.angularDamping = 1f;

        Vector3 bodyLaunchDirection = new Vector3(Random.Range(-0.3f, 0.3f), 1f, Random.Range(-0.3f, 0.3f)).normalized;
        rb.AddForce(bodyLaunchDirection * bodyLaunchSpeed, ForceMode.VelocityChange);
        rb.AddTorque(Random.insideUnitSphere * bodyLaunchSpeed * 0.5f, ForceMode.VelocityChange);

        // Пока корпус летит вверх, демпфирование как и раньше гасит рывок от импульса (высота
        // подлёта не меняется). Но то же самое демпфирование потом мешает падению - гасит
        // скорость от гравитации так же, как и скорость от импульса. Поэтому как только корпус
        // долетает до пика (вертикальная скорость обнуляется) и начинает падать, резко снижаем
        // демпфирование, чтобы падение шло быстро, а не "как пёрышко".
        StartCoroutine(SpeedUpFallAfterPeak());

        if(deathEffect != null){
            var explosion = Instantiate(deathEffect, transform.position, transform.rotation);
            Destroy(explosion, 2f);
        }

        if(explosionSound != null){
            explosionSound.Play();
        }

        DarkenBody();
        DetachWheels();

        // CancelInvoke() останавливает CarSpeedUI/CarSounds - PrometeoCarController запускает их
        // через InvokeRepeating, а это не завязано на enabled: отключение компонента само по себе
        // НЕ останавливает уже запущенные Invoke, звук мотора продолжал бы играть и после смерти.
        car.CancelInvoke();
        if(car.carEngineSound != null){
            car.carEngineSound.Stop();
        }
        if(car.tireScreechSound != null){
            car.tireScreechSound.Stop();
        }

        // Отключаем весь контроллер машины ПОСЛЕ отрыва колёс - иначе его Update() каждый кадр
        // насильно подгоняет позицию колёс под WheelCollider (AnimateWheelMeshes) и оторванные
        // колёса не смогут улететь, их будет тянуть обратно.
        car.enabled = false;
    }

    // Ждёт, пока корпус долетит до верхней точки (вертикальная скорость станет <= 0), и только
    // тогда снижает линейное демпфирование - если снизить его сразу, то во время подъёма ничего
    // не изменится (гравитация и так тянет вниз, а вверх корпус уже летит по инерции импульса),
    // а вот на падении демпфирование иначе так и продолжало бы гасить скорость от гравитации.
    System.Collections.IEnumerator SpeedUpFallAfterPeak()
    {
        while(rb != null && rb.linearVelocity.y > 0f){
            yield return null;
        }
        if(rb != null){
            rb.linearDamping = bodyFallDamping;
        }
    }

    // Удаляет всё, что мёртвой машине больше не нужно: WheelCollider (физика подвески, мешает
    // корпусу подлететь), системы частиц (дым/пыль от шин) и следы шин (TrailRenderer).
    void CleanupWreck()
    {
        // Destroy() удаляет компонент только в конце кадра - в момент самого импульса подвеска
        // ещё жива и продолжает давить корпус вниз. enabled = false отключает её мгновенно,
        // а Destroy() потом просто подчищает компонент за ненадобностью.
        DisableAndDestroy(car.frontLeftCollider);
        DisableAndDestroy(car.frontRightCollider);
        DisableAndDestroy(car.rearLeftCollider);
        DisableAndDestroy(car.rearRightCollider);

        if(car.RLWParticleSystem != null) Destroy(car.RLWParticleSystem.gameObject);
        if(car.RRWParticleSystem != null) Destroy(car.RRWParticleSystem.gameObject);
        if(car.RLWTireSkid != null) Destroy(car.RLWTireSkid.gameObject);
        if(car.RRWTireSkid != null) Destroy(car.RRWTireSkid.gameObject);

        // Подчищаем заодно любые другие системы частиц, которые могли остаться в детях
        // (например, эффекты из папки Effects, не привязанные к полям выше явно).
        foreach(ParticleSystem ps in GetComponentsInChildren<ParticleSystem>()){
            if(ps != null){
                Destroy(ps.gameObject);
            }
        }
    }

    void DisableAndDestroy(WheelCollider wheelCollider)
    {
        if(wheelCollider == null) return;
        wheelCollider.enabled = false;
        Destroy(wheelCollider);
    }

    // Затемняет все материалы кузова (через Renderer.materials - создаёт собственную копию
    // материалов для этой машины, не портит общий ассет, которым пользуются другие машины).
    void DarkenBody()
    {
        MeshRenderer bodyRenderer = GetComponentInChildren<MeshRenderer>();
        if(bodyRenderer == null) return;

        foreach(Material mat in bodyRenderer.materials){
            if(mat.HasProperty("_BaseColor")){
                Color c = mat.GetColor("_BaseColor");
                mat.SetColor("_BaseColor", new Color(c.r * deathDarkenFactor, c.g * deathDarkenFactor, c.b * deathDarkenFactor, c.a));
            }
            if(mat.HasProperty("_Color")){
                Color c = mat.GetColor("_Color");
                mat.SetColor("_Color", new Color(c.r * deathDarkenFactor, c.g * deathDarkenFactor, c.b * deathDarkenFactor, c.a));
            }
            if(mat.HasProperty("_EmissionColor")){
                mat.SetColor("_EmissionColor", Color.black);
            }
        }
    }

    // Отрывает визуальные меши колёс от машины и раскидывает их в стороны физикой.
    // Сами WheelCollider к этому моменту уже удалены в CleanupWreck().
    void DetachWheels()
    {
        GameObject[] wheelMeshes = { car.frontLeftMesh, car.frontRightMesh, car.rearLeftMesh, car.rearRightMesh };

        // Коллайдеры, которые остаются на машине (корпус + сами WheelCollider подвески) -
        // отлетающие колёса не должны с ними физически сталкиваться, иначе при пересечении
        // объёмов в момент создания коллайдера физика "выталкивает" колесо огромной силой,
        // которая полностью забивает наш собственный (гораздо более слабый) AddForce.
        Collider[] carColliders = GetComponentsInChildren<Collider>();
        List<Collider> detachedWheelColliders = new List<Collider>();

        foreach(GameObject wheelMesh in wheelMeshes){
            if(wheelMesh == null) continue;

            wheelMesh.transform.SetParent(null, true);

            SphereCollider wheelCollider = wheelMesh.GetComponent<Collider>() as SphereCollider;
            if(wheelCollider == null){
                wheelCollider = wheelMesh.AddComponent<SphereCollider>();
                wheelCollider.radius = 0.3f; // примерно реальный радиус колеса, а не дефолтные 0.5
            }

            foreach(Collider carCollider in carColliders){
                if(carCollider != null){
                    Physics.IgnoreCollision(wheelCollider, carCollider, true);
                }
            }
            foreach(Collider otherWheelCollider in detachedWheelColliders){
                Physics.IgnoreCollision(wheelCollider, otherWheelCollider, true);
            }
            detachedWheelColliders.Add(wheelCollider);

            Rigidbody wheelRb = wheelMesh.AddComponent<Rigidbody>();
            wheelRb.mass = 0.4f; // колесо лёгкое - тот же импульс даёт больше скорости
            // Без затухания круглое колесо с ненулевой угловой скоростью катится по земле
            // практически бесконечно (трения о землю недостаточно, чтобы само погасить вращение
            // за разумное время) - угловое и линейное демпфирование гасят его естественно.
            wheelRb.linearDamping = 0.5f;
            wheelRb.angularDamping = 0.8f;

            // Y всегда заметно больше X/Z - гарантирует, что колесо явно подскочит вверх,
            // а не просто откатится в сторону, и уже потом разлетится по бокам под гравитацией.
            Vector3 launchDirection = new Vector3(Random.Range(-1f, 1f), Random.Range(2f, 3f), Random.Range(-1f, 1f)).normalized;
            wheelRb.AddForce(launchDirection * wheelBlastForce, ForceMode.Impulse);
            wheelRb.AddTorque(Random.insideUnitSphere * wheelBlastForce, ForceMode.Impulse);
        }
    }
}
