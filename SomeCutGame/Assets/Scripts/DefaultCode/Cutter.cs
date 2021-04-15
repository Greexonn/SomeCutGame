using System.Collections;
using UnityEngine;

namespace DefaultCode
{
    public class Cutter : MonoBehaviour
    {
        public void OnCollisionEnter(Collision other)
        {
            var localTransform = transform;
            DefaultCutCode.Cut(other.gameObject, localTransform.position, localTransform.up);

            StartCoroutine(ResetCoroutine());
        }

        private static IEnumerator ResetCoroutine()
        {
            yield return new WaitForSeconds(1);
            DefaultCutCode.currentlyCutting = false;
        }
    }
}
