using UnityEngine;

// Повесить на машину игрока (тот же объект, где PrometeoCarController). По нажатию activateKey
// подбрасывает машину вертикально вверх - манёвр уклонения, чтобы перепрыгнуть/избежать
// столкновения с соперником.
[RequireComponent(typeof(PrometeoCarController))]
[RequireComponent(typeof(Rigidbody))]
public class CarJumpBooster : MonoBehaviour
{
    [Tooltip("Клавиша активации прыжка")]
    public KeyCode activateKey = KeyCode.R;
    [Tooltip("На сколько высот собственного корпуса подпрыгивает машина")]
    public float jumpHeightInBodies = 4f;
    [Tooltip("Высота корпуса машины (метры) - используется для расчёта высоты прыжка (jumpHeightInBodies * bodyHeight). Если 0 - определяется автоматически в Awake() по размеру самого крупного меша машины, т.к. разные машины (Truck/F1/Lada/SportCar) сильно отличаются по размеру")]
    public float bodyHeight = 0f;
    [Tooltip("Сколько раз за игру можно прыгнуть (0 - нельзя прыгать вообще)")]
    public int maxUses = 2;

    PrometeoCarController car;
    Rigidbody rb;
    int usesLeft;

    void Awake()
    {
        car = GetComponent<PrometeoCarController>();
        rb = GetComponent<Rigidbody>();
        usesLeft = maxUses;

        if(bodyHeight <= 0f){
            bodyHeight = DetectBodyHeight();
        }
    }

    // Ищем среди всех Renderer машины самый крупный по bounds (та же эвристика "кузов - самый
    // крупный меш на машине", что и в CarDeformation.FindLikelyBodyMeshFilter) и берём высоту
    // его bounds - без этого пришлось бы вручную подбирать bodyHeight на каждом из 4 префабов.
    float DetectBodyHeight()
    {
        float bestSize = -1f;
        float height = 1.5f; // разумное значение по умолчанию, если Renderer вообще не нашёлся

        foreach(Renderer candidate in GetComponentsInChildren<Renderer>()){
            float size = candidate.bounds.size.sqrMagnitude;
            if(size > bestSize){
                bestSize = size;
                height = candidate.bounds.size.y;
            }
        }

        return height;
    }

    void Update()
    {
        // Input.GetKey* - это глобальный опрос клавиатуры, не привязанный к конкретному объекту.
        // Этот же компонент может быть включён и у противников (см. CarRole) - без проверки
        // isPlayerControlled нажатие R игроком одновременно подбрасывало бы все машины сразу.
        if(car.isPlayerControlled && Input.GetKeyDown(activateKey)){
            TryJump();
        }
    }

    // Публичный прыжок - на случай, если позже понадобится вызывать его из EnemyCarAI, как и
    // TryFire()/TryActivate()/TryDropMine() у остального вооружения.
    public void TryJump()
    {
        // enabled - на случай, если этот компонент выключен через CarRole (опциональное
        // "вооружение"). Вызов публичного метода не блокируется выключенным компонентом сам по
        // себе (это влияет только на автоматические Update/FixedUpdate), поэтому проверяем сами.
        if(!enabled || !car.enabled || usesLeft <= 0) return;
        if(!IsGrounded()) return;

        usesLeft--;

        // v = sqrt(2 * g * h) - классическая формула скорости, нужной для подъёма на высоту h
        // под гравитацией. ForceMode.VelocityChange не зависит от массы машины, поэтому не нужно
        // подбирать число под лёгкий F1 или тяжёлый грузовик отдельно - только jumpHeightInBodies.
        float targetHeight = bodyHeight * jumpHeightInBodies;
        float gravity = Mathf.Abs(Physics.gravity.y);
        float jumpSpeed = Mathf.Sqrt(2f * gravity * targetHeight);

        rb.AddForce(Vector3.up * jumpSpeed, ForceMode.VelocityChange);
    }

    // Прыгать можно только "с земли" - хотя бы одно колесо касается поверхности. Иначе машина
    // могла бы прыгнуть повторно прямо в воздухе, пока не кончились заряды.
    bool IsGrounded()
    {
        return car.frontLeftCollider.isGrounded || car.frontRightCollider.isGrounded
            || car.rearLeftCollider.isGrounded || car.rearRightCollider.isGrounded;
    }
}
