using System.Text;
using FurnaceCommandStream2MML.Etc;
using FurnaceCommandStream2MML.Utils;
using Application = System.Windows.Application;
namespace FurnaceCommandStream2MML.Conversion;

public static class ConvertCmdStreamToMML
{
    private static byte _arpValue = 0; // 0: arpeggio disabled
    private static byte _arpTickSpeed = 1;
    public static void SetArpSpeed(FurnaceCommand cmd)
        => _arpTickSpeed = (byte)cmd.Value1;
    public static void SetArpeggioStatus(FurnaceCommand cmd)
        => _arpValue = (byte)cmd.Value1;

    public static void ConvertNoteOn(FurnaceCommand cmd, int tickLen, ref int defaultOct, StringBuilder curOrderSb)
    {
        var noteNum = cmd.Value1;
        var mmlNote = CmdStreamToMMLUtil.GetMMLNote(noteNum, ref defaultOct);

        if(_arpValue == 0) {
            curOrderSb.Append(mmlNote).AppendFracLength(tickLen);
        } else { // if arpeggio is enabled
            var arpNote1 = CmdStreamToMMLUtil.GetMMLNote(noteNum + (_arpValue / 16), ref defaultOct);
            var arpNote2 = CmdStreamToMMLUtil.GetMMLNote(noteNum + (_arpValue % 16), ref defaultOct);
            var fracLenSpeed = CmdStreamToMMLUtil.ConvertBetweenTickAndFraction(_arpTickSpeed);
            curOrderSb.Append($"{{{{{mmlNote}{arpNote1}{arpNote2}}}}}").AppendFracLength(tickLen).Append($",{fracLenSpeed}");
        }
    }

    public static void ConvertNoteOff(int tickLen, StringBuilder curOrderSb)
    {
        if(tickLen > 0)
            curOrderSb.Append('r').AppendFracLength(tickLen);
    }
    
    public static void ConvertPanning(FurnaceCommand cmd, int tickLen, StringBuilder curOrderSb)
    {
        var leftPan  = cmd.Value1;
        var rightPan = -cmd.Value2;
	    
        // curOrderSb.Append(" px").Append((leftPan + rightPan) / 2).Append(' ');
        curOrderSb.Append("px").Append((leftPan + rightPan) / 2);
        if(tickLen > 0)
            curOrderSb.Append('r').AppendFracLength(tickLen);
    }
    
    public static void ConvertInstrument(FurnaceCommand cmd, int tickLen, StringBuilder curOrderSb)
    {
        // Conversion Warning: Valid Instrument Type of SSG Channel is @0 ~ @9.
        if(cmd.Channel is >= 6 and <= 8 && cmd.Value1 is not (>= 0 and <= 9)) {
            ((MainWindow)Application.Current.MainWindow!).LogTextBox.AppendText($"Warning: Invalid instrument type for SSG Channel found.\nValid instrument type of SSG Channel is @0 ~ @9.\n[Channel: {cmd.Channel}, Order: {cmd.OrderNum:X2}, Tick: {cmd.Tick}]\n");
            cmd.Value1 = 0;
        }

        curOrderSb.Append($"@{cmd.Value1}");
        if(tickLen > 0)
            curOrderSb.Append('r').AppendFracLength(tickLen);
    }

    public static void ConvertVolume(FurnaceCommand cmd, int tickLen, StringBuilder curOrderSb)
    {
        var volumeValue = cmd.Value1;
        curOrderSb.Append('V').Append(volumeValue);
        if(tickLen > 0)
            curOrderSb.Append('r').AppendFracLength(tickLen);
    }

    public static void ConvertPortamento(List<FurnaceCommand> cmdList, int curCmdIdx, ref int defaultOct,  StringBuilder curOrderSb)
    {
        var curCmd = cmdList[curCmdIdx];
        
        // MML 문법: a&&{b e}4
        if(curCmd.Value1 == -1)
            return;

        // var prevCmd = CmdStreamToMMLUtil.GetFirstCertainCmd(cmdList, curCmdIdx, ["PRE_PORTA"], "backward", true);  
        var prevCmd = CmdStreamToMMLUtil.GetFirstCertainCmd(cmdList, curCmdIdx, ["HINT_PORTA", "NOTE_ON"], "backward");  
        var nextCmd = cmdList[curCmdIdx+1];

        var isEndOfPorta     = nextCmd.CmdType.EqualsAny("HINT_LEGATO", "NOTE_OFF");
        var nextCmdForLength = CmdStreamToMMLUtil.GetFirstCertainCmd(cmdList, curCmdIdx, isEndOfPorta ? ["NOTE_ON", "NOTE_OFF"] : ["HINT_PORTA"], "forward");

        var prevMMLNote = CmdStreamToMMLUtil.GetMMLNote(prevCmd.Value1, ref defaultOct, false); 
        var curMMLNote  = CmdStreamToMMLUtil.GetMMLNote(curCmd.Value1, ref defaultOct, true);
        
        var tickOfFrac1    = CmdStreamToMMLUtil.ConvertBetweenTickAndFraction(1);
        var portaLength    = nextCmdForLength.Tick - curCmd.Tick;
        var isPortaLenLong = portaLength > tickOfFrac1;
        
        if(isPortaLenLong)
            curOrderSb.Append($"&{{{prevMMLNote} {curMMLNote}}}").AppendFracLength(tickOfFrac1).Append($"&{curMMLNote.ToString().Trim(['<', '>'])}").AppendFracLength(portaLength - tickOfFrac1);
        else
            curOrderSb.Append($"&{{{prevMMLNote} {curMMLNote}}}").AppendFracLength(portaLength);
    }
    




