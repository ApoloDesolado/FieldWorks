// Copyright (c) 2004-2018 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Diagnostics;

namespace SIL.FieldWorks.Filters
{
	/// <summary>
	/// Just a shell class for containing runtime Switches for controling the diagnostic output.
	/// </summary>
	public class RuntimeSwitches
	{
		/// Tracing variable - used to control when and what is output to the debug and trace listeners
		public static TraceSwitch RecordTimingSwitch = new TraceSwitch("FilterRecordTiming", "Used for diagnostic timing output", "Off");
	}
}