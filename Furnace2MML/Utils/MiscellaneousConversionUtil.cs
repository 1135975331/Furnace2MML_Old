using FurnaceCommandStream2MML.Etc;
using static FurnaceCommandStream2MML.Etc.PublicValue;
namespace FurnaceCommandStream2MML.Utils;

public static class MiscellaneousConversionUtil
{
    public static byte GetOrderNum(int tick)
    {
        for(byte orderNum = 0; orderNum <= MaxOrderNum; orderNum++) {
            var curOrderStartTick = OrderStartTicks[orderNum].StartTick;
            var nextOrderStartTick = orderNum+1 <= MaxOrderNum ? OrderStartTicks[orderNum+1].StartTick : int.MaxValue;

            if(curOrderStartTick <= tick && tick < nextOrderStartTick)
                return orderNum;
        }
        
        return byte.MaxValue;
    }
}