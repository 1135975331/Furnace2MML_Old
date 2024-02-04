﻿using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
namespace FurnaceCommandStream2MML.Utils;

public static partial class Util
{
	public static string GetFileExtensionFromPath(string filePath)
	{
		var splitPathStr = filePath.Split('.');
		return splitPathStr[^1];
	}
    
	public static string? ReadLineCountingLineNum(this TextReader reader, ref int curNumberLine)
	{
		curNumberLine++;
		return reader.ReadLine();
	}

	public static string ToEscapedString(this string origin)
		=> origin.Replace(@"\", @"\\");
    
	/// <summary>
	/// Remove method for optimized for loop
	/// </summary>
	/// <param name="list"></param>
	/// <param name="elem"></param>
	/// <param name="listLen"></param>
	/// <typeparam name="T"></typeparam>
	/// <returns></returns>
    public static bool Remove<T>(this List<T> list, T elem, ref int listLen)
    {
        listLen -= 1;
        return list.Remove(elem);
    }

	// Referenced code from: https://stackoverflow.com/questions/10293236/accessing-the-scrollviewer-of-a-listbox-from-c-sharp
	public static Visual GetDescendantByType(Visual element, Type type)
	{
		if (element == null)
			return null;
        
		if (element.GetType() == type)
			return element;
        
		Visual foundElement = null;
		if (element is FrameworkElement)
			(element as FrameworkElement).ApplyTemplate();
        
		for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++) {
			var visual = VisualTreeHelper.GetChild(element, i) as Visual;
			foundElement = GetDescendantByType(visual, type);
			if (foundElement != null)
				break;
		}
        
		return foundElement;
	}

	public static bool EqualsAny(this string source, params string[] otherStrings)
		=> otherStrings.Any(source.Equals);
    
	public static string TrimExceptWords(this string str)
		=> RegexExceptWords().Replace(str, "");
    public static string LeaveAlphabetOnly(this string str) 
	    => RegexExceptAlphabet().Replace(str, "");
    public static string LeaveNumberOnly(this string str) 
	    => RegexExceptNumber().Replace(str, "");
    
    [GeneratedRegex("[^a-zA-Z]")]
    private static partial Regex RegexExceptAlphabet();
    
    // [GeneratedRegex("^[0-9]*$")]
    [GeneratedRegex("[^0-9]")]
    private static partial Regex RegexExceptNumber();
    
    [GeneratedRegex(@"^[\s-]+|[\s0-9]+$|:(.*)")]
    private static partial Regex RegexExceptWords();
}