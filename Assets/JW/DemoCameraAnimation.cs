using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DemoCameraAnimation : MonoBehaviour
{
    private Vector3 startPos;
    private Quaternion startRotation;
    // Start is called before the first frame update
    void Start()
    {
        startPos = transform.position;
        startRotation = transform.rotation;
        //StartCoroutine(AnimationEnumeratorLoop());

    }

    private void Update()
    {
        if(Input.GetMouseButtonUp(0))
        {
            StopAllCoroutines();
            StartCoroutine(AnimationEnumerator());
        }
    }

    private IEnumerator AnimationEnumeratorLoop()
    {
        while (true)
        {

            yield return new WaitForSeconds(2);
            yield return AnimationEnumerator();
            yield return new WaitForSeconds(10);
        }
    }

    private IEnumerator AnimationEnumerator()
    {
        transform.position = startPos;
        transform.rotation = startRotation;
        yield return new WaitForSeconds(4);
        yield return RotateEnumerator(new Vector3(0, 45, 0), 7);
        yield return new WaitForSeconds(1);
        //StartCoroutine(RotateEnumerator(new Vector3(7, 0, 0), 13));
        Vector3 forwardNoY = transform.forward;
        forwardNoY.y = 0;
        yield return TranslateEnumerator(forwardNoY * 80, 13);
    }

    private IEnumerator RotateEnumerator(Vector3 angle, float timer)
    {
        Quaternion origin = Quaternion.Euler(transform.localEulerAngles);
        Quaternion target = Quaternion.Euler(transform.localEulerAngles + angle);
        float timeStart = Time.time;
        while (Time.time - timeStart < timer)
        {
            float t = (Time.time - timeStart) / timer;
            Quaternion curr = Quaternion.Lerp(origin, target, t);
            transform.localRotation = curr;
            yield return new WaitForEndOfFrame();
        }
    }
    private IEnumerator TranslateEnumerator(Vector3 move, float timer)
    {
        Vector3 origin = transform.localPosition;
        Vector3 target = transform.localPosition + move;
        float timeStart = Time.time;
        while (Time.time - timeStart < timer)
        {
            float t = (Time.time - timeStart) / timer;
            Vector3 curr = Vector3.Lerp(origin, target, t);
            transform.localPosition = curr;
            yield return new WaitForEndOfFrame();
        }
    }
}
