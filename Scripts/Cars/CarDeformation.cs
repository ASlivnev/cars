using UnityEngine;

// Повесить на корневой объект машины (туда же, где CarHealth). При столкновении мнёт вершины
// меша кузова рядом с точкой удара - чисто визуальный эффект, не связан с логикой урона,
// поэтому мнутся ОБЕ машины при достаточно сильном столкновении, а не только "проигравшая".
[RequireComponent(typeof(Rigidbody))]
public class CarDeformation : MonoBehaviour
{
    [Tooltip("Меш кузова, который будет мяться. Если не назначить - берётся первый MeshFilter среди дочерних объектов")]
    public MeshFilter bodyMeshFilter;

    [Header("Деформация")]
    [Tooltip("Минимальная относительная скорость столкновения (м/с), ниже которой вмятин не будет")]
    public float minImpactSpeed = 3f;
    [Tooltip("Радиус вмятины вокруг точки удара, в локальных единицах меша кузова")]
    public float dentRadius = 1.2f;
    [Tooltip("Насколько сильно вминаются вершины на единицу скорости удара")]
    public float dentStrengthPerSpeed = 0.03f;
    [Tooltip("Максимальная суммарная просадка одной вершины от исходной формы - чтобы кузов не проваливался в себя после множества ударов")]
    public float maxDentDepth = 0.5f;

    Mesh mesh;
    Vector3[] vertices;
    Vector3[] originalVertices;

    void Start()
    {
        if(bodyMeshFilter == null){
            bodyMeshFilter = FindLikelyBodyMeshFilter();
        }

        if(bodyMeshFilter == null) return;

        // Read/Write Enabled может быть выключен в настройках импорта модели (это дефолт для
        // многих импортированных FBX) - тогда mesh.vertices кидает исключение. Вместо краха
        // просто отключаем деформацию для этой машины и явно говорим, что нужно поправить.
        if(bodyMeshFilter.sharedMesh == null || !bodyMeshFilter.sharedMesh.isReadable){
            Debug.LogWarning($"CarDeformation: меш '{bodyMeshFilter.name}' нельзя читать в рантайме (Read/Write Enabled выключен в Import Settings этой модели) - деформация корпуса для этой машины отключена. Включите Read/Write Enabled на меше и нажмите Apply.", this);
            return;
        }

        // .mesh (а не .sharedMesh) сам делает уникальную копию под этот конкретный
        // экземпляр - не портим общий ассет меша, используемый другими машинами.
        mesh = bodyMeshFilter.mesh;
        vertices = mesh.vertices;
        originalVertices = (Vector3[])vertices.Clone();
    }

    // Если bodyMeshFilter не назначен вручную, среди всех MeshFilter в детях берём не первый
    // попавшийся (это часто оказывается мелкая деталь вроде поворотника или зеркала), а тот,
    // у кого самый большой bounds - эвристика "кузов обычно самый крупный меш на машине".
    MeshFilter FindLikelyBodyMeshFilter()
    {
        MeshFilter best = null;
        float bestSize = -1f;

        foreach(MeshFilter candidate in GetComponentsInChildren<MeshFilter>()){
            if(candidate.sharedMesh == null) continue;

            float size = candidate.sharedMesh.bounds.size.sqrMagnitude;
            if(size > bestSize){
                bestSize = size;
                best = candidate;
            }
        }

        return best;
    }

    void OnCollisionEnter(Collision collision)
    {
        if(mesh == null) return;

        float impactSpeed = collision.relativeVelocity.magnitude;
        if(impactSpeed < minImpactSpeed) return;

        ContactPoint contact = collision.GetContact(0);
        Vector3 localPoint = bodyMeshFilter.transform.InverseTransformPoint(contact.point);
        Vector3 localDirection = bodyMeshFilter.transform.InverseTransformDirection(contact.normal).normalized;

        float strength = impactSpeed * dentStrengthPerSpeed;

        for(int i = 0; i < vertices.Length; i++){
            float distance = Vector3.Distance(vertices[i], localPoint);
            if(distance > dentRadius) continue;

            float falloff = 1f - (distance / dentRadius);
            Vector3 displaced = vertices[i] + localDirection * strength * falloff;

            Vector3 fromOriginal = displaced - originalVertices[i];
            if(fromOriginal.magnitude > maxDentDepth){
                displaced = originalVertices[i] + fromOriginal.normalized * maxDentDepth;
            }

            vertices[i] = displaced;
        }

        mesh.vertices = vertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
}
