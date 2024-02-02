using System.Text;
using FurnaceCommandStream2MML.Etc;
using static FurnaceCommandStream2MML.Etc.PublicValue;
namespace FurnaceCommandStream2MML.Utils;

public static class CmdStreamToMMLUtil
{
    /// <summary>
    ///  Tick 혹은 Fraction 길이를 서로 반대로 바꿔주는 메소드
    /// 해당 메소드에 Tick길이를 넣으면 Fraction길이,
    /// Fraction길이를 넣으면 Tick길이가 반환된다.
    /// </summary>
    /// <param name="value"></param>
    /// <returns>value == Tick => Fraction, value == Fraction => Tick</returns>
    public static int ConvertBetweenTickAndFraction(int value)
    {
        return value switch {
            1  => 96,
            2  => 48,
            3  => 32,
            4  => 24,
            6  => 16,
            8  => 12,
            12 => 8,
            16 => 6,
            24 => 4,
            32 => 3,
            48 => 2,
            96 => 1,
            _  => -1
        };
    }
    
    public static string ConvertChannel(int chNum)
    {
        return chNum switch {
            // FM 1~6
            0 => "A",
            1 => "B",
            2 => "C",
            3 => "D",
            4 => "E",
            5 => "F",
                
            // SSG 1~3
            6 => "G",
            7 => "H",
            8 => "I",
            
            _ => "?"
        };
    }
    

    public static int GetCmdTickLength(List<FurnaceCommand> cmdList, int curCmdIdx)
    {
        var curCmd         = cmdList[curCmdIdx];
        var allNoteInChLen = cmdList.Count;
        if(curCmdIdx + 1 < allNoteInChLen) {
            var otherNote = cmdList[curCmdIdx + 1];
            return otherNote.Tick - curCmd.Tick;
        }

        var songTotalTick = GetLastTick();
        return songTotalTick - curCmd.Tick;
    }
    
    public static StringBuilder GetMMLNote(int noteNum, ref int defaultOct, bool updateDefaultOct = true)
    {
        var sb         = new StringBuilder();
        var octave     = noteNum / 12;
        var pitch      = noteNum % 12;
        var octaveDiff = octave - defaultOct;
	    
        switch(octaveDiff) {
            case > 0: sb.Append(new string('>', octaveDiff)); break;
            case < 0: sb.Append(new string('<', -octaveDiff)); break;
        }
        
        if(updateDefaultOct)
            defaultOct = octave;
	    
        var pitchChar = pitch switch {
            0  => "c",
            1  => "c+",
            2  => "d",
            3  => "d+",
            4  => "e",
            5  => "f",
            6  => "f+",
            7  => "g",
            8  => "g+",
            9  => "a",
            10 => "a+",
            11 => "b",
            _  => throw new ArgumentOutOfRangeException($"Invalid pitch value: {pitch}")
        };
	    
        sb.Append(pitchChar);
        return sb;
    }

    /// <summary>
    /// ExactFractionLength == n분음 표기(1, 2, 4, 8, 16, 32, 64,  3, 6, 12, 24, 48, 96)가 가능한가의 여부
    /// </summary>
    /// <param name="tickLength"></param>
    /// <param name="conversionResult"></param>
    /// <returns></returns>
    public static bool GetIsExactFractionLength(int tickLength, out int conversionResult)
    {
        conversionResult = ConvertBetweenTickAndFraction(tickLength);
        return conversionResult != -1;
    }

    public static FurnaceCommand GetFirstNoteOn(List<FurnaceCommand> cmdsInCh)
        => cmdsInCh.FirstOrDefault(cmd => cmd.CmdType.Equals("NOTE_ON"), new FurnaceCommand(-1, byte.MaxValue, byte.MaxValue, "NO_NOTE_ON", -1, -1));


    

