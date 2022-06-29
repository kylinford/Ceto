using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DemoCameraAnimation : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(AnimationEnumerator());

    }


    private IEnumerator AnimationEnumerator()
    {
        yield return RotateEnumerator(new Vector3(0, -12, 0), 5);
        yield return new WaitForSeconds(1);
        StartCoroutine(RotateEnumerator(new Vector3(7, 0, 0), 13));
        yield return TranslateEnumerator(transform.forward * 140 + new Vector3(0, 25, 0), 13);
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
