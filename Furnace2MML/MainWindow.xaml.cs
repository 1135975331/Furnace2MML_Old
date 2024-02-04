using System.ComponentModel;
using System.IO;
using System.Net.Mime;
using System.Text;
using System.Windows;
using Furnace2MML.Parsing;
using FurnaceCommandStream2MML.Conversion;
using FurnaceCommandStream2MML.Etc;
using FurnaceCommandStream2MML.Utils;
using static FurnaceCommandStream2MML.Etc.ErrorWhileConversion;
using static FurnaceCommandStream2MML.Etc.ErrorWhileConversionMethods;
using static FurnaceCommandStream2MML.Etc.PrintLog;
using static FurnaceCommandStream2MML.Etc.PublicValue;
using Application = System.Windows.Application;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;


// todo Progressbar
// todo Document

namespace FurnaceCommandStream2MML;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public string CmdFilePath;
    public string TxtOutFilePath;
    public StreamReader Sr;

    public MainWindow()
    {
        InitializeComponent();
        this.Loaded  += OnWindowLoad;
        this.Closing += OnClosing;

        PrintLog.LogTextBox = LogTextBox;

		#if DEBUG
        var testFilesDirPath = Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName + @"\FurnaceForTest";
        LogDebug($"TestFilesDir: {testFilesDirPath}");
        // GetFilePaths(testFilesDirPath + @"\hurairu\hurairu_cmd.txt", OutputFileType.CMD_STREAM);
        // GetFilePaths(testFilesDirPath + @"\hurairu\hurairu_txt.txt", OutputFileType.TXT_OUTPUT);
        // GetFilePaths(testFilesDirPath + @"\tuya\tuya_cmd.txt", OutputFileType.CMD_STREAM);
        // GetFilePaths(testFilesDirPath + @"\tuya\tuya_txt.txt", OutputFileType.TXT_OUTPUT);
        // GetFilePaths($@"{testFilesDirPath}\okf\okf_cmd.txt", OutputFileType.CMD_STREAM);
        // GetFilePaths($@"{testFilesDirPath}\okf\okf_txt.txt", OutputFileType.TXT_OUTPUT);
        StartConvert();
		#endif
    }

    private void OnWindowLoad(object sender, RoutedEventArgs e)
    {
        Application.Current.MainWindow       = this;
        Application.Current.MainWindow.Title = "Furnace2MML Converter";
    }

    private void CmdFileSelectButton_Click(object sender, RoutedEventArgs e)
        => FileSelect(OutputFileType.CMD_STREAM);

    private void TxtOutFileSelectButton_Click(object sender, RoutedEventArgs e)
        => FileSelect(OutputFileType.TXT_OUTPUT);

    private void FileSelect(OutputFileType outputFileType)
    {
        var openFileDialog = new OpenFileDialog {
            Filter           = "Furnace Command Stream files (*.txt)|*.txt;|All files (*.*)|*.*",
            FilterIndex      = 1,
            RestoreDirectory = true
        };

        if(openFileDialog.ShowDialog() == true)
            GetFilePaths(openFileDialog.FileName, outputFileType); //Get the path of specified file
    }

    public void GetFilePaths(string fileName, OutputFileType outputFileType)
    {
        switch(outputFileType) {
            case OutputFileType.CMD_STREAM:
                CmdFilePath             = fileName;
                CmdFilePathTextBox.Text = fileName;
                break;
            case OutputFileType.TXT_OUTPUT:
                TxtOutFilePath             = fileName;
                TxtOutFilePathTextBox.Text = fileName;
                break;
            default:
                throw new ArgumentOutOfRangeException($"Invalid output file type: {outputFileType}");
        }

        if(!Util.GetFileExtensionFromPath(fileName).Equals("txt")) {
            ResultOutputTextBox.Text = GetErrorMessage(FILE_NOT_VALID);
        }
    }

    private void StartConvert()
    {
        ClearPreviousData();

        // return if the parse method returns false(unsuccessful).
        if(!ParseTextOutput(TxtOutFilePath))
            return;
        if(!ParseCommandStream(CmdFilePath))
            return;

        Convert();
        CountCharSize();

        // LogElapsedTime();
    }

    private static void ClearPreviousData()
    {
        PublicValue.InstDefs.Clear();
        foreach(var noteCmdList in PublicValue.NoteCmds)
            noteCmdList.Clear();
        DrumCmds.Clear();
        OtherEffects.Clear();
        PublicValue.TickPerUnitChanges.Clear();
        PublicValue.OrderStartTicks.Clear();
        MaxOrderNum = -1;
    }

    private bool ParseTextOutput(string textOutputFileName)
    {
        ConvertProgress.Progress[(int) ConvertStage.PARSE_TEXT_INIT] = true;
        try { Sr = new StreamReader(textOutputFileName); } catch(ArgumentException e) {
            ResultOutputTextBox.Text = GetExceptionErrorMessage(FILE_NOT_FOUND, e);
            return false;
        } catch(PathTooLongException e) {
            ResultOutputTextBox.Text = GetExceptionErrorMessage(FILE_PATH_TOO_LONG, e);
            return false;
        } catch(DirectoryNotFoundException e) {
            ResultOutputTextBox.Text = GetExceptionErrorMessage(FILE_NOT_FOUND, e);
            return false;
        } catch(FileNotFoundException e) {
            ResultOutputTextBox.Text = GetExceptionErrorMessage(FILE_NOT_FOUND, e);
            return false;
        } catch(Exception e) {
            ResultOutputTextBox.Text = GetExceptionErrorMessage(UNKNOWN_ERROR, e);
            return false;
        }

        var curReadingLineNum = 0;

        var firstLine = Sr.ReadLineCountingLineNum(ref curReadingLineNum);
        if(firstLine == null) {
            ResultOutputTextBox.Text = GetErrorMessage(FILE_EMPTY);
            return false;
        }
        if(!firstLine.Equals("# Furnace Text Export")) {
            ResultOutputTextBox.Text = GetErrorMessage(NOT_FURNACE_TEXT_EXPORT);
            return false;
        }

#if RELEASE
        try {
#endif
        // # Song Information
        var txtOutputParser = new TxtOutputParsingMethods(Sr);

        while(!Sr.EndOfStream) {
            var line = Sr.ReadLineCountingLineNum(ref curReadingLineNum);
            if(string.IsNullOrEmpty(line))
                continue;

            // Parse Section (# Song Information, # Sound Chips, # Instruments, etc.)
            var curSection = line.Count(ch => ch == '#') is 1 or 2 ? line.Trim('#').Trim() : "";

            switch(curSection) {
                case "Song Information":
                    PublicValue.SongInfo = txtOutputParser.ParseSongInfoSection(ref curReadingLineNum);
                    break;
                case "Song Comments":
                    PublicValue.Memo = txtOutputParser.ParseSongComment(ref curReadingLineNum);
                    break;
                case "Instruments":
                    PublicValue.InstDefs = txtOutputParser.ParseInstrumentDefinition(ref curReadingLineNum);
                    break;
                case "Wavetables": break;
                case "Samples":    break;
                case "Subsongs":
                    PublicValue.Subsong = txtOutputParser.ParseSubsongs(ref curReadingLineNum);
                    // PublicValue.MaxOrderNum  = TxtOutputParsingMethods.ParseMaxOrderNum(sr, ref curReadingLineNum);
                    break;
                case "Patterns":
                    txtOutputParser.ParsePatterns(ref curReadingLineNum);
                    txtOutputParser.SetTickPerUnits();
                    break;
            }
        }

        if(!txtOutputParser.CheckIsSystemValid()) {
            ResultOutputTextBox.Text = GetErrorMessage(SYSTEM_NOT_OPNA);
            return false;
        } else if(PublicValue.Subsong.VirtualTempo[0] != PublicValue.Subsong.VirtualTempo[1]) {
            ResultOutputTextBox.Text = GetErrorMessage(INVALID_VIRT_TEMPO);
            return false;
        }
#if RELEASE
        } catch(Exception e) {
            var errMsg = $"An error has occured while parsing the command stream at line {curReadingLineNum}.\n\nStackTrace: {e.StackTrace}\n\nMsg: {e.Message}\n\n\n\n";
            LogTextBox.Text = errMsg;
        }
#endif
        Sr.Close();
        return true;
    }

    private bool ParseCommandStream(string cmdFileName)
    {
        try { Sr = new StreamReader(cmdFileName); } catch(ArgumentException e) {
            ResultOutputTextBox.Text = GetExceptionErrorMessage(FILE_NOT_FOUND, e);
            return false;
        } catch(PathTooLongException e) {
            ResultOutputTextBox.Text = GetExceptionErrorMessage(FILE_PATH_TOO_LONG, e);
            return false;
        } catch(DirectoryNotFoundException e) {
            ResultOutputTextBox.Text = GetExceptionErrorMessage(FILE_NOT_FOUND, e);
            return false;
        } catch(FileNotFoundException e) {
            ResultOutputTextBox.Text = GetExceptionErrorMessage(FILE_NOT_FOUND, e);
            return false;
        } catch(Exception e) {
            ResultOutputTextBox.Text = GetExceptionErrorMessage(UNKNOWN_ERROR, e);
            return false;
        }

        var curReadingLineNum = 0;

        var firstLine = Sr.ReadLineCountingLineNum(ref curReadingLineNum);
        if(firstLine == null) {
            ResultOutputTextBox.Text = GetErrorMessage(FILE_EMPTY);
            return false;
        }
        if(!firstLine.Equals("# Furnace Command Stream")) {
            ResultOutputTextBox.Text = GetErrorMessage(NOT_FURNACE_CMD_STREAM);
            return false;
        }

#if RELEASE
        try {
#endif
        NoteCmds = new List<FurnaceCommand>[9];
        for(var chNum = 0; chNum < NoteCmds.Length; chNum++)
            NoteCmds[chNum] = [];
        DrumCmds = [];

        // 본격적인 Stream 파싱 시작
        var cmdStreamParser        = new CmdStreamParsingMethods();
        var curTick                = -1;
        var isCurrentSectionStream = false;
        while(!Sr.EndOfStream) {
            var line = Sr.ReadLineCountingLineNum(ref curReadingLineNum);
            if(string.IsNullOrEmpty(line))
                continue;

            if(!isCurrentSectionStream) {
                if(line.Equals("[Stream]"))
                    isCurrentSectionStream = true;
                continue;
            }

            if(line.Contains(">> TICK "))
                curTick = int.Parse(line[8..]);
            else if(line.Contains(">> END") || line.Contains(">> LOOP"))
                cmdStreamParser.RemoveDuplicatedEventsAtTheEnd(ref curTick, line);
            else {
                var split    = line.Trim().Split(" ");
                var orderNum = MiscellaneousConversionUtil.GetOrderNum(curTick);
                var channel  = byte.Parse(split[0].Remove(split[0].Length - 1));
                var cmdType  = split[1];
                var value1   = int.Parse(split[2]);
                var value2   = int.Parse(split[3]);

                if(cmdType.EqualsAny("NOTE_ON", "HINT_PORTA") && channel is >= 0 and <= 5)
                    value1 += 12; // Increases octave of FM channels by 1

                var cmdStruct = new FurnaceCommand(curTick, orderNum, channel, cmdType, value1, value2);

                switch(channel) {
                    case >= 0 and <= 5: // FM 1~6
                        NoteCmds[channel].Add(cmdStruct);
                        break;
                    case >= 6 and <= 8: // SSG 1~3
                        NoteCmds[channel].Add(cmdStruct);
                        break;
                    case >= 9 and <= 14: // Drum
                        DrumCmds.Add(cmdStruct);
                        break;
                    // default: ADPCM << 사용안함
                }
            }
        }

        for(byte chNum = 0; chNum < NoteCmds.Length; chNum++) //  각 채널의 첫 명령의 Tick이 0이 아니면 MML에 쉼표(r)를 넣기 위해 Tick이 0인 명령 삽입
            if(NoteCmds[chNum].Count != 0 && NoteCmds[chNum][0].Tick != 0)
                NoteCmds[chNum].Insert(0, new FurnaceCommand(0, 0, chNum, "NOTE_OFF", 0, 0));
        if(DrumCmds.Count != 0 && DrumCmds[0].Tick != 0)
            DrumCmds.Insert(0, new FurnaceCommand(0, 0, 16, "NOTE_ON", 0, 0));  // Channel Number outside 9~14 in DrumCmds are regarded as Rest

        cmdStreamParser.InsertNoteOffAtStartOfEachOrder();
        cmdStreamParser.RemoveUselessPortamentoCommands();
        cmdStreamParser.FixRetriggerCommands();
        cmdStreamParser.ReorderCommands();
#if RELEASE
        } catch(Exception e) {
            var errMsg = $"An error has occured while parsing the command stream at line {curReadingLineNum}.\n\nStackTrace: {e.StackTrace}\n\nMsg: {e.Message}\n\n\n\n";
            LogTextBox.Text = errMsg;
        }
#endif
        Sr.Close();
        return true;
    }


    private bool Convert()
    {
        ResultOutputTextBox.Clear();

        var songName = MetaTitleTextbox.Text.Length != 0 ? MetaTitleTextbox.Text : PublicValue.SongInfo.SongName;
        var composer = MetaComposerTextbox.Text.Length != 0 ? MetaTitleTextbox.Text : PublicValue.SongInfo.Author;
        var arranger = MetaArrangerTextbox.Text;
        var tempo    = int.TryParse(MetaTempoTextbox.Text, out _) ? MetaTempoTextbox.Text : ConvertCmdStreamToMML.ConvertTickrateToTempo(PublicValue.Subsong.TickRate).ToString();
        var option   = MetaOptionTextbox.Text.Length != 0 ? MetaOptionTextbox.Text : "/v/c";
        var filename = MetaFilenameTextbox.Text.Length != 0 ? MetaFilenameTextbox.Text : ".M2";
        var zenlen   = MetaZenlenTextbox.Text.Length != 0 ? MetaZenlenTextbox.Text : "192";
        var voldown  = GetVoldownMeta();

        /* Metadata */
        var metaSb = new StringBuilder();
        metaSb.AppendLine("; Converted with FurnaceCommandStream2MML").AppendLine()
           .AppendLine($"#Title\t\t{songName}")
           .AppendLine($"#Composer\t{composer}")
           .AppendLine($"#Arranger\t{arranger}")
           .AppendLine($"#Tempo\t\t{tempo}")
           .AppendLine($"#Option\t\t{option}")
           .AppendLine($"#Filename\t{filename}")
           .AppendLine($"#Zenlen\t\t{zenlen}")
           .AppendLine($"#Volumedown\t{voldown}")
           .AppendLine($"{PublicValue.Memo}")
            // .AppendLine("#Memo\t\tConverted with FurnaceCommandStream2MML")
           .AppendLine()
           .AppendLine();
        ResultOutputTextBox.AppendText(metaSb.ToString());

        var instSb = ConvertFurnaceToMML.ConvertInstrument(new StringBuilder());
        ResultOutputTextBox.AppendText(instSb.ToString());


        /* Initialize Order StringBuilder */
        var orderSb = new StringBuilder[MaxOrderNum + 1];
        for(var orderNum = 0; orderNum < orderSb.Length; orderNum++) {
            orderSb[orderNum] = new StringBuilder();
            orderSb[orderNum].AppendLine($";;; {orderNum:X2}[{orderNum:D3}] ({CmdStreamToMMLUtil.GetOrderStartTick(orderNum)}~{CmdStreamToMMLUtil.GetOrderStartTick(orderNum + 1)} Tick)");
        }

        var loopPointOrder = TxtOutputToMMLUtil.GetLoopPoint(OtherEffects);
        if(loopPointOrder != -1)
            AppendLoop(orderSb[loopPointOrder]);

        /* Convert FM/SSG, Drum */
        ConvertFurnaceToMML.ConvertNotesToMML(orderSb);
        ConvertFurnaceToMML.ConvertDrumsToMML(orderSb);

        /* Output results to ResultOutputTextBox */
        foreach(var ordSb in orderSb)
            ResultOutputTextBox.AppendText(ordSb.AppendLine().ToString());

        return true;

        #region Local Functions

        /* ---------------------------------------- Local Functions ------------------------------------------- */
        string GetVoldownMeta()
        {
            var fmVoldown  = MetaVoldownFMTextbox.Text;
            var ssgVoldown = MetaVoldownSSGTextbox.Text;
            var rhyVoldown = MetaVoldownRhythmTextbox.Text;

            var sb = new StringBuilder();
            sb.Append(fmVoldown.Length != 0 ? $"F{fmVoldown}" : "F18")
               .Append(ssgVoldown.Length != 0 ? $"S{ssgVoldown}" : "")
               .Append(rhyVoldown.Length != 0 ? $"R{rhyVoldown}" : "");

            return sb.ToString();
        }

        void AppendLoop(StringBuilder ordSb)
        {
            var noteCmdListLen = PublicValue.NoteCmds.Length;
            for(var chNum = 0; chNum < noteCmdListLen; chNum++) {
                var noteCmdCh = NoteCmds[chNum];
                if(noteCmdCh.Count == 0)
                    continue;

                var firstNoteOnCmd = CmdStreamToMMLUtil.GetFirstNoteOn(noteCmdCh);
                if(firstNoteOnCmd.CmdType.Equals("NO_NOTE_ON")) // if there's no NOTE_ON on the channel
                    continue;

                ordSb.Append(CmdStreamToMMLUtil.ConvertChannel(chNum));
            }

            if(DrumCmds.Count != 0)
                ordSb.Append('K');

            ordSb.Append(" L").AppendLine();
        }

        #endregion
    }

    private void CountCharSize()
    {
        var outputText = ResultOutputTextBox.Text;
        CharCountLabel.Content = $"Character Count: {outputText.Length:N0}";

        var sizeKb       = Encoding.Default.GetByteCount(outputText) / 1000f;
        var limitPercent = sizeKb / 61;
        SizeLabel.Content = $"Size/SizeLimit:\n    {sizeKb:N2}/61 KB ({limitPercent:P1})";
    }

    private void ConvertStartButton_Click(object sender, RoutedEventArgs e)
        => StartConvert();

    private void ClipboardCopyButton_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(ResultOutputTextBox.Text);
        LogInfo("Output is copied to clipboard.");
    }


    private static void OnClosing(object? sender, CancelEventArgs e)
    {
        foreach(var win in Application.Current.Windows) { // MainWindow가 닫히면 다른 창도 닫혀야 함
            if(win is not MainWindow)
                ((Window) win).Close();
        }
    }

}