    public static int ConvertTickrateToTempo(double tickrate)
    {
        return (int)(tickrate * 2.5);
    }

    private static StringBuilder AppendFracLength(this StringBuilder curOrderSb, int tickLen)
    {
        if(tickLen == 0)
            return curOrderSb;
        
        var mmlFracLen = FormatNoteLength(tickLen, PublicValue.ValidFractionLength, PublicValue.CurDefaultNoteFractionLength);
        curOrderSb.Append(mmlFracLen);
        return curOrderSb;
    }


    /*
     * Kick     - @1 Bass Drum
     * Snare 00 - @2 Snare Drum 1
     * Snare 01 - @64 Snare Drum 2
     * Top   00 - @256 Hi-Hat Open
     * Top   01 - @512 Crash Cymbal
     * Top   02 - @1024 Ride Cymbal
     * HiHat    - @128 Hi-Hat Close
     * Tom   00 - @4 Low Tom
     * Tom   01 - @8 Middle Tom
     * Tom   02 - @16 High Tom
     * Rim      - @32 Rim Shot
     */
    private static string _prevMMLDrum = "";
    private static int _prevOrderNum = -1;
    private static bool _firstDrumProcessed = false;
    public static void ConvertDrumNoteOn(List<int[]> chNums, int curOrderNum, int tickLen, StringBuilder sb)
    {
        var mmlDrum    = DrumConversion.MidiDrumToMMLDrum(chNums);
        var mmlFracLen = FormatNoteLength(tickLen, PublicValue.ValidFractionLength, PublicValue.CurDefaultNoteFractionLength);
        // var mmlFracLen = CommandToMMLUtil.ConvertBetweenTickAndFraction(tickLen);

        var isRest = mmlDrum.Equals("r");
        if(curOrderNum != _prevOrderNum) {
            _prevOrderNum       = curOrderNum;
            _firstDrumProcessed = false;
        }

        if(!isRest && _firstDrumProcessed && mmlDrum.Equals(_prevMMLDrum))
            mmlDrum = ""; // In order to reduce file size
        else if(!isRest) {
            _prevMMLDrum        = mmlDrum;
            _firstDrumProcessed = true;
        }

        sb.Append(isRest ? $"r{mmlFracLen}" : $"{mmlDrum}c{mmlFracLen}");
    }
    

    private static StringBuilder FormatNoteLength(int tickLenP, int[] validFractionLength, int defaultFractionLength)
    {
        var tickLen      = tickLenP;
        var isLengthLong = tickLen >= 192; // 길이가 길면 점분음표 표기 시 오류가 발생함
		
        var strBuilder = new StringBuilder();

        var fracLenResultList = new List<int>();
        var validFracLenIndex = 0;

        while(tickLen > 0) {                                                                               // tickLength -> FractionLength 변환
            var isTickLengthExact = CmdStreamToMMLUtil.GetIsExactFractionLength(tickLen, out var fracLen); // validFractionLength의 길이와 정확이 일치한가의 여부

            if(isTickLengthExact) {
                fracLenResultList.Add(fracLen);
                tickLen -= tickLen;
            } else {
                var curTick = CmdStreamToMMLUtil.ConvertBetweenTickAndFraction(validFractionLength[validFracLenIndex]);

                var isValidFracLen = tickLen / curTick >= 1; //isValidTick, 현재 curTick값에 의한 분수표기가 fracLengthResultList에 들어가는 것이 올바른지 여부
                
                if(isValidFracLen) {
                    fracLenResultList.Add(validFractionLength[validFracLenIndex]);
                    tickLen -= curTick;
                } else
                    validFracLenIndex++;
            }
        }

        for(var i = 0; i < fracLenResultList.Count; i++) { // 변환되어 저장된 값에 따라 문자열 만들기
            var fracLength      = fracLenResultList[i];
            var isDefaultLength = fracLength == defaultFractionLength;
			
            var fracLenStr = fracLength.ToString();

            if(i == 0 && isDefaultLength)
                strBuilder.Append('&');
            else if(i != 0 && !isLengthLong && fracLength == fracLenResultList[i - 1] * 2) // 현재 분수표기 길이 == 이전 분수표기 길이 * 2 => 점n분음표로 나타낼 수 있는가의 여부
                strBuilder.Append('.');
            else
                strBuilder.Append('&').Append(fracLenStr);
            //DebuggingAndTestingTextBox.AppendText($" &{d}");
        }

        // strBuilder = ReplaceComplicatedLengthStr(strBuilder);  // 복잡하게 변환된 길이를 단순하게 되도록 치환함

        return strBuilder.Remove(0, 1);
    }
}