    private static readonly string[] NoteOnOrNoteOff = ["NOTE_ON", "NOTE_OFF"];
    public static int GetDefaultNoteFracLenForThisOrder(List<FurnaceCommand> cmdList, int curOrder, int curIdx)
    {
        var nextOrderStartTick = GetOrderStartTick(curOrder+1);
        
        var fracLenCounts = new Dictionary<int, int>();  // <FracLen, Count>
        
        var cmdListLen = cmdList.Count;
        for(var i = curIdx; i < cmdListLen; i++) {
            var curCmd = cmdList[i];

            if(curCmd.CmdType.EqualsAny(NoteOnOrNoteOff)) {
                var fracLen = ConvertBetweenTickAndFraction(GetCmdTickLength(cmdList, i));
                
                if(!fracLenCounts.TryAdd(fracLen, 1))
                    fracLenCounts[fracLen]++;
            }

            if(curCmd.Tick >= nextOrderStartTick)
                break;
        }
        
        return GetMostFrequentFracLen();
        
        #region Local Functions
        /* -------------------------------------- Local Function -------------------------------------------- */
        int GetMostFrequentFracLen()
        {
            var maxKey = -1;    // MaxFracLenKey
            var maxValue = -1;  // MaxFracLenCountValue

            foreach(var curKey in fracLenCounts.Keys) {
                if(curKey == -1)
                    continue;
                
                var curValue = fracLenCounts[curKey];
                if(curValue > maxValue) {
                    maxValue = curValue;
                    maxKey   = curKey;
                } else if(curValue == maxValue)
                    maxKey = Math.Min(maxKey, curKey);
            }
            
            return maxKey;
        }
        #endregion
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cmdList"></param>
    /// <param name="curIdx"></param>
    /// <param name="cmdTypeToFind"></param>
    /// <param name="direction"></param>
    /// <param name="conditionInverted">결과를 반전시키는가(포함하지 않는 경우로 변경하는가)의 여부</param>
    /// <returns></returns>
    public static FurnaceCommand GetFirstCertainCmd(List<FurnaceCommand> cmdList, int curIdx, string[] cmdTypeToFind, string direction = "forward", bool conditionInverted = false)
    {
        switch(direction) {
            case "forward": {
                var listLen = cmdList.Count;
                for(var i = curIdx + 1; i < listLen; i++)
                    if(SearchCondition(cmdList[i].CmdType))
                        return cmdList[i];
                break;
            }
            case "backward": {
                for(var i = curIdx - 1; i >= 0; i--)
                    if(SearchCondition(cmdList[i].CmdType))
                        return cmdList[i];
                break;
            }
            default:
                throw new ArgumentOutOfRangeException($"Invalid direction argument: {direction}");
        }
        
        var lastTick = GetLastTick();
        return new FurnaceCommand(lastTick, MiscellaneousConversionUtil.GetOrderNum(lastTick), cmdList[0].Channel, "NOTE_OFF", 0, 0);
        // return new FurnaceCommand(-1, -1, "INVALID_NOT_FOUND", -1, -1);

        #region Local Functions
        /* ----------------------------------Local Functions-------------------------------------- */
        bool SearchCondition(string s)
            => conditionInverted ? !cmdTypeToFind.Contains(s) : cmdTypeToFind.Contains(s);
        #endregion
    }



    /// <summary>
    /// 리스트에서 다음 틱의 인덱스를 반환하는 메소드
    /// out 매개변수로 다음 틱 값도 얻을 수 있음
    /// </summary>
    /// <param name="cmdList"></param>
    /// <param name="curTick"></param>
    /// <param name="nextTick"></param>
    /// <returns></returns>
    public static int GetNextTickIdx(List<FurnaceCommand> cmdList, int curTick, out int nextTick)
    {
        var listLen = cmdList.Count;
        for(var i = 0; i < listLen; i++) {
            if(cmdList[i].Tick <= curTick)
                continue;
            nextTick = cmdList[i].Tick;
            return i;
        }
        
        nextTick = -1;
        return -1;
    }
    
    public static int GetOrderStartTick(int orderNum)
        => orderNum <= MaxOrderNum ? OrderStartTicks[orderNum].StartTick : GetLastTick();

    public static int GetLastTick()
        => OrderStartTicks[MaxOrderNum].StartTick + TickPerUnitChanges[^1].TickPerOrder;
}