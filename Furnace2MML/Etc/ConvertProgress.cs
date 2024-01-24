using System.Windows.Controls;
using System.Windows.Threading;
namespace FurnaceCommandStream2MML.Etc;

public static class ConvertProgress
{
    public static ConvertStage Current { get; set; }
    public static ProgressBar ProgressBar { get; set; }
    public static bool[] Progress { get; } = new bool[(int) ConvertStage.COMPLETED + 1];

    public static void SetProgress(ConvertStage stage, bool status = true)
    {
        if(status)
            Current = stage;
        Progress[(int)stage] = status;
        Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Render, () => {
        // ProgressBar
        });
    }
}

public enum ConvertStage
{
    PARSE_TEXT_INIT, PARSE_TEXT_SONG_INFO, PARSE_TEXT_INST, PARSE_TEXT_SUBSONG, PARSE_TEXT_PATTERN,
    PARSE_CMD_INIT, PARSE_CMD, PARSE_CMD_POST,
    CONVERT_META, CONVERT_INST, CONVERT_LOOP_POINT, CONVERT_NOTE, CONVERT_DRUM,
    COUNT_CHAR, GET_OUTPUT_SIZE, 
    COMPLETED 
}