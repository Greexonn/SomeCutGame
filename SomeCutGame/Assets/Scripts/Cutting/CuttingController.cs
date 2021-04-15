using System.Collections;
using UnityEngine;

namespace Cutting
{
    public class CuttingController : MonoBehaviour
    {
        private bool _justCut;

        private void Start()
        {
            StartCoroutine(ResetCoroutine());
        }
        public void OnCollisionEnter(Collision other)
        {
            var cuttableMeshObject = other.gameObject.GetComponent<CuttableMesh>();
            if (cuttableMeshObject == null || _justCut) 
                return;

            var localTransform = transform;
            cuttableMeshObject.Cut(localTransform.position, localTransform.up, localTransform.right, localTransform.forward);
            StartCoroutine(ResetCoroutine());
        }

        private IEnumerator ResetCoroutine()
        {
            _justCut = true;
            yield return new WaitForSeconds(1);
            _justCut = false;
        }
    }
}
