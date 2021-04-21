using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Cutting.Jobs
{
    [BurstCompile]
    public struct ConnectEndVerticesJob : IJob
    {
        [ReadOnly] public NativeList<float2> verticesOnPlane; 
        
        public NativeArray<int> edgesToLeft, edgesToRight;
        
        private int _withLeftFree, _withRightFree;
        
        public void Execute()
        {
            _withLeftFree = _withRightFree = -1;
            
            for (var index = 0; index < verticesOnPlane.Length; index++)
            {
                var left = edgesToLeft[index];
                if (left < 0)
                    _withLeftFree = index;
    
                var right = edgesToRight[index];
                if (right < 0)
                    _withRightFree = index;

                if (_withLeftFree < 0 || _withRightFree < 0) 
                    continue;
                
                edgesToLeft[_withLeftFree] = _withRightFree;
                edgesToRight[_withRightFree] = _withLeftFree;
            }
        }
    }
}
