using System.Collections;
using UnityEngine;

public class Cutting : MonoBehaviour
{
    private bool _justCut;

    void Start()
    {
        StartCoroutine(Reset());
    }
    public void OnCollisionEnter(Collision other)
    {
        Cuttable _cuttableObject = other.gameObject.GetComponent<Cuttable>();
        if (_cuttableObject != null && !_justCut)
        {
            _cuttableObject.Cut(transform.position, transform.up);
            StartCoroutine(Reset());
        }
    }

    private IEnumerator Reset()
    {
        _justCut = true;
        yield return new WaitForSeconds(1);
        _justCut = false;
    }
}
