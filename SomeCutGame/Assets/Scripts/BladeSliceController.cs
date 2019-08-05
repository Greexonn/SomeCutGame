using UnityEngine;

public class BladeSliceController : MonoBehaviour
{
    [SerializeField] private Vector3 _sliceAngle;
    [SerializeField] private float _sliceSpeed;

    private bool _isInSlice;
    private float _transition;

    void Update()
    {
        if (_isInSlice)
        {
            if (_transition < 1)
            {
                _transition += Time.deltaTime * _sliceSpeed;
                transform.Rotate(_sliceAngle * Time.deltaTime * _sliceSpeed, Space.Self);
            }
            else
            {
                _isInSlice = false;
            }
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _transition = 0;
                _isInSlice = true;
            }
        }
    }
}
