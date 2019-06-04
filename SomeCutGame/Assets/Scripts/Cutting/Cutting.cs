﻿using System.Collections;
using System.Collections.Generic;
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
        CuttableGraph _cuttableObject = other.gameObject.GetComponent<CuttableGraph>();
        if (_cuttableObject != null && !_justCut)
        {
            _cuttableObject.CutAsync(transform.position, transform.up);
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
