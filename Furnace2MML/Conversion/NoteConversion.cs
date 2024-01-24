using System.Text;
using static FurnaceCommandStream2MML.Utils.Midi2MMLUtil;

namespace FurnaceCommandStream2MML.Conversion;

public static class NoteConversion
{
	public static string FormatNoteLength(long tickLengthP, long[] validFractionLength, long defaultFractionLength)
	{
		var tickLength = tickLengthP;

		if(tickLength % 20 != 0)  // tickLength는 MMLClock 단위 변환을 위해 20으로 나누어 떨어지는 값을 가져야 한다.
			tickLength += 20 - tickLength % 20;
		

		var strBuilder = new StringBuilder();

		var  fracLengthResultList  = new List<long>();
		var  clockLengthResultList = new List<long>();
		var  validFracLenIndex     = 0;

		while(tickLength > 0) {  // tickLength -> FractionLength 변환
			if(validFracLenIndex >= validFractionLength.Length) { // ValidFractionLength 배열을 모두 돌았음에도 틱에 대응하는 분수표기를 찾지 못한 경우 => 분수 표기로 나타내지 못하는 길이 => MML Clock 단위로 표기한다 
				var clockLen = TickToClockCycle(tickLength);
				var tickLen = ClockCycleToTick(clockLen);
			        
				clockLengthResultList.Add(clockLen);
				tickLength -= tickLen;

				continue;
			}
			
		        
			var isTickLengthExact = GetIsExactFractionLength(tickLength);  // validFractionLength의 길이와 정확이 일치한가의 여부

			if(isTickLengthExact) {
				fracLengthResultList.Add(TickLengthToFractionLength(tickLength));
				tickLength -= tickLength;
			} else {
				var curTick = FractionLengthToTickLength(validFractionLength[validFracLenIndex]);

				var isValidFracLen = tickLength / curTick >= 1; //isValidTick, 현재 curTick값에 의한 분수표기가 fracLengthResultList에 들어가는 것이 올바른지 여부
                
				if(isValidFracLen) {
					fracLengthResultList.Add(validFractionLength[validFracLenIndex]);
					tickLength -= curTick;
				} else
					validFracLenIndex++;
			}
		}

		for(var i = 0; i < fracLengthResultList.Count; i++) {  // 변환되어 저장된 값에 따라 문자열 만들기
			var fracLength = fracLengthResultList[i];
			var isDefaultLength = fracLength == defaultFractionLength;
			
			var fracLenStr = fracLength.ToString();

			/*if(fracLength == fracLengthResultList[i - 1] * 2) 
				fracLenStr = ".";*/

			if(i == 0 && isDefaultLength)
				strBuilder.Append("&");
			else if(i != 0 && fracLength == fracLengthResultList[i - 1] * 2) // 현재 분수표기 길이 == 이전 분수표기 길이 * 2 => 점n분음표로 나타낼 수 있는가의 여부
				strBuilder.Append(".");
			else
				strBuilder.Append($"&{fracLenStr}");
			//DebuggingAndTestingTextBox.AppendText($" &{d}");
		}

		foreach(var clockLength in clockLengthResultList) 
			strBuilder.Append($"&%{clockLength}");
		
		// strBuilder = ReplaceComplicatedLengthStr(strBuilder);  // 복잡하게 변환된 길이를 단순하게 되도록 치환함

		return strBuilder.ToString().Remove(0,1);
	}

	public static string FormatOctaveChangeSymbol(int octaveDifference)
	{
		var strBuilder = new StringBuilder();

		var octChangeSym = octaveDifference switch {
			> 0 => ">",
			< 0 => "<",
			_   => throw new ArgumentOutOfRangeException(nameof(octaveDifference))
		};
			
		for(var i = 1; i <= Math.Abs(octaveDifference); i++)
			strBuilder.Append(octChangeSym);

		return strBuilder.ToString();
	}

	private static bool GetIsExactFractionLength(long tickLength)
	{
		var result = TickLengthToFractionLength(tickLength);
		return result != -1;
	}
}