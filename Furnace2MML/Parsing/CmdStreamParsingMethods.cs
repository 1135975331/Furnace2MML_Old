using FurnaceCommandStream2MML.Etc;
using static FurnaceCommandStream2MML.Etc.PublicValue;
using static FurnaceCommandStream2MML.Utils.CmdStreamToMMLUtil;
namespace Furnace2MML.Parsing;

public class CmdStreamParsingMethods
{
    /// <summary>
    /// 마지막 줄이 >> LOOP 0 인 경우, Order의 첫 줄에 노트가 있으면 중복되는 Command Stream이 생김
    /// 이 메소드는 이러한 중복을 제거함
    /// (중복을 제거하면서 curTick값도 재조정함)
    /// </summary>
    /// <param name="curTick"></param>
    /// <param name="lastLine"></param>
    public void RemoveDuplicatedEventsAtTheEnd(ref int curTick, string lastLine)
    {
        var noteCmds = NoteCmds;
        var drumCmds = DrumCmds;
        
        if(lastLine.Equals(">> END")) // 마지막 줄이 >> END인 경우 중복이 발생하지 않으므로 아래 코드를 실행시키지 않는다
            return;
        
        var lastTick         = int.MinValue;
        var totalSkippedTick = PublicValue.OrderStartTicks[^1].TotalSkippedTick;
		
        for(var chNum = 0; chNum < 9; chNum++) {
            var noteCmdCh = noteCmds[chNum];
            if(noteCmdCh.Count == 0)
                continue;
			
            RemoveDuplication(noteCmdCh);
        }

        if(drumCmds.Count != 0)
            RemoveDuplication(drumCmds);
        
        curTick = lastTick;
        return;
        
        #region Local Functions
        /* ---------------------- Local Function ------------------------ */
        void RemoveDuplication(List<FurnaceCommand> cmdList)
        {
            var lastCmdTick     = cmdList[^1].Tick;
            var minTickPerOrder = TickPerUnitChanges.Min(tickPerUnit => tickPerUnit.TickPerOrder);
            
            if(lastCmdTick != 0 && (lastCmdTick+totalSkippedTick) % minTickPerOrder == 0)
                cmdList.RemoveAll(cmd => cmd.Tick == lastCmdTick);

            var newLastDrumCmdTick = cmdList.Count != 0 ? cmdList[^1].Tick : 0;
            lastTick = Math.Max(lastTick, newLastDrumCmdTick);
        }
        #endregion
    }


    /// <summary>
    /// 각 Order의 시작부분에 NOTE_OFF 명령을 끼워넣는 메소드
    /// </summary>
    public void InsertNoteOffAtStartOfEachOrder()
    {
        var noteCmds = NoteCmds;
        var drumCmds = DrumCmds;
        
        for(var chNum = 0; chNum < 9; chNum++) {
            var noteCmdCh    = noteCmds[chNum];
            var noteCmdChLen = noteCmdCh.Count;
            if(noteCmdChLen == 0)
                continue;
            
            InsertNoteOffToList(noteCmdCh, true);
        }
        
        InsertNoteOffToList(drumCmds, false);
        
        return;
        
        #region Local Functions
        /* --------------------------------- Local Function ------------------------------------- */
        void InsertNoteOffToList(List<FurnaceCommand> cmdList, bool isNoteCmd)
        {
            var cmdListLen = cmdList.Count;
            for(var i = 0; i < cmdListLen; i++) {
                var curCmd         = cmdList[i];
                var curCmdOrderNum = curCmd.OrderNum;
                var nextOrderTick  = GetOrderStartTick(curCmdOrderNum + 1);
                
                if(isNoteCmd && curCmd.CmdType.Equals("NOTE_ON"))
                    continue;
                if(curCmdOrderNum >= MaxOrderNum)
                    break;
                
                if(i != cmdListLen - 1) { // 마지막 인덱스가 아닌 경우에는 현재 Cmd와 다음 Cmd의 Order가 서로 다를때만 NOTE_OFF삽입
                    var nextCmd         = cmdList[i + 1];
                    var nextCmdOrderNum = nextCmd.OrderNum;

                    if(curCmdOrderNum >= nextCmdOrderNum) // curNoteOrderNum < nextNoteOrderNum 인 경우 아래 코드 실행
                        continue;
                    if(nextCmd.Tick == GetOrderStartTick(curCmdOrderNum+1)) // 다음 Order가 시작하는 틱에 Cmd가 있는 경우
                        continue;
                }

                var noteOffCmd = new FurnaceCommand(nextOrderTick, (byte)(curCmdOrderNum+1), curCmd.Channel, "NOTE_OFF", 0, 0);
                cmdList.Insert(i + 1, noteOffCmd);
                cmdListLen++;
            }
        }
        #endregion
    }

    /// <summary>
    /// 
    /// </summary>
    public void RemoveUselessPortamentoCommands()
    {
        var noteCmds = NoteCmds;
        
        for(var chNum = 0; chNum < 9; chNum++) {
            var noteCmdChList    = noteCmds[chNum];
            var noteCmdChLen = noteCmdChList.Count;
            if(noteCmdChLen == 0)
                continue;

            var curTick     = -1;
            var cmdsToRemove = new List<FurnaceCommand>();
            
            var hintPortaFound  = false;
            var prePortaFound   = false;
            var hintLegatoFound = false;
            
            for(var i = 0; i < noteCmdChLen - 1; i++) {
                var curCmd = noteCmdChList[i];
                if(curTick != curCmd.Tick) { // 틱이 바뀌면 초기화
                    hintPortaFound  = false;
                    prePortaFound   = false;
                    hintLegatoFound = false;
                    curTick         = curCmd.Tick;
                    cmdsToRemove.Clear();
                }

                switch(curCmd.CmdType) {
                    case "HINT_PORTA":  
                        hintPortaFound = true; 
                        if(curCmd.Value2 == 0)  // If Value2 of the HINT_PORTA is 0, it's useless
                            RemoveCmd(curCmd, noteCmdChList, ref noteCmdChLen);
                        else
                            cmdsToRemove.Add(curCmd);
                        break;
                    case "PRE_PORTA": 
                        prePortaFound = true;
                        RemoveCmd(curCmd, noteCmdChList, ref noteCmdChLen);
                        break;
                    case "HINT_LEGATO":
                        hintLegatoFound = true;
                        cmdsToRemove.Add(curCmd);
                        break;
                }
                
                
                if(hintPortaFound && prePortaFound && hintLegatoFound) { // 같은 틱 내에 해당 3개의 명령이 모두 발견된 경우 Portamento 관련 명령 모두 삭제
                    foreach(var cmd in cmdsToRemove)
                        RemoveCmd(cmd, noteCmdChList, ref noteCmdChLen);
                    
                    i = GetNextTickIdx(noteCmdChList, curTick, out _) - 1;
                }
                //  같은 틱 내에 HINT_PORTA, PRE_PORTA, HINT_LEGATO가 모두 발견되는 경우
                //  해당 틱 내의 세 명령어를 모두 삭제함
                
                #region Local Functions
                void RemoveCmd(FurnaceCommand cmdToBeRemoved, List<FurnaceCommand> cmdList, ref int cmdListLen)
                {
                    cmdList.Remove(cmdToBeRemoved);
                    cmdListLen -= 1;
                }
                #endregion
            }
        }
    }
    
}