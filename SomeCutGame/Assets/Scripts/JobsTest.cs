using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;

public class JobsTest : MonoBehaviour
{
    void Start()
    {
        NativeQueue<TwoInt> _twoInt = new NativeQueue<TwoInt>(Allocator.TempJob);

        FillTwoIntJob _fillTwoIntJob = new FillTwoIntJob
        {
            twoInt = _twoInt.ToConcurrent()
        };

        _fillTwoIntJob.Schedule(100, 10).Complete();

        while (_twoInt.Count > 0)
        {
            var _values = _twoInt.Dequeue();
            string _output = _values.one.ToString() + " | " + _values.two.ToString();
            print(_output);
        }

        _twoInt.Dispose();
    }

    [System.Serializable]
    public struct TwoInt
    {
        public int one, two;
    }

    [BurstCompile]
    public struct FillTwoIntJob : IJobParallelFor
    {
        [WriteOnly] public NativeQueue<TwoInt>.Concurrent twoInt;

        public void Execute(int index)
        {
            twoInt.Enqueue(new TwoInt
            {
                one = index,
                two = index
            });
        }
    }
}

