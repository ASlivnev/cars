using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour {

	[Tooltip("Машина, за которой следит камера. Если не назначить вручную - камера сама найдёт машину игрока среди PrometeoCarController.AllCars (первую, у которой CarRole.isPlayer == true)")]
	public Transform carTransform;
	public PrometeoCarController carController;
	[Range(1, 10)]
	public float followSpeed = 2;
	[Range(1, 10)]
	public float lookSpeed = 5;

	[Header("FOV в зависимости от скорости")]
	public float minFOV = 20f;  // FOV, когда машина стоит или едет медленнее speedThreshold
	public float maxFOV = 40f;  // FOV, когда машина едет быстрее speedThreshold
	public float speedThreshold = 15f; // км/ч - порог, ниже которого FOV возвращается к minFOV
	public float fovSmooth = 3f;

	Vector3 initialCameraPosition;
	Vector3 initialCarPosition;
	Vector3 absoluteInitCameraPosition;
	Camera cam;

	void Start(){
		if(carTransform == null){
			FindPlayerCar();
		}

		cam = GetComponent<Camera>();
		if(carController == null && carTransform != null){
			carController = carTransform.GetComponent<PrometeoCarController>();
		}

		if(carTransform == null){
			Debug.LogWarning("CameraFollow: машина игрока не найдена на сцене (нет PrometeoCarController с CarRole.isPlayer == true)", this);
			return;
		}

		initialCameraPosition = gameObject.transform.position;
		initialCarPosition = carTransform.position;
		absoluteInitCameraPosition = initialCameraPosition - initialCarPosition;
	}

	// Ищет машину игрока среди всех зарегистрированных машин на сцене - это первая, у которой
	// CarRole.isPlayer == true. CarRole стоит на общем префабе машины (см. CarRole.cs), поэтому
	// именно эта галочка теперь отличает игрока от ботов, без ручного перетаскивания ссылки на
	// конкретный объект в инспекторе камеры.
	void FindPlayerCar(){
		foreach(PrometeoCarController candidate in PrometeoCarController.AllCars){
			if(candidate == null) continue;

			CarRole role = candidate.GetComponent<CarRole>();
			if(role != null && !role.isPlayer) continue;

			carTransform = candidate.transform;
			carController = candidate;
			return;
		}
	}

	void FixedUpdate()
	{
		// Машина могла быть уничтожена (CarHealth.Die() делает Destroy(gameObject)) -
		// без этой проверки камера падает с MissingReferenceException.
		if(carTransform == null) return;

		//Look at car
		Vector3 _lookDirection = (new Vector3(carTransform.position.x, carTransform.position.y, carTransform.position.z)) - transform.position;
		Quaternion _rot = Quaternion.LookRotation(_lookDirection, Vector3.up);
		transform.rotation = Quaternion.Lerp(transform.rotation, _rot, lookSpeed * Time.deltaTime);

		//Move to car
		Vector3 _targetPos = absoluteInitCameraPosition + carTransform.transform.position;
		transform.position = Vector3.Lerp(transform.position, _targetPos, followSpeed * Time.deltaTime);

		//FOV in function of speed
		if(cam != null && carController != null){
			float targetFOV = Mathf.Abs(carController.carSpeed) >= speedThreshold ? maxFOV : minFOV;
			cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, fovSmooth * Time.deltaTime);
		}

	}

}
