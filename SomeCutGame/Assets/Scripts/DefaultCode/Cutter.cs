using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Cutter : MonoBehaviour
{
    public void OnCollisionEnter(Collision other)
    {
        DefaultCutCode.Cut(other.gameObject, transform.position, transform.up);

        StartCoroutine(Reset());
    }

    private IEnumerator Reset()
    {
        yield return new WaitForSeconds(1);
        DefaultCutCode.currentlyCutting = false;
    }
}
