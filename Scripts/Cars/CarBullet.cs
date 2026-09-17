using UnityEngine;

// Пуля пулемёта. Летит по прямой БЕЗ физики (Rigidbody) - вместо этого каждый кадр двигается
// вручную и проверяет попадание через Physics.Raycast от текущей позиции до следующей. Это
// надёжнее физического столкновения: при высокой скорости обычный Rigidbody может "проскочить"
// тонкий коллайдер между кадрами (туннелирование), а рейкаст на весь пройденный за кадр отрезок
// такого не допускает. Все параметры выставляет CarMachineGuns сразу после Instantiate.
public class CarBullet : MonoBehaviour
{
    [HideInInspector] public float speed;
    [HideInInspector] public float maxDistance;
    [HideInInspector] public int damage;
    [HideInInspector] public Transform shooterRoot;

    float traveledDistance;

    void Update()
    {
        float step = speed * Time.deltaTime;

        // Попадание в саму машину-стрелка игнорируем (не засчитываем как удар и не уничтожаем
        // пулю) - точка вылета стоит вплотную к корпусу, и в первые кадры луч иначе почти всегда
        // упирался бы в собственный коллайдер машины.
        if(Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, step) && hit.transform.root != shooterRoot){
            CarHealth health = hit.collider.GetComponentInParent<CarHealth>();
            if(health != null && !health.isDead){
                health.TakeDamage(damage);
            }
            Destroy(gameObject);
            return;
        }

        transform.position += transform.forward * step;
        traveledDistance += step;

        if(traveledDistance >= maxDistance){
            Destroy(gameObject);
        }
    }
}
