using System;

namespace jCaballol94.IDE.Sublime
{
	[Flags]
	public enum ProjectGenerationFlag
	{
		// Same as the other packages
		None				= 0x00,
		Embedded			= 0x01,
		Local				= 0x02,
		Registry			= 0x04,
		Git					= 0x08,
		BuiltIn				= 0x10,
		Unknown				= 0x20,
		PlayerAssemblies	= 0x40,
		LocalTarBall		= 0x80,
		// New for this package
		OmniSharp			= 0x100
	}
}