namespace FurnaceCommandStream2MML.Utils;

public static class Midi2MMLUtil
{
	public static int GetOctaveDifference(int curOctave, int curChannelOctave)
	{
		return curChannelOctave - curOctave;
	}

	/// <summary>
	///		Duration 단위 (n분음표, MML Clock Cycle, MidiTick)를 변환하는 메소드
	///		단위를 기입하는 매개변수는 <c>string</c> 타입이며,
	///		<c>"noteLength", "mmlClock", "midiTick"</c> 중 하나이어야 한다.
	/// </summary>
	/// 
	/// <param name="value">변환할 값</param>
	/// <param name="unitToBeConverted">변환 전 value의 단위</param>
	/// <param name="unitToConvert">변환 후 value의 단위</param>
	/// <exception cref="ArgumentOutOfRangeException">2, 3번째 매개변수에 noteLength, mmlClock, midiTick을 제외한 다른 문자열이 들어온 경우</exception>>
	/// <returns>단위가 변환된 value값</returns>
	public static long ConvertDurationUnit(long value, string unitToBeConverted, string unitToConvert)
	{
		return unitToBeConverted switch { 
			"noteLength" => unitToConvert switch {
				"mmlClock" => TickToClockCycle(FractionLengthToTickLength(value)),  //noteLength -> midiTick -> mmlClock
				"midiTick" => FractionLengthToTickLength(value),
				_          => throw new ArgumentOutOfRangeException($"Invalid unitToConvert: \"{unitToConvert}\"")
			},
			"mmlClock" => unitToConvert switch {
				"noteLength" => TickLengthToFractionLength(value),  //mmlClock -> midiTick -> 
				"midiTick"   => ClockCycleToTick(value),
				_            => throw new ArgumentOutOfRangeException($"Invalid unitToConvert: \"{unitToConvert}\"")
			},
			"midiTick" => unitToConvert switch {
				"mmlClock"   => TickLengthToFractionLength(value),
				"noteLength" => TickLengthToFractionLength(value),
				_            => throw new ArgumentOutOfRangeException($"Invalid unitToConvert: \"{unitToConvert}\"")
			},
			_ => throw new ArgumentOutOfRangeException($"Invalid unitToBeConverted: \"{unitToBeConverted}\"")
		};
	}
	
	public static string MidiTickToMMLDuration(long midiTick)
	{
		var noteLength = midiTick switch {
			1920 => "1",
			960  => "2",
			640  => "3",
			480  => "4",
			320  => "6",
			240  => "8",
			160  => "12",
			120  => "16",
			80   => "24",
			60   => "32",
			40   => "48",
			20   => "96",
			_    => $"%{TickToClockCycle(midiTick)}"
		};

		return noteLength;
	}
	
	
	
	public static long TickLengthToFractionLength(long midiTickLength)
	{
		var noteLength = midiTickLength switch {
			1920 => 1,
			960  => 2,
			640  => 3,
			480  => 4,
			320  => 6,
			240  => 8,
			160  => 12,
			120  => 16,
			80   => 24,
			60   => 32,
			40   => 48,
			20   => 96,
			_    => -1
		};

		return noteLength;
	}
		
	public static long FractionLengthToTickLength(long fractionLength)
	{
		var tickLength = fractionLength switch {
			1  => 1920,
			2  => 960,
			3  => 640,
			4  => 480,
			6  => 320,
			8  => 240,
			12 => 160,
			16 => 120,
			24 => 80,
			32 => 60,
			48 => 40,
			96 => 20,
			_  => -1
		};

		return tickLength;
	}

	/// <summary>
	/// 1 MML Clock == 20 MIDI Tick Length
	/// </summary>
	/// <param name="midiTickLength">Tick Length in MIDI</param>
	/// <returns></returns>
	public static long TickToClockCycle(long midiTickLength) 
	{
		return (long)Math.Round((double)midiTickLength / 20);
	}
	
	/// <summary>
	/// 1 MML Clock == 20 MIDI Tick Length
	/// </summary>
	/// <param name="mmlClock">Clock Length in MML</param>
	/// <returns></returns>
	public static long ClockCycleToTick(long mmlClock) 
	{
		return mmlClock * 20;
	}